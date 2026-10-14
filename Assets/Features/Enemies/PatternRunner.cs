using Features.Characters;
using Features.Combat;
using Features.Economy;
using Features.Events;
using Features.Movement;
using Features.Spells;
using Systems.EventBusses;
using UnityEngine;
using VContainer;

namespace Features.Enemies {
    /// <summary>
    /// Plays a boss's pattern (design doc §9.6), on the Boss prefab variant next to EnemyAI:
    /// - the phase follows the HP share (PatternPhase.HealthThreshold), its steps run in sequence
    ///   or by weighted draw;
    /// - Wait leaves the control to the usual AI; CastSpell / Charge / Summon suspend it for the
    ///   step (telegraph, then the effect, then a recovery);
    /// - the boss's items (BossDefinition.Items, 3 max) give their stats and passives at Init.
    /// Publishes BossSpawned / BossHealth / BossDefeated for the boss bar and the RunDirector.
    /// </summary>
    public class PatternRunner : MonoBehaviour {
        [SerializeField] private Character owner;
        [SerializeField] private EnemyAI ai;
        [SerializeField] private CharacterMovement movement;
        [SerializeField] private BasicAttacker attacker;
        [SerializeField] private SpellCaster caster;

        private enum StepState { None, Telegraph, Active, Recovery }

        private IEventBus _eventBus;
        private EnemyDirector _director;
        private SpellExecutor _executor;
        private HordeSpawner _spawner;
        private BossDefinition _boss;
        private int _phase = -1, _step;
        private StepState _state;
        private float _stateEnd;
        private PatternStep _current;
        private Vector3 _chargeDirection;
        private bool _chargeHit;
        private float _lastHealth = -1f, _lastMax = -1f;

        public int Phase => _phase;
        public bool IsBoss => _boss != null;

        [Inject]
        public void Construct(IEventBus eventBus, EnemyDirector director, SpellExecutor executor, HordeSpawner spawner) {
            _eventBus = eventBus;
            _director = director;
            _executor = executor;
            _spawner = spawner;
        }

        private void Awake() {
            owner.Initialized += OnInitialized;
            owner.Died += OnDied;
            owner.Damaged += OnDamaged;
        }

        private void OnDestroy() {
            owner.Initialized -= OnInitialized;
            owner.Died -= OnDied;
            owner.Damaged -= OnDamaged;
        }

        private void OnInitialized(Character character) {
            _boss = character.Definition as BossDefinition;
            _phase = -1;
            _state = StepState.None;
            if (_boss == null) return;
            character.Carry(_boss.Items);   // stats, passives, icons over the health bar
            _eventBus.Publish(new BossSpawnedEvent(_boss.DisplayName, _boss.Title));
            EnterPhase(0);
        }

        private void OnDamaged(Character _, float __) {
            if (_boss == null) return;
            PublishHealth();
            PatternPhase[] phases = _boss.Pattern != null ? _boss.Pattern.Phases : null;
            if (phases == null) return;
            float share = owner.Health.Current / Mathf.Max(1f, owner.Health.Max);
            // Next phase(s) whose threshold is reached; the current step finishes first
            int next = _phase;
            while (next + 1 < phases.Length && share <= phases[next + 1].HealthThreshold) next++;
            if (next != _phase) EnterPhase(next);
        }

        private void OnDied(Character _) {
            if (_boss == null) return;
            EndStep();
            _eventBus.Publish(new BossHealthEvent(0f, owner.Health.Max, _phase + 1));
            _eventBus.Publish(new BossDefeatedEvent());
        }

        private void EnterPhase(int phase) {
            _phase = phase;
            _step = 0;
            PublishHealth();
        }

        private void Update() {
            if (_boss == null || !owner.IsAlive) return;
            if (!Mathf.Approximately(_lastHealth, owner.Health.Current) || !Mathf.Approximately(_lastMax, owner.Health.Max)) PublishHealth();
            if (_boss.Pattern == null || owner.Status.IsStunned) return;
            Character player = _director.Player;
            if (player == null || !player.IsAlive) {
                EndStep();
                return;
            }

            switch (_state) {
                case StepState.None:
                    StartStep(NextStep(), player);
                    break;
                case StepState.Telegraph when Time.time >= _stateEnd:
                    StartCharge();
                    break;
                case StepState.Active:
                    UpdateActive(player);
                    break;
                case StepState.Recovery when Time.time >= _stateEnd:
                    EndStep();
                    break;
            }
        }

