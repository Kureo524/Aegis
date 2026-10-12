using System;
using Features.Events;
using Features.Movement;
using Features.Progression;
using Features.Stats;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.AI;
using VContainer;

namespace Features.Characters {
    /// <summary>
    /// Entry point of the generic character prefab (Assets/Prefabs/Characters/Character, design doc
    /// §2, §8; Player and later Enemy are variants of it). <see cref="Init"/>
    /// configures it from a <see cref="CharacterDefinition"/>; it can be called again to reuse the
    /// object (pooling, new run).
    ///
    /// Only the player character talks to the global bus (XP in, level/XP/stat changes out):
    /// hundreds of enemies must not flood it (§14.3).
    /// </summary>
    public class Character : MonoBehaviour {
        [SerializeField] private CharacterMovement movement;
        [SerializeField] private CharacterVisual visual;
        [SerializeField] private BoxCollider body;
        [SerializeField] private NavMeshAgent agent;

        private IEventBus _eventBus;
        private ExperienceCurve _experienceCurve;
        private readonly DisposableBag _busSubscriptions = new();

        public CharacterDefinition Definition { get; private set; }
        public StatController Stats { get; } = new();
        public CharacterLevel Level { get; private set; }
        public bool IsPlayer => Definition != null && Definition.Faction == Faction.Player;

        [Inject]
        public void Construct(IEventBus eventBus, ExperienceCurve experienceCurve) {
            _eventBus = eventBus;
            _experienceCurve = experienceCurve;
        }

        public void Init(CharacterDefinition definition) {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            _busSubscriptions.Dispose();

            Definition = definition;
            ApplyBody(definition);

            // Order matters (§12): stats first, everything else reads them.
            Stats.Init(definition.BaseStats);
            Level = new CharacterLevel(Stats, definition.GrowthPerLevel, _experienceCurve);
            if (movement != null) movement.Init(Stats.Get(StatType.MoveSpeed));

            if (IsPlayer) BindPlayerToBus();
        }

        private void OnDestroy() => _busSubscriptions.Dispose();

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

            foreach (Stat stat in Stats.Stats.Values) {
                StatType type = stat.Type;
                stat.Changed += value => _eventBus.Publish(new StatChangedEvent(type, value));
            }

            // Initial snapshot for whoever displays it
            foreach (Stat stat in Stats.Stats.Values) _eventBus.Publish(new StatChangedEvent(stat.Type, stat.Value));
            PublishXp();
        }

        private void PublishXp() => _eventBus.Publish(new XpChangedEvent(Level.Level, Level.Xp, Level.XpToNext));
    }
}
