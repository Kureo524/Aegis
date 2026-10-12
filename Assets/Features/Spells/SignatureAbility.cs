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
    /// as R. Flash for the test hero: blink to the cursor, refused if the spot is not walkable.
    /// Player prefab variant. (Picked-up summoner spells replacing it come with phase 8.)
    /// </summary>
    public class SignatureAbility : MonoBehaviour {
        [SerializeField] private Character owner;
        [SerializeField] private SpellTargeting targeting;
        [SerializeField] private SpellCaster caster;

        private IEventBus _eventBus;
        private SpellExecutor _executor;
        private readonly DisposableBag _subscriptions = new();
        private float _readyAt;

        public SpellDefinition Spell => owner.Definition != null ? owner.Definition.SignatureAbility : null;
        public float CooldownRemaining => Mathf.Max(0f, _readyAt - Time.time);

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
            if (Spell == null) return;
            _subscriptions.Add(_eventBus.Subscribe<SignatureEvent>(_ => TryUse()));
            _eventBus.Publish(new SignatureAssignedEvent(Spell));
        }

        public bool TryUse() {
            SpellDefinition spell = Spell;
            if (spell == null || !owner.IsAlive) return false;
            if (CooldownRemaining > 0f) return Feedback($"{spell.DisplayName} en recharge");
            if (caster.IsCasting) return Feedback("Incantation en cours");

            if (spell.NeedsAim) targeting.BeginAim(spell, AimMode.Signature, Confirm);
            else Confirm(new CastContext { Origin = owner.transform.position, Direction = owner.transform.forward, TargetPoint = owner.transform.position });
            return true;
        }

        private void Confirm(CastContext ctx) {
            SpellDefinition spell = Spell;
            if (spell.Blink && !_executor.TryGetBlinkPoint(ctx.TargetPoint, out _)) {
                Feedback("Destination inaccessible");
                return;
            }
            if (!caster.Cast(spell, ctx, 0)) return;

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
