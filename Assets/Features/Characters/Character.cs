using System;
using Features.Combat;
using Features.Events;
using Features.Movement;
using Features.Progression;
using Features.Stats;
using Features.UI;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.AI;
using VContainer;

namespace Features.Characters {
    /// <summary>
    /// Entry point of the generic character prefab (Assets/Prefabs/Characters/Character, design doc
    /// §2, §8; Player and TrainingDummy are variants of it). <see cref="Init"/>
    /// configures it from a <see cref="CharacterDefinition"/>; it can be called again to reuse the
    /// object (pooling, new run).
    ///
    /// Bus traffic stays coarse (§14.3): every character publishes its hits (DamageDealtEvent, for
    /// the damage numbers) and enemies their death (EnemyKilledEvent); only the player publishes its
    /// HP, XP, level and stats. Per-character reactions use the C# events below (CharacterEvents).
    /// </summary>
    public class Character : MonoBehaviour {
        [SerializeField] private CharacterMovement movement;
        [SerializeField] private CharacterVisual visual;
        [SerializeField] private BoxCollider body;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private BasicAttacker attacker;
        [SerializeField] private HealthBarView healthBar;

        private IEventBus _eventBus;
        private ExperienceCurve _experienceCurve;
        private Rigidbody _rigidbody;
        private readonly DisposableBag _busSubscriptions = new();

        public CharacterDefinition Definition { get; private set; }
        public StatController Stats { get; } = new();
        public CharacterLevel Level { get; private set; }
        public Health Health { get; private set; }
        public ManaPool Mana { get; private set; }
        /// <summary>Temporary stat modifiers (buffs, debuffs), ticked every frame.</summary>
        public TimedModifiers Buffs { get; } = new();
        public bool IsAlive => Health != null && !Health.IsDead;
        public bool IsPlayer => Definition != null && Definition.Faction == Faction.Player;
        public Faction Faction => Definition != null ? Definition.Faction : Faction.Neutral;
        public float BodyRadius => Definition != null ? Definition.BodySize.x / 2f : 0.5f;
        public Vector3 HeadPosition => transform.position + Vector3.up * (Definition != null ? Definition.BodySize.y : 2f);

        // Per-character events (design doc §8.1): passives, items, augments, dummy respawn…
        public event Action<Character, float> Damaged;   // (self, damage taken)
        public event Action<Character> Died;
        public event Action<Character> Revived;
        /// <summary>After every Init: same-prefab components configure themselves from the definition.</summary>
        public event Action<Character> Initialized;

        [Inject]
        public void Construct(IEventBus eventBus, ExperienceCurve experienceCurve) {
            _eventBus = eventBus;
            _experienceCurve = experienceCurve;
        }

        private void Awake() {
            _rigidbody = GetComponent<Rigidbody>();
        }

        public void Init(CharacterDefinition definition) {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            _busSubscriptions.Dispose();
            if (Health != null) {
                Health.Died -= OnDied;
                Health.Detach();
            }
            Mana?.Detach();

            Definition = definition;
            Buffs.Clear();
            ApplyBody(definition);

            // Order matters (§12): stats first, everything else reads them.
            Stats.Init(definition.BaseStats);
            Level = new CharacterLevel(Stats, definition.GrowthPerLevel, _experienceCurve);
            Health = new Health(Stats.Get(StatType.MaxHealth));
            Health.Died += OnDied;
            Mana = new ManaPool(Stats.Get(StatType.MaxMana));
            if (movement != null) movement.Init(Stats.Get(StatType.MoveSpeed));
            if (healthBar != null) healthBar.Bind(this);
            SetBodyActive(true);

            if (IsPlayer) BindPlayerToBus();
            Initialized?.Invoke(this);
        }

        // TODO(phase 6): regeneration of hundreds of enemies → one central tick.
        private void Update() {
            if (!IsAlive) return;
            Buffs.Tick(Time.deltaTime);
            Health.Heal(Stats.Value(StatType.HealthRegen) * Time.deltaTime);
            Mana.Restore(Stats.Value(StatType.ManaRegen) * Time.deltaTime);
        }

        private void OnDestroy() {
            _busSubscriptions.Dispose();
            Health?.Detach();
            Mana?.Detach();
        }