        private PatternStep NextStep() {
            PatternPhase phase = _boss.Pattern.Phases[Mathf.Clamp(_phase, 0, _boss.Pattern.Phases.Length - 1)];
            if (phase.Steps == null || phase.Steps.Length == 0) return new PatternStep { Kind = PatternStepKind.Wait, Duration = 1f };
            if (phase.Order == PatternOrder.Sequence) return phase.Steps[_step++ % phase.Steps.Length];

            float total = 0f;
            foreach (PatternStep s in phase.Steps) total += Mathf.Max(0f, s.Weight);
            float roll = Random.value * total;
            foreach (PatternStep s in phase.Steps) {
                roll -= Mathf.Max(0f, s.Weight);
                if (roll <= 0f) return s;
            }
            return phase.Steps[0];
        }

        private void StartStep(PatternStep step, Character player) {
            _current = step;
            if (step.Kind == PatternStepKind.Wait) {
                ai.Suspended = false;
                SetState(StepState.Active, step.Duration);
                return;
            }

            ai.Suspended = true;
            attacker.Cancel();
            movement.Stop();
            Vector3 origin = owner.transform.position;
            Vector3 toPlayer = SpellMath.FlatDirection(origin, player.transform.position, owner.transform.forward);

            switch (step.Kind) {
                case PatternStepKind.CastSpell:
                    if (step.Spell == null) {
                        SetState(StepState.Recovery, step.Duration);
                        return;
                    }
                    Vector3 point = step.AtSelf ? origin : SpellMath.ClampToRange(origin, player.transform.position, step.Spell.Range);
                    var ctx = new CastContext { Origin = origin, Direction = toPlayer, TargetPoint = point, TargetUnit = player };
                    if (caster.Cast(step.Spell, ctx, 0)) _executor.Telegraph(step.Spell, ctx, step.Spell.CastTime);
                    SetState(StepState.Recovery, step.Spell.CastTime + step.Duration);
                    return;
                case PatternStepKind.Charge:
                    _chargeDirection = toPlayer;
                    _chargeHit = false;
                    _executor.TelegraphLine(origin, toPlayer, step.ChargeDistance, owner.BodyRadius + 0.3f, step.Telegraph);
                    SetState(StepState.Telegraph, step.Telegraph);
                    return;
                case PatternStepKind.Summon:
                    if (step.SummonPrefab != null && step.Summon != null) {
                        for (int i = 0; i < step.Count; i++) {
                            float angle = i * Mathf.PI * 2f / Mathf.Max(1, step.Count);
                            Vector3 spot = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (owner.BodyRadius + 1.5f);
                            if (UnityEngine.AI.NavMesh.SamplePosition(spot, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                                _spawner.Spawn(step.SummonPrefab, step.Summon, hit.position);
                        }
                    }
                    SetState(StepState.Recovery, step.Duration);
                    return;
            }
        }

        private void StartCharge() {
            float duration = _current.ChargeDistance / Mathf.Max(0.1f, _current.ChargeSpeed);
            movement.Dash(_chargeDirection, _current.ChargeSpeed, duration);
            SetState(StepState.Active, duration);
        }

        private void UpdateActive(Character player) {
            if (_current.Kind == PatternStepKind.Charge) {
                if (!_chargeHit) {
                    Vector3 d = player.transform.position - owner.transform.position;
                    d.y = 0f;
                    if (d.magnitude <= owner.BodyRadius + player.BodyRadius + 0.3f) {
                        _chargeHit = true;
                        player.TakeDamage(new DamageInfo(_current.ChargeDamage, DamageType.Physical, false, false, owner));
                    }
                }
                if (Time.time >= _stateEnd || !movement.IsDashing) {
                    movement.Stop();
                    SetState(StepState.Recovery, _current.Duration);
                }
                return;
            }
            if (Time.time >= _stateEnd) EndStep();   // Wait over
        }

        private void SetState(StepState state, float duration) {
            _state = state;
            _stateEnd = Time.time + duration;
        }

        private void EndStep() {
            _state = StepState.None;
            if (ai != null) ai.Suspended = false;
        }

        private void PublishHealth() {
            if (owner.Health == null) return;
            _lastHealth = owner.Health.Current;
            _lastMax = owner.Health.Max;
            _eventBus.Publish(new BossHealthEvent(_lastHealth, _lastMax, _phase + 1));
        }
    }
}
