using System.Collections.Generic;
using Features.Characters;
using Features.Events;
using Features.Stats;
using Systems.EventBusses;
using Systems.Inputs.Events;
using UnityEngine;
using VContainer;

namespace Features.Spells {
    /// <summary>
    /// The player's element side (design doc §7): A/Z/E stack elements, T clears them, Ctrl+A/Z/E
    /// (or the HUD "+" button, ElementLevelUpRequestedEvent) spends a skill point on an element. Resolves the selection to a spell and its mana cost,
    /// live, for the HUD. Casting (R) comes with phase 5c.
    /// Player prefab variant only: enemies cast without elements (§9.6).
    /// </summary>
    public class ElementCaster : MonoBehaviour {
        [SerializeField] private Character owner;

        private IEventBus _eventBus;
        private SpellSettings _settings;
        private readonly DisposableBag _subscriptions = new();

        public ElementSelection Selection { get; private set; }
        public ElementLevels Levels { get; private set; }
        public SpellResolver Resolver { get; private set; }
        public SpellDefinition ResolvedSpell => Selection == null ? null : Resolver.Resolve(Selection.Key);
        private IReadOnlyList<ElementDefinition> Slots => owner.Definition.Elements;

        [Inject]
        public void Construct(IEventBus eventBus, SpellSettings settings) {
            _eventBus = eventBus;
            _settings = settings;
        }

        private void Awake() => owner.Initialized += OnInitialized;

        private void OnDestroy() {
            owner.Initialized -= OnInitialized;
            _subscriptions.Dispose();
        }

        private void OnInitialized(Character character) {
            _subscriptions.Dispose();
            CharacterDefinition def = character.Definition;
            if (!def.HasElements) {
                Selection = null;
                return;
            }

            Selection = new ElementSelection(_settings.MaxTotalStacks, _settings.MaxStacksPerElement);
            Levels = new ElementLevels(character.Level, def.ElementUnlockLevels, _settings.MaxElementLevel);
            Resolver = new SpellResolver(def.SpellBook, def.Elements);

            Selection.Changed += PublishSelection;
            Levels.Changed += PublishLevels;
            character.Level.SkillPointsChanged += PublishLevels;
            character.Stats.Get(StatType.ManaCostReduction).Changed += _ => PublishSelection();

            _subscriptions.Add(_eventBus.Subscribe<ElementKeyEvent>(e => {
                if (e.LevelUp) LevelUp(e.Slot);
                else AddStack(e.Slot);
            }));
            _subscriptions.Add(_eventBus.Subscribe<ElementLevelUpRequestedEvent>(e => LevelUp(e.Slot)));
            _subscriptions.Add(_eventBus.Subscribe<ResetElementsEvent>(_ => Selection.Clear()));

            _eventBus.Publish(new PlayerElementsEvent(Slots[0], Slots[1], Slots[2]));
            PublishLevels();
            PublishSelection();
        }

        public bool AddStack(int slot) {
            if (Selection == null || !owner.IsAlive) return false;
            if (!Levels.IsUnlocked(slot)) return Feedback("Élément non débloqué");
            if (!Selection.Add(slot)) return Feedback("Sélection pleine");
            return true;
        }

        public bool LevelUp(int slot) {
            if (Levels == null) return false;
            if (Levels.TryLevelUp(slot)) return true;
            if (owner.Level.SkillPoints <= 0) return Feedback("Aucun point de compétence");
            if (Levels.Level(slot) >= _settings.MaxElementLevel) return Feedback("Niveau maximum");
            return Feedback($"Prochain élément au niveau {Levels.NextUnlockLevel}");
        }

        public float CurrentManaCost =>
            Selection == null ? 0f
                : SpellResolver.ManaCost(Selection, Slots, _settings, owner.Stats.Value(StatType.ManaCostReduction));

        private bool Feedback(string message) {
            _eventBus.Publish(new GameplayFeedbackEvent(message));
            return false;
        }

        private void PublishSelection() {
            _eventBus.Publish(new ElementSelectionChangedEvent(
                Selection.Stacks(0), Selection.Stacks(1), Selection.Stacks(2), Selection.Total,
                ResolvedSpell, CurrentManaCost));
        }

        private void PublishLevels() {
            _eventBus.Publish(new ElementLevelsChangedEvent(
                Levels.Level(0), Levels.Level(1), Levels.Level(2),
                Levels.CanLevelUp(0), Levels.CanLevelUp(1), Levels.CanLevelUp(2),
                owner.Level.SkillPoints, Levels.NextUnlockLevel));
        }
    }
}