        public bool IsHostileTo(Character other) =>
            other != null && other != this && Faction != Faction.Neutral && other.Faction != Faction.Neutral
            && other.Faction != Faction;

        /// <summary>
        /// Receives a hit (design doc §6): resistances, HP, then the attacker's sustain.
        /// Returns the damage actually taken.
        /// </summary>
        public float TakeDamage(in DamageInfo info) {
            if (!IsAlive) return 0f;

            float final = DamageCalculator.Mitigate(info, Stats.Value(StatType.Armor), Stats.Value(StatType.MagicResist));
            float taken = Health.TakeDamage(final);

            _eventBus.Publish(new DamageDealtEvent(HeadPosition, taken, info.Type, info.IsCrit, IsPlayer));
            Damaged?.Invoke(this, taken);
            if (info.Source != null) info.Source.OnDamageDealt(taken, info.IsBasicAttack);
            return taken;
        }

        /// <summary>Back to life at full HP (respawn, revive effects).</summary>
        public void Revive() {
            if (Health == null || IsAlive) return;
            Health.Revive();
            SetBodyActive(true);
            Revived?.Invoke(this);
        }

        // Omnivamp / lifesteal (§6 step 6)
        private void OnDamageDealt(float damageDealt, bool isBasicAttack) {
            if (!IsAlive || damageDealt <= 0f) return;
            Health.Heal(DamageCalculator.Sustain(damageDealt, isBasicAttack,
                Stats.Value(StatType.Omnivamp), Stats.Value(StatType.Lifesteal), Stats.Value(StatType.HealShieldPower)));
        }

        private void OnDied() {
            if (attacker != null) attacker.Cancel();
            if (movement != null) movement.Stop();
            SetBodyActive(false);

            if (Definition is EnemyDefinition enemy)
                _eventBus.Publish(new EnemyKilledEvent(enemy.XpReward, enemy.GoldReward, transform.position));
            if (IsPlayer) _eventBus.Publish(new PlayerDiedEvent());

            Died?.Invoke(this);
        }

        // A dead body is invisible, cannot be hit or clicked, and does not fall through the floor.
        private void SetBodyActive(bool active) {
            if (visual != null) visual.gameObject.SetActive(active);
            if (body != null) body.enabled = active;
            if (_rigidbody != null) _rigidbody.isKinematic = !active;
        }

        // Root pivot at the feet: the box stands on the ground, the agent matches it.
        private void ApplyBody(CharacterDefinition definition) {
            if (visual != null) visual.SetSprite(definition.Sprite);

            Vector2 size = definition.BodySize;
            if (body != null) {
                body.size = new Vector3(size.x, size.y, size.x);
                body.center = new Vector3(0f, size.y / 2f, 0f);
            }
            if (agent != null) {
                agent.radius = size.x / 2f;
                agent.height = size.y;
            }
        }

        private void BindPlayerToBus() {
            _busSubscriptions.Add(_eventBus.Subscribe<XpGrantedEvent>(e => Level.AddXp(e.Amount)));

            CharacterLevel level = Level;
            level.XpChanged += PublishXp;
            level.LevelChanged += _ => _eventBus.Publish(new LevelUpEvent(level.Level, level.SkillPoints, level.OverchargePoints));

            Health health = Health;
            health.Changed += (current, max) => _eventBus.Publish(new PlayerHealthChangedEvent(current, max));
            ManaPool mana = Mana;
            mana.Changed += (current, max) => _eventBus.Publish(new PlayerManaChangedEvent(current, max));

            foreach (Stat stat in Stats.Stats.Values) {
                StatType type = stat.Type;
                stat.Changed += value => _eventBus.Publish(new StatChangedEvent(type, value));
            }

            // Initial snapshot for whoever displays it
            foreach (Stat stat in Stats.Stats.Values) _eventBus.Publish(new StatChangedEvent(stat.Type, stat.Value));
            _eventBus.Publish(new PlayerHealthChangedEvent(health.Current, health.Max));
            _eventBus.Publish(new PlayerManaChangedEvent(mana.Current, mana.Max));
            PublishXp();
        }

        private void PublishXp() => _eventBus.Publish(new XpChangedEvent(Level.Level, Level.Xp, Level.XpToNext));
    }
}
