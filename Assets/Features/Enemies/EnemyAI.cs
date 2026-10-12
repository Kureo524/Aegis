using Features.Characters;
using Features.Combat;
using Features.Movement;
using Features.Spells;
using Features.Stats;
using UnityEngine;
using UnityEngine.AI;
using VContainer;

namespace Features.Enemies {
    /// <summary>
    /// "Minimally smart" enemy (design doc §9.6), on the Enemy prefab variant: Chase → Attack → Cast,
    /// against the player, through the same components as the player (CharacterMovement,
    /// BasicAttacker, SpellCaster).
    ///
    /// - Chase: horde enemies follow the shared flow field (no path of their own, the agent is
    ///   switched off), elites their own NavMesh path. Bodies block each other (creep block).
    /// - Attack: in AttackRange, the basic attack takes over; ranged enemies stop at their range.
    ///   The attacker never chases by itself (ChasesTarget = false): out of range, this moves.
    /// - Cast: TFT mana (EnemyDefinition) gained per second, per landed attack and per hit taken;
    ///   when full and the player is within the special spell's range, the spell is telegraphed and
    ///   cast at the player, then the mana goes back to StartMana.
    /// No Update: <see cref="EnemyDirector"/> calls <see cref="Think"/> a few times per second.
    /// </summary>
    public class EnemyAI : MonoBehaviour {
        [SerializeField] private Character owner;
        [SerializeField] private CharacterMovement movement;
        [SerializeField] private BasicAttacker attacker;
        [SerializeField] private SpellCaster caster;
        [SerializeField] private NavMeshAgent agent;

        private EnemyDirector _director;
        private FlowFieldService _flowField;
        private SpellExecutor _executor;
        private HordeSettings _settings;
        private EnemyDefinition _definition;
        private float _lastThink;
        private float _nextRepath;

        public EnemyDefinition Definition => _definition;
        /// <summary>A boss pattern step has the control: no decision until it gives it back.</summary>
        public bool Suspended { get; set; }

        [Inject]
        public void Construct(EnemyDirector director, FlowFieldService flowField, SpellExecutor executor, HordeSettings settings) {
            _director = director;
            _flowField = flowField;
            _executor = executor;
            _settings = settings;
        }

        private void Awake() {
            owner.Initialized += OnInitialized;
            owner.Damaged += OnDamaged;
            attacker.Hit += OnAttackHit;
        }

        private void OnDestroy() {
            owner.Initialized -= OnInitialized;
            owner.Damaged -= OnDamaged;
            attacker.Hit -= OnAttackHit;
            _director?.Unregister(this);
        }

        private void OnDisable() => _director?.Unregister(this);

        private void OnInitialized(Character character) {
            _definition = character.Definition as EnemyDefinition;
            if (_definition == null) {
                _director?.Unregister(this);
                return;
            }
            attacker.ChasesTarget = false;
            // Hundreds of agents would cost a crowd simulation for nothing: the flow field steers them
            agent.enabled = _definition.Navigation == EnemyNavigation.NavMesh;
            if (_definition.SpecialSpell != null) owner.Mana.Set(_definition.StartMana);
            _lastThink = Time.time;
            _nextRepath = 0f;
            Suspended = false;
            _director?.Register(this);
        }

        /// <summary>One decision; called by the EnemyDirector.</summary>
        public void Think() {
            float elapsed = Time.time - _lastThink;
            _lastThink = Time.time;
            if (_definition == null || !owner.IsAlive || Suspended) return;

            Character player = _director.Player;
            if (player == null || !player.IsAlive) {
                if (attacker.IsAttacking) attacker.Cancel();
                if (movement.IsMoving) movement.Stop();
                return;
            }

            if (_definition.SpecialSpell != null) owner.Mana.Restore(_definition.ManaPerSecond * elapsed);
            if (caster.IsCasting || attacker.IsWindingUp) return;

            float distance = DistanceTo(player);
            if (TryCastSpecial(player, distance)) return;

            if (distance <= owner.Stats.Value(StatType.AttackRange)) {
                if (attacker.Target != player) attacker.Attack(player);   // stops and attacks
                return;
            }
            Chase(player);
        }

        private void Chase(Character player) {
            Vector3 position = owner.transform.position;
            if (_definition.Navigation == EnemyNavigation.NavMesh) {
                if (Time.time < _nextRepath && movement.IsMoving) return;
                _nextRepath = Time.time + _settings.RepathInterval;
                movement.SetDestination(player.transform.position);
                return;
            }

            FlowField field = _flowField.Field;
            if (field == null || !field.TryGetDirection(position, out Vector3 direction)) {
                direction = player.transform.position - position;   // same cell, or off the grid
            }
            movement.Steer(direction);
        }

        private bool TryCastSpecial(Character player, float distance) {
            SpellDefinition spell = _definition.SpecialSpell;
            if (spell == null || owner.Mana.Max <= 0f || !owner.Mana.IsFull) return false;
            if (spell.Targeting != TargetingType.Self && distance > spell.Range) return false;

            Vector3 origin = owner.transform.position;
            Vector3 target = player.transform.position;
            Vector3 direction = SpellMath.FlatDirection(origin, target, owner.transform.forward);
            var ctx = new CastContext {
                Origin = origin,
                Direction = direction,
                TargetPoint = SpellMath.ClampToRange(origin, target, spell.Range),
                TargetUnit = player,
            };
            if (!caster.Cast(spell, ctx, 0)) return false;
            _executor.Telegraph(spell, ctx, spell.CastTime);
            owner.Mana.Set(_definition.StartMana);
            return true;
        }

        private void OnAttackHit(Character _, float __) {
            if (_definition?.SpecialSpell != null && owner.IsAlive) owner.Mana.Restore(_definition.ManaPerAttack);
        }

        private void OnDamaged(Character _, float __) {
            if (_definition?.SpecialSpell != null && owner.IsAlive) owner.Mana.Restore(_definition.ManaPerHitTaken);
        }

        private float DistanceTo(Character other) {
            Vector3 delta = other.transform.position - owner.transform.position;
            delta.y = 0f;
            return delta.magnitude - other.BodyRadius;
        }
    }
}
