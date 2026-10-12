using Features.Characters;
using Features.Events;
using Features.Stats;
using Systems.EventBusses;
using Systems.Inputs.Events;
using UnityEngine;
using VContainer;

namespace Features.Spells {
    /// <summary>
    /// The character's own ability on F (design doc §7.8): a spell outside the spell book, cooldown
    /// only (CooldownOverride, reduced by AbilityHaste), no mana, no element. Same aiming and casting
    /// as R (quick cast included). Flash for the test hero: blink of fixed length (FixedRange) towards
    /// the cursor, onto the farthest walkable spot of that line; refused only if there is none.
    /// A picked-up summoner spell (§9.5, SummonerSpellPickedEvent) takes F for one use, ignoring the
    /// signature's cooldown; then F is the signature again, its own cooldown untouched. No other
    /// summoner spell can be picked up meanwhile. Player prefab variant.
    /// </summary>
    public class SignatureAbility : MonoBehaviour {
        [SerializeField] private Character owner;
        [SerializeField] private SpellTargeting targeting;
        [SerializeField] private SpellCaster caster;

        private IEventBus _eventBus;
        private SpellExecutor _executor;
        private readonly DisposableBag _subscriptions = new();
        private float _readyAt;
        private SpellDefinition _consumable;

        public SpellDefinition Default => owner.Definition != null ? owner.Definition.SignatureAbility : null;
        /// <summary>What F casts now: the picked-up summoner spell, else the signature ability.</summary>
        public SpellDefinition Spell => _consumable != null ? _consumable : Default;
        public bool HoldsConsumable => _consumable != null;
        public float CooldownRemaining => _consumable != null ? 0f : Mathf.Max(0f, _readyAt - Time.time);

        [Inject]
        public void Construct(IEventBus eventBus, SpellExecutor executor) {
            _eventBus = eventBus;
            _executor = executor;
        }

        private void Awake() => owner.Initialized += OnInitialized;

        private void OnDestroy() {
            owner.Initialized -= OnInitialized;
            _subscriptions.Dispose();
        }

        private void OnInitialized(Character _) {
            _subscriptions.Dispose();
            _readyAt = 0f;
            _consumable = null;
            _subscriptions.Add(_eventBus.Subscribe<SummonerSpellPickedEvent>(e => PickUp(e.Spell)));
            if (Spell == null) return;
            _subscriptions.Add(_eventBus.Subscribe<SignatureEvent>(e => {
                if (e.Released) targeting.Release(AimMode.Signature);
                else TryUse();
            }));
            _eventBus.Publish(new SignatureAssignedEvent(Spell));
        }

        public bool PickUp(SpellDefinition spell) {
            if (spell == null || _consumable != null) return false;
            _consumable = spell;
            _eventBus.Publish(new SignatureAssignedEvent(spell));
            _eventBus.Publish(new CooldownStartedEvent(CooldownSlot.Signature, 0f));
            _eventBus.Publish(new SummonerSlotChangedEvent(true));
            _eventBus.Publish(new GameplayFeedbackEvent($"{spell.DisplayName} sur F (usage unique)"));
            return true;
        }

        public bool TryUse() {
            SpellDefinition spell = Spell;
            if (spell == null || !owner.IsAlive) return false;
            if (CooldownRemaining > 0f) return Feedback($"{spell.DisplayName} en recharge");
            if (caster.IsCasting) return Feedback("Incantation en cours");

            if (spell.NeedsAim) targeting.Aim(spell, AimMode.Signature, Confirm);
            else Confirm(new CastContext { Origin = owner.transform.position, Direction = owner.transform.forward, TargetPoint = owner.transform.position });
            return true;
        }

        private void Confirm(CastContext ctx) {
            SpellDefinition spell = Spell;
            if (spell.Blink) {
                if (!_executor.TryGetBlinkPoint(ctx.Origin, ctx.TargetPoint, out Vector3 landing)) {
                    Feedback("Destination inaccessible");
                    return;
                }
                ctx.TargetPoint = landing;
            }
            if (!caster.Cast(spell, ctx, 0)) return;

            if (_consumable != null) {   // used up: F is the signature again, with its own cooldown
                _consumable = null;
                _eventBus.Publish(new SignatureAssignedEvent(Default));
                _eventBus.Publish(new CooldownStartedEvent(CooldownSlot.Signature, CooldownRemaining));
                _eventBus.Publish(new SummonerSlotChangedEvent(false));
                return;
            }
            float cooldown = SpellMath.CooldownWithHaste(spell.CooldownOverride, owner.Stats.Value(StatType.AbilityHaste));
            _readyAt = Time.time + cooldown;
            _eventBus.Publish(new CooldownStartedEvent(CooldownSlot.Signature, cooldown));
        }

        private bool Feedback(string message) {
            _eventBus.Publish(new GameplayFeedbackEvent(message));
            return false;
        }
    }
}
