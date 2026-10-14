using System;
using System.Collections.Generic;
using Features.Combat;
using Features.Events;
using Features.Movement;
using Features.Passives;
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
        [Tooltip("Soft shadow on the ground, sized from the body width.")]
        [SerializeField] private Transform shadow;
        [SerializeField] private BoxCollider body;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private BasicAttacker attacker;
        [SerializeField] private HealthBarView healthBar;

        private IEventBus _eventBus;
        private ExperienceCurve _experienceCurve;
        private ProjectileLauncher _projectiles;
        private Spells.SpellSettings _spellSettings;
        private Systems.Pooling.PrefabPool _pool;

        /// <summary>A flash ring on the ground (passive novas, auras).</summary>
        public void PlayRing(Vector3 center, float radius, Color color) {
            if (_pool == null || _spellSettings == null || _spellSettings.ShapeVfx == null) return;
            Spells.SpellVfx vfx = _pool.Get(_spellSettings.ShapeVfx);
            vfx.PlayRing(center, radius, color, 0.4f, _pool.Release);
        }
        private Rigidbody _rigidbody;
        private readonly DisposableBag _busSubscriptions = new();

        public CharacterDefinition Definition { get; private set; }
        public StatController Stats { get; } = new();
        public CharacterLevel Level { get; private set; }
        public Health Health { get; private set; }
        public ManaPool Mana { get; private set; }
        /// <summary>Temporary stat modifiers (buffs, debuffs), ticked every frame.</summary>
        public TimedModifiers Buffs { get; } = new();
        /// <summary>Crowd control, slows and damage over time (§5.7).</summary>
        public StatusEffects Status { get; private set; }
        /// <summary>Per-character events for passives, items and augments (§8.1).</summary>
        public CharacterEvents Events { get; } = new();
        /// <summary>Rules granted by passives (SpellCrit…, §5.6).</summary>
        public CapabilityController Capabilities { get; } = new();
        /// <summary>The character's passives: its own (definition), items', augments' (§8.1).</summary>
        public PassiveController Passives { get; private set; }
        public bool IsAlive => Health != null && !Health.IsDead;

        /// <summary>
        /// Bonus part of a stat (items, augments, buffs): total minus base and level growth, like
        /// MOBA "bonus AD" (§5.1). Ability ratios and adaptive force use it.
        /// </summary>
        public float BonusStat(StatType type) {
            if (Definition == null) return 0f;
            float baseValue = (Definition.BaseStats != null ? Definition.BaseStats.Get(type) : 0f) + (Level?.FlatGrowth(type) ?? 0f);
            return Stats.Value(type) - baseValue;
        }
        public bool IsPlayer => Definition != null && Definition.Faction == Faction.Player;

        // ---- elites (design doc §9.6): enemies that carry items ----
        private readonly List<Economy.ItemDefinition> _carriedItems = new();
        private float _rewardMultiplier = 1f;
        /// <summary>Items this enemy carries (elites, bosses): their stats and passives are on it.</summary>
        public IReadOnlyList<Economy.ItemDefinition> CarriedItems => _carriedItems;
        public bool IsElite { get; private set; }
        public bool IsEliteOrBoss => IsElite || Definition is Enemies.BossDefinition;

        /// <summary>
        /// Gives the character items (stats + passive), like a boss. With <paramref name="elite"/>: an
        /// elite of the horde, tougher (health ×), worth more (gold and XP ×), bigger and marked.
        /// Undone by the next Init (pool reuse).
        /// </summary>
        public void Carry(IReadOnlyList<Economy.ItemDefinition> items, bool elite = false, float healthMultiplier = 1f, float rewardMultiplier = 1f) {
            foreach (Economy.ItemDefinition item in items) {
                if (item == null) continue;
                _carriedItems.Add(item);
                foreach (Economy.StatBonus bonus in item.Stats) Stats.Get(bonus.Stat)?.AddModifier(bonus.ToModifier(item));
                Passives.Add(item.Passive, item);
            }
            if (elite) {
                IsElite = true;
                _rewardMultiplier = rewardMultiplier;
                if (!Mathf.Approximately(healthMultiplier, 1f))
                    Stats.Get(StatType.MaxHealth)?.AddModifier(new StatModifier(healthMultiplier - 1f, ModifierType.PercentMult, _carriedItems));
                if (visual != null) visual.SetElite(true);
            }
            if (healthBar != null) healthBar.ShowItems(_carriedItems, IsElite);
        }
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
        public void Construct(IEventBus eventBus, ExperienceCurve experienceCurve, ProjectileLauncher projectiles, Spells.SpellSettings spellSettings,
                              Systems.Pooling.PrefabPool pool) {
            _pool = pool;
            _eventBus = eventBus;
            _experienceCurve = experienceCurve;
            _projectiles = projectiles;
            _spellSettings = spellSettings;
        }

        /// <summary>A homing bolt from this character to <paramref name="target"/> (item and augment passives: Runaan, Luden…).</summary>
        public void FireBolt(Character target, in DamageInfo damage, Color color) {
            if (_projectiles == null || _spellSettings == null || target == null || !target.IsAlive) return;
            Vector3 muzzle = transform.position + Vector3.up * (Definition.BodySize.y * 0.6f);
            _projectiles.Launch(_spellSettings.HomingProjectile, muzzle, target, damage, 22f, null, color);
        }

        // ---- Adaptive force (§5.1): AD × 0.6 or AP × 1, whichever bonus is higher (AD on a tie) ----
        private readonly object _adaptiveSource = new();
        private bool _updatingAdaptive;

        private void BindAdaptiveForce() {
            foreach (StatType type in new[] { StatType.AdaptiveForce, StatType.PhysicalAttack, StatType.MagicAttack })
                Stats.Get(type).Changed += _ => UpdateAdaptiveForce();
            UpdateAdaptiveForce();
        }

        private void UpdateAdaptiveForce() {
            if (_updatingAdaptive) return;
            _updatingAdaptive = true;
            Stat ad = Stats.Get(StatType.PhysicalAttack), ap = Stats.Get(StatType.MagicAttack);
            ad.RemoveAllFromSource(_adaptiveSource);
            ap.RemoveAllFromSource(_adaptiveSource);
            float force = Stats.Value(StatType.AdaptiveForce);
            if (force > 0f) {
                bool toAp = BonusStat(StatType.MagicAttack) > BonusStat(StatType.PhysicalAttack);
                (toAp ? ap : ad).AddModifier(new StatModifier(toAp ? force : force * 0.6f, ModifierType.Flat, _adaptiveSource));
            }
            _updatingAdaptive = false;
        }

        /// <summary>Where the adaptive force goes now (UI).</summary>
        public bool AdaptiveForceIsMagic => BonusStat(StatType.MagicAttack) > BonusStat(StatType.PhysicalAttack);

        private void Awake() {
            _rigidbody = GetComponent<Rigidbody>();
            Status = new StatusEffects(Stats, Buffs, info => TakeDamage(info));
            Status.Changed += OnStatusChanged;
            Passives = new PassiveController(this);
            Passives.LootDropped += OnPassiveLoot;
        }

        // Only the player's passives drop loot (augments, items, §9.5)
        private void OnPassiveLoot(LootKind kind, Vector3 position) {
            if (IsPlayer) _eventBus.Publish(new LootDropRequestedEvent(kind, position));
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
            _carriedItems.Clear();
            IsElite = false;
            _rewardMultiplier = 1f;
            if (visual != null) visual.SetElite(false);
            Passives.Clear();
            Capabilities.Clear();
            Buffs.Clear();
            Status.Clear();
            ApplyBody(definition);

            // Order matters (§12): stats first, everything else reads them.
            Stats.Init(definition.BaseStats);
            Level = new CharacterLevel(Stats, definition.GrowthPerLevel, _experienceCurve);
            BindAdaptiveForce();
            Health = new Health(Stats.Get(StatType.MaxHealth));
            Health.Died += OnDied;
            Mana = new ManaPool(Stats.Get(StatType.MaxMana));
            if (movement != null) movement.Init(Stats.Get(StatType.MoveSpeed));
            if (healthBar != null) healthBar.Bind(this);
            SetBodyActive(true);

            Level.LevelChanged += Events.RaiseLevelUp;
            Level.LevelChanged += _ => PlayRing(transform.position, 1.6f, new Color(1f, 0.85f, 0.35f, 0.9f));   // level up flash
            Passives.Add(definition.Passive, definition);   // the character's own passive (§8.1)
            if (IsPlayer) BindPlayerToBus();
            Initialized?.Invoke(this);
        }

        // TODO(phase 6): regeneration of hundreds of enemies → one central tick.
        private void Update() {
            if (!IsAlive) return;
            Buffs.Tick(Time.deltaTime);
            Status.Tick(Time.deltaTime);
            if (!IsAlive) return;   // killed by a damage over time
            Health.Tick(Time.deltaTime);
            Passives.Tick();
            if (IsPlayer && Time.unscaledTime >= _nextBuffs) PublishBuffs();
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

            // Damage modifiers of the passives (§8.1): the attacker's, then the target's own
            float scale = (info.Source != null ? info.Source.Passives.OutgoingMultiplier(this, info) : 1f) * Passives.IncomingMultiplier;
            DamageInfo hit = scale != 1f ? info.WithAmount(info.RawAmount * scale) : info;
            float final = DamageCalculator.Mitigate(hit, Stats.Value(StatType.Armor), Stats.Value(StatType.MagicResist));
            float taken = Health.TakeDamage(final);

            _eventBus.Publish(new DamageDealtEvent(HeadPosition, taken, info.Type, info.IsCrit, IsPlayer));
            if (taken > 0f && visual != null) visual.PlayHit();
            Damaged?.Invoke(this, taken);
            Events.RaiseDamageTaken(taken, info.Source);
            if (info.Source != null) {
                info.Source.OnDamageDealt(taken, info.IsBasicAttack);
                info.Source.Events.RaiseDamageDealt(this, taken, info);
                if (!IsAlive) info.Source.Events.RaiseKill(this);
            }
            return taken;
        }

        /// <summary>Applies a status (§5.7). <paramref name="source"/> identifies the effect (same source = refresh).</summary>
        public void ApplyStatus(in StatusApplication status, Character applier, object source) {
            if (IsAlive) Status.Apply(status, applier, source);
        }

        /// <summary>Heals, scaled by the producer's HealShieldPower (§6). Returns the HP actually healed.</summary>
        public float ReceiveHeal(float amount, Character producer) {
            if (!IsAlive) return 0f;
            return Health.Heal(amount * HealShieldMultiplier(producer));
        }

        /// <summary>Shield for <paramref name="duration"/> s, scaled by the producer's HealShieldPower (§6).</summary>
        public void ReceiveShield(float amount, float duration, Character producer) {
            if (IsAlive) Health.AddShield(amount * HealShieldMultiplier(producer), duration);
        }

        private static float HealShieldMultiplier(Character producer) =>
            producer != null ? producer.Stats.Value(StatType.HealShieldPower) : 1f;

        // Movement follows the CC; the look shows it
        private void OnStatusChanged() {
            if (movement != null) movement.Immobilized = !Status.CanMove;
            if (visual != null) visual.SetStatus(Status);
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
            Status.Clear();
            if (visual != null) visual.PlayDeath();   // before the body goes away
            SetBodyActive(false);

            if (Definition is EnemyDefinition enemy)
                _eventBus.Publish(new EnemyKilledEvent(Mathf.RoundToInt(enemy.XpReward * _rewardMultiplier),
                                                       Mathf.RoundToInt(enemy.GoldReward * _rewardMultiplier), transform.position));
            if (IsPlayer) _eventBus.Publish(new PlayerDiedEvent());

            Died?.Invoke(this);
        }

        // A dead body is invisible, cannot be hit or clicked, and does not fall through the floor.
        private void SetBodyActive(bool active) {
            if (visual != null) visual.gameObject.SetActive(active);
            if (shadow != null) shadow.gameObject.SetActive(active);
            if (body != null) body.enabled = active;
            if (_rigidbody != null) _rigidbody.isKinematic = !active;
        }

        // Root pivot at the feet: the box stands on the ground, the agent matches it.
        private void ApplyBody(CharacterDefinition definition) {
            if (visual != null) visual.SetLook(definition.Sprite, definition.Animations);

            Vector2 size = definition.BodySize;
            if (shadow != null) shadow.localScale = new Vector3(size.x * 1.7f, size.x * 1.1f, 1f);
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
            _busSubscriptions.Add(_eventBus.Subscribe<RunStateChangedEvent>(e => {
                if (e.To == Features.Run.RunState.Reward) Passives.OnWaveEnded();
            }));

            _busSubscriptions.Add(_eventBus.Subscribe<XpGrantedEvent>(e => Level.AddXp(e.Amount)));

            CharacterLevel level = Level;
            level.XpChanged += PublishXp;
            level.LevelChanged += _ => _eventBus.Publish(new LevelUpEvent(level.Level, level.SkillPoints, level.OverchargePoints));

            Health health = Health;
            health.Changed += (current, max) => _eventBus.Publish(new PlayerHealthChangedEvent(current, max, health.Shield));
            ManaPool mana = Mana;
            mana.Changed += (current, max) => _eventBus.Publish(new PlayerManaChangedEvent(current, max));

            foreach (Stat stat in Stats.Stats.Values) {
                StatType type = stat.Type;
                stat.Changed += value => _eventBus.Publish(new StatChangedEvent(type, value));
            }

            _eventBus.Publish(new PlayerChampionEvent(Definition.DisplayName, Definition.Portrait, Definition.Passive, Definition.RecommendedItems));
            _lastBuffs = "?";   // the buff bar starts from a fresh snapshot

            // Initial snapshot for whoever displays it
            foreach (Stat stat in Stats.Stats.Values) _eventBus.Publish(new StatChangedEvent(stat.Type, stat.Value));
            _eventBus.Publish(new PlayerHealthChangedEvent(health.Current, health.Max, health.Shield));
            _eventBus.Publish(new PlayerManaChangedEvent(mana.Current, mana.Max));
            PublishXp();
        }

        // HUD buff bar: passives with stacks or a charge (§7.9), only when that changes
        private float _nextBuffs;
        private string _lastBuffs = "";

        private void PublishBuffs() {
            _nextBuffs = Time.unscaledTime + 0.2f;
            var buffs = new System.Collections.Generic.List<PlayerBuff>();
            var key = new System.Text.StringBuilder();
            foreach (Passive p in Passives.All) {
                if (p.Stacks <= 0 && !p.IsCharged) continue;
                buffs.Add(new PlayerBuff(p.Definition.DisplayName, p.Definition.Description, p.Definition.Icon, p.Stacks, p.IsCharged));
                key.Append(p.Definition.name).Append(p.Stacks).Append(p.IsCharged ? '*' : '-');
            }
            string k = key.ToString();
            if (k == _lastBuffs) return;
            _lastBuffs = k;
            _eventBus.Publish(new PlayerBuffsEvent(buffs.ToArray()));
        }

        private void PublishXp() => _eventBus.Publish(new XpChangedEvent(Level.Level, Level.Xp, Level.XpToNext));
    }
}
