using Features.Characters;
using Features.Events;
using Features.Stats;
using Systems.EventBusses;
using Systems.Inputs.Events;
using UnityEngine;
using VContainer;

namespace Features.Spells {
    /// <summary>
    /// The player's spells, MOBA style (design doc §7): 6 slots (AbilitySlots) = the champion's Q W E R
    /// abilities and the D F summoner spells. Player prefab variant only (enemies cast through
    /// EnemyAI / PatternRunner).
    ///
    /// - Key pressed (AbilityKeyEvent): checks (learned, cooldown, mana, cast in progress, CC) then
    ///   aims through SpellTargeting (cast mode: normal, quick with indicator, quick) or casts at once
    ///   (Self). Key released: casts in the "quick with indicator" mode.
    /// - Confirm: mana paid (× (1 − ManaCostReduction)), SpellCaster.Cast at the ability's rank,
    ///   cooldown = the rank's cooldown × 100 / (100 + AbilityHaste) (summoner spells ignore haste).
    /// - Ctrl + key / HUD "+": spends a skill point on the ability (AbilityRanks).
    /// - CharacterEvents.CooldownRefund (cooldown refund on hit) shortens the 4 abilities' cooldowns.
    /// - A picked-up summoner spell (§9.5) fills D when it is empty, for one use.
    /// </summary>
    public class AbilityCaster : MonoBehaviour {
        [SerializeField] private Character owner;
        [SerializeField] private SpellTargeting targeting;
        [SerializeField] private SpellCaster caster;

        private IEventBus _eventBus;
        private SpellExecutor _executor;
        private readonly DisposableBag _subscriptions = new();
        private readonly SpellDefinition[] _spells = new SpellDefinition[AbilitySlots.Count];
        private readonly float[] _readyAt = new float[AbilitySlots.Count];
        private SpellDefinition _consumable;

        public AbilityRanks Ranks { get; private set; }
        /// <summary>A picked-up, single-use summoner spell is on D.</summary>
        public bool HoldsConsumable => _consumable != null;

        [Inject]
        public void Construct(IEventBus eventBus, SpellExecutor executor) {
            _eventBus = eventBus;
            _executor = executor;
        }

        private void Awake() => owner.Initialized += OnInitialized;

        private void OnDestroy() {
            owner.Initialized -= OnInitialized;
            owner.Events.CooldownRefund -= ReduceCooldowns;
            _subscriptions.Dispose();
        }

        private void OnInitialized(Character character) {
            _subscriptions.Dispose();
            owner.Events.CooldownRefund -= ReduceCooldowns;
            System.Array.Clear(_readyAt, 0, _readyAt.Length);
            _consumable = null;

            CharacterDefinition def = character.Definition;
            for (int i = 0; i < AbilitySlots.AbilityCount; i++)
                _spells[i] = i < def.Abilities.Count ? def.Abilities[i] : null;
            _spells[AbilitySlots.SummonerD] = def.SummonerD;
            _spells[AbilitySlots.SummonerF] = def.SummonerF;

            Ranks = new AbilityRanks(def.Abilities, character.Level);
            Ranks.Changed += Publish;
            character.Level.SkillPointsChanged += Publish;
            character.Level.LevelChanged += _ => Publish();
            owner.Events.CooldownRefund += ReduceCooldowns;

            _subscriptions.Add(_eventBus.Subscribe<AbilityKeyEvent>(OnKey));
            _subscriptions.Add(_eventBus.Subscribe<AbilityRankUpRequestedEvent>(e => RankUp(e.Slot)));
            _subscriptions.Add(_eventBus.Subscribe<SummonerSpellPickedEvent>(e => PickUp(e.Spell)));
            Publish();
        }

        /// <summary>What <paramref name="slot"/> casts now (D: the picked-up summoner spell first).</summary>
        public SpellDefinition Spell(int slot) {
            if (slot < 0 || slot >= AbilitySlots.Count) return null;
            return slot == AbilitySlots.SummonerD && _consumable != null ? _consumable : _spells[slot];
        }

        /// <summary>Rank of the spell in <paramref name="slot"/>: ability rank, 1 for a summoner spell, 0 when empty.</summary>
        public int Rank(int slot) {
            if (AbilitySlots.IsAbility(slot)) return Ranks?.Rank(slot) ?? 0;
            return Spell(slot) != null ? 1 : 0;
        }

        public float CooldownRemaining(int slot) =>
            slot == AbilitySlots.SummonerD && _consumable != null ? 0f : Mathf.Max(0f, _readyAt[slot] - Time.time);

        public float ManaCost(int slot) {
            SpellDefinition spell = Spell(slot);
            if (spell == null) return 0f;
            float reduction = Mathf.Clamp01(owner.Stats.Value(StatType.ManaCostReduction));
            return spell.ManaCost(Mathf.Max(1, Rank(slot))) * (1f - reduction);
        }

