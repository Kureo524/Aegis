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
    /// (or the HUD "+" button, ElementLevelUpRequestedEvent) spends a skill point on an element.
    /// Resolves the selection to a spell and its mana cost, live, for the HUD.
    /// R casts it (§7.4): checks (spell, cooldown, mana) → aim → left click → mana paid, shared
    /// elemental cooldown (CooldownByStacks or CooldownOverride, reduced by AbilityHaste), selection
    /// cleared → SpellCaster. Elements added while aiming re-resolve the aimed spell; T cancels the aim.
    /// Player prefab variant only: enemies cast without elements (§9.6).
    /// </summary>
    public class ElementCaster : MonoBehaviour {
        [SerializeField] private Character owner;
        [SerializeField] private SpellTargeting targeting;
        [SerializeField] private SpellCaster caster;

        private IEventBus _eventBus;
        private SpellSettings _settings;
        private readonly DisposableBag _subscriptions = new();
        private float _readyAt;

        public ElementSelection Selection { get; private set; }
        public ElementLevels Levels { get; private set; }
        public SpellResolver Resolver { get; private set; }
        public SpellDefinition ResolvedSpell => Selection == null ? null : Resolver.Resolve(Selection.Key);
        public float CooldownRemaining => Mathf.Max(0f, _readyAt - Time.time);
        private bool AimingElemental => targeting.IsAiming && targeting.Mode == AimMode.Spell;
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
            _readyAt = 0f;
            CharacterDefinition def = character.Definition;
            if (!def.HasElements) {
                Selection = null;
                return;
            }

            Selection = new ElementSelection(_settings.MaxTotalStacks, _settings.MaxStacksPerElement);
            Levels = new ElementLevels(character.Level, def.ElementUnlockLevels, _settings.MaxElementLevel);
            Resolver = new SpellResolver(def.SpellBook, def.Elements);

            Selection.Changed += PublishSelection;
            Selection.Changed += OnSelectionChangedWhileAiming;
            Levels.Changed += PublishLevels;
            character.Level.SkillPointsChanged += PublishLevels;
            character.Stats.Get(StatType.ManaCostReduction).Changed += _ => PublishSelection();

            _subscriptions.Add(_eventBus.Subscribe<ElementKeyEvent>(e => {
                if (e.LevelUp) LevelUp(e.Slot);
                else AddStack(e.Slot);
            }));
            _subscriptions.Add(_eventBus.Subscribe<ElementLevelUpRequestedEvent>(e => LevelUp(e.Slot)));
            _subscriptions.Add(_eventBus.Subscribe<ResetElementsEvent>(_ => {
                if (AimingElemental) targeting.Cancel();   // T also cancels an R aim (§7.1)
                Selection.Clear();
            }));
            _subscriptions.Add(_eventBus.Subscribe<CastSpellEvent>(e => {
                if (e.Released) targeting.Release(AimMode.Spell);
                else TryCast();
            }));

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

        /// <summary>R: checks, then aims (or casts at once: Self spell, quick cast). Releasing R casts in the "with indicator" mode.</summary>
        public bool TryCast() {
            if (Selection == null || !owner.IsAlive) return false;
            if (Selection.IsEmpty) return Feedback("Aucun élément");
            SpellDefinition spell = ResolvedSpell;
            if (spell == null) return Feedback("Aucun sort pour cette combinaison");
            if (CooldownRemaining > 0f) return Feedback($"Sorts en recharge ({CooldownRemaining:0.0} s)");
            if (!owner.Mana.CanSpend(CurrentManaCost)) return Feedback("Mana insuffisant");
            if (caster.IsCasting) return Feedback("Incantation en cours");

            if (spell.NeedsAim) targeting.Aim(spell, AimMode.Spell, Confirm);
            else Confirm(new CastContext { Origin = owner.transform.position, Direction = owner.transform.forward, TargetPoint = owner.transform.position });
            return true;
        }

        // Left click: the spell resolved NOW is cast (it may have changed while aiming).
        private void Confirm(CastContext ctx) {
            SpellDefinition spell = ResolvedSpell;
            if (spell == null) return;
            float cost = CurrentManaCost;
            if (caster.IsCasting) {
                Feedback("Incantation en cours");
                return;
            }
            if (!owner.Mana.TrySpend(cost)) {
                Feedback("Mana insuffisant");   // the selection is kept (§7.3)
                return;
            }

            int levels = SpellMath.SumOfLevelsAboveOne(spell, Slots, Levels.Level);
            caster.Cast(spell, ctx, levels);

            float baseCooldown = spell.CooldownOverride > 0f ? spell.CooldownOverride : _settings.CooldownByStacks(spell.TotalStacks);
            float cooldown = SpellMath.CooldownWithHaste(baseCooldown, owner.Stats.Value(StatType.AbilityHaste));
            _readyAt = Time.time + cooldown;
            _eventBus.Publish(new CooldownStartedEvent(CooldownSlot.Elemental, cooldown));

            if (_settings.ClearSelectionAfterCast) Selection.Clear();
        }

        private void OnSelectionChangedWhileAiming() {
            if (!AimingElemental || Selection.IsEmpty) return;
            SpellDefinition spell = ResolvedSpell;
            if (spell == null || !spell.NeedsAim) targeting.Cancel();
            else targeting.UpdateSpell(spell);
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