        private void OnKey(AbilityKeyEvent e) {
            if (e.Released) {
                targeting.Release(e.Slot);
                return;
            }
            if (e.LevelUp && AbilitySlots.IsAbility(e.Slot)) RankUp(e.Slot);
            else TryCast(e.Slot);
        }

        public bool RankUp(int slot) {
            if (Ranks == null) return false;
            if (Ranks.TryRankUp(slot)) return true;
            return Feedback(Ranks.RefusalReason(slot) ?? "Impossible");
        }

        /// <summary>Checks, then aims (or casts at once: Self spell, quick cast).</summary>
        public bool TryCast(int slot) {
            SpellDefinition spell = Spell(slot);
            if (spell == null || !owner.IsAlive) return slot == AbilitySlots.SummonerD && Feedback("Aucun sort d'invocateur");
            if (Rank(slot) <= 0) return Feedback($"{spell.DisplayName} : pas encore appris");
            if (CooldownRemaining(slot) > 0f) return Feedback($"{spell.DisplayName} en recharge ({CooldownRemaining(slot):0.0} s)");
            if (!owner.Mana.CanSpend(ManaCost(slot))) return Feedback("Mana insuffisant");
            if (caster.IsCasting) return Feedback("Incantation en cours");
            if (!owner.Status.CanCast) return Feedback(owner.Status.IsStunned ? "Étourdi" : "Réduit au silence");

            if (spell.NeedsAim) targeting.Aim(spell, slot, ctx => Confirm(slot, spell, ctx));
            else Confirm(slot, spell, new CastContext { Origin = owner.transform.position, Direction = owner.transform.forward, TargetPoint = owner.transform.position });
            return true;
        }

        private void Confirm(int slot, SpellDefinition spell, CastContext ctx) {
            if (Spell(slot) != spell || caster.IsCasting) {
                Feedback("Incantation en cours");
                return;
            }
            if (!owner.Status.CanCast) {   // stunned while aiming: nothing spent
                Feedback(owner.Status.IsStunned ? "Étourdi" : "Réduit au silence");
                return;
            }
            if (spell.Blink) {
                if (!_executor.TryGetBlinkPoint(ctx.Origin, ctx.TargetPoint, out Vector3 landing)) {
                    Feedback("Destination inaccessible");
                    return;
                }
                ctx.TargetPoint = landing;
            }
            float cost = ManaCost(slot);
            if (!owner.Mana.TrySpend(cost)) {
                Feedback("Mana insuffisant");
                return;
            }
            int rank = Mathf.Max(1, Rank(slot));
            if (!caster.Cast(spell, ctx, rank)) {
                owner.Mana.Restore(cost);
                return;
            }

            if (slot == AbilitySlots.SummonerD && _consumable != null) {   // used up: D is empty again
                _consumable = null;
                _eventBus.Publish(new SummonerSlotChangedEvent(false));
                Publish();
                return;
            }
            float cooldown = spell.Cooldown(rank);
            if (AbilitySlots.IsAbility(slot))   // + the haste of this very ability (Bread and Butter…)
                cooldown = SpellMath.CooldownWithHaste(cooldown, owner.Stats.Value(StatType.AbilityHaste) + owner.Passives.AbilityHaste(slot));
            _readyAt[slot] = Time.time + cooldown;
            _eventBus.Publish(new CooldownStartedEvent(slot, cooldown));
        }

        /// <summary>Shortens the cooldowns of the 4 abilities (cooldown refund on hit).</summary>
        public void ReduceCooldowns(float seconds) {
            for (int slot = 0; slot < AbilitySlots.AbilityCount; slot++) {
                if (_readyAt[slot] <= Time.time) continue;
                _readyAt[slot] -= seconds;
                _eventBus.Publish(new CooldownStartedEvent(slot, CooldownRemaining(slot)));
            }
        }

        public bool PickUp(SpellDefinition spell) {
            if (spell == null || _consumable != null || _spells[AbilitySlots.SummonerD] != null) return false;
            _consumable = spell;
            _eventBus.Publish(new SummonerSlotChangedEvent(true));
            _eventBus.Publish(new GameplayFeedbackEvent($"{spell.DisplayName} sur D (usage unique)"));
            Publish();
            return true;
        }

        private bool Feedback(string message) {
            _eventBus.Publish(new GameplayFeedbackEvent(message));
            return false;
        }

        private void Publish() {
            if (Ranks == null) return;
            var spells = new SpellDefinition[AbilitySlots.Count];
            var ranks = new int[AbilitySlots.Count];
            var canRankUp = new bool[AbilitySlots.Count];
            for (int slot = 0; slot < AbilitySlots.Count; slot++) {
                spells[slot] = Spell(slot);
                ranks[slot] = Rank(slot);
                canRankUp[slot] = AbilitySlots.IsAbility(slot) && Ranks.CanRankUp(slot);
            }
            _eventBus.Publish(new AbilitiesChangedEvent(spells, ranks, canRankUp, owner.Level.SkillPoints));
        }
    }
}
