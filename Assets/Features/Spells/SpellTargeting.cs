using System;
using Features.Characters;
using Features.Combat;
using Features.Controls;
using Features.Events;
using Features.Options;
using Systems.EventBusses;
using Systems.Inputs;
using Systems.Inputs.Events;
using UnityEngine;
using VContainer;

namespace Features.Spells {
    /// <summary>
    /// Aiming of the player's spells (design doc §7.4–7.5): shows the range and the spell shape under
    /// the cursor; left click confirms (builds the CastContext), right click / Escape cancel.
    /// Only one aim at a time: starting one publishes AimModeEvent, and any other aim (attack-move,
    /// another spell key) cancels this one. <see cref="Aim"/> follows the player's CastMode (UserOptions):
    /// Normal = aim then left click; QuickWithIndicator = aim while the key is held, cast on release
    /// (<see cref="Release"/>); Quick = cast at once towards the cursor. Player prefab variant.
    /// </summary>
    public class SpellTargeting : MonoBehaviour {
        [SerializeField] private Character owner;
        [SerializeField] private SpellIndicator indicator;
        [SerializeField] private RangeIndicator rangeIndicator;
        [Tooltip("Unit target spells: an enemy this close to the pointed ground point counts.")]
        [SerializeField] private float pickRadius = 0.8f;

        private IEventBus _eventBus;
        private UserOptions _options;
        private Camera _camera;
        private readonly DisposableBag _subscriptions = new();
        private readonly RaycastHit[] _rayHits = new RaycastHit[16];
        private readonly Collider[] _overlapHits = new Collider[16];
        private Plane _ground = new(Vector3.up, Vector3.zero);

        private SpellDefinition _spell;
        private int _slot = -1;
        private Action<CastContext> _onConfirm;
        private Action _onCancel;
        private bool _castOnRelease;
        private Vector2 _pointer;

        public bool IsAiming => _spell != null;
        public AimMode Mode => _spell != null ? AimMode.Ability : AimMode.None;
        /// <summary>Spell slot being aimed (AbilitySlots), -1 when not aiming.</summary>
        public int Slot => _spell != null ? _slot : -1;
        public SpellDefinition Spell => _spell;

        [Inject]
        public void Construct(IEventBus eventBus, UserOptions options, Camera mainCamera) {
            _eventBus = eventBus;
            _options = options;
            _camera = mainCamera;
        }

        private void Start() {
            _subscriptions.Add(_eventBus.Subscribe<PointerPositionEvent>(e => _pointer = e.Position));
            _subscriptions.Add(_eventBus.Subscribe<ConfirmEvent>(OnConfirm));
            _subscriptions.Add(_eventBus.Subscribe<CancelEvent>(_ => Cancel()));
            _subscriptions.Add(_eventBus.Subscribe<PointerCommandEvent>(_ => Cancel()));
            _subscriptions.Add(_eventBus.Subscribe<AimModeEvent>(e => {
                if (IsAiming && e.Mode != AimMode.None && (e.Mode != AimMode.Ability || e.Slot != _slot)) Stop(publish: false);   // another aim took over
            }));
        }

        private void OnDestroy() => _subscriptions.Dispose();

        /// <summary>A spell key pressed: aims <paramref name="spell"/> or casts it at once, depending on the cast mode.</summary>
        public void Aim(SpellDefinition spell, int slot, Action<CastContext> onConfirm) {
            CastMode castMode = _options != null ? _options.CastMode : CastMode.Normal;
            if (castMode == CastMode.Quick) {
                CastAtCursor(spell, onConfirm);
                return;
            }
            BeginAim(spell, slot, onConfirm);
            _castOnRelease = castMode == CastMode.QuickWithIndicator;
        }

        /// <summary>
        /// A spell key released: casts the aim started by that key in the "quick cast with indicator"
        /// mode. Nothing if the aim was already cast (left click), cancelled, or is a plain Normal aim.
        /// </summary>
        public void Release(int slot) {
            if (!IsAiming || _slot != slot || !_castOnRelease) return;
            if (!TryBuildContext(_spell, out CastContext ctx)) {
                Cancel();   // no valid target under the cursor: the key is up, the aim ends
                return;
            }
            Action<CastContext> onConfirm = _onConfirm;
            Stop(publish: true);
            onConfirm?.Invoke(ctx);
        }

        /// <summary>Quick cast: the context is built from the cursor right now, no aiming step.</summary>
        public bool CastAtCursor(SpellDefinition spell, Action<CastContext> onConfirm) {
            if (IsAiming) Cancel();
            if (!TryBuildContext(spell, out CastContext ctx)) return false;
            onConfirm?.Invoke(ctx);
            return true;
        }

        /// <summary>Starts aiming <paramref name="spell"/>; <paramref name="onConfirm"/> receives the cast context.</summary>
        public void BeginAim(SpellDefinition spell, int slot, Action<CastContext> onConfirm, Action onCancel = null) {
            if (IsAiming) Stop(publish: false);
            _slot = slot;
            _eventBus.Publish(new AimModeEvent(AimMode.Ability, slot));   // first: the others hide their own aim
            _spell = spell;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _castOnRelease = false;
            ShowRange();
        }

        public void Cancel() {
            if (!IsAiming) return;
            Action onCancel = _onCancel;
            Stop(publish: true);
            onCancel?.Invoke();
        }

        private void Stop(bool publish) {
            _spell = null;
            _onConfirm = null;
            _onCancel = null;
            _castOnRelease = false;
            indicator.Hide();
            rangeIndicator.Hide();
            if (publish) _eventBus.Publish(new AimModeEvent(AimMode.None));
        }

        private void ShowRange() {
            indicator.SetColor(_spell.Color);
            rangeIndicator.Show(_spell.Range);
        }

        private void LateUpdate() {
            if (!IsAiming) return;
            if (!owner.IsAlive) {
                Cancel();
                return;
            }

            CastContext ctx = BuildContext(_spell, out _);
            switch (_spell.Targeting) {
                case TargetingType.Skillshot:
                    indicator.ShowRectangle(ctx.Origin, ctx.Direction, _spell.Range, _spell.Radius);
                    break;
                case TargetingType.GroundPoint:
                    indicator.ShowCircle(ctx.TargetPoint, Mathf.Max(_spell.Radius, 0.4f));
                    break;
                case TargetingType.Cone:
                    indicator.ShowCone(ctx.Origin, ctx.Direction, _spell.Range, _spell.ConeAngle);
                    break;
                case TargetingType.UnitTarget:
                    if (ctx.TargetUnit != null) indicator.ShowCircle(ctx.TargetUnit.transform.position, ctx.TargetUnit.BodyRadius + 0.3f);
                    else indicator.Hide();
                    break;
                default:
                    indicator.Hide();
                    break;
            }
        }

        private void OnConfirm(ConfirmEvent e) {
            if (!IsAiming) return;
            _pointer = e.ScreenPosition;
            if (!TryBuildContext(_spell, out CastContext ctx)) return;   // keep aiming

            Action<CastContext> onConfirm = _onConfirm;
            Stop(publish: true);
            onConfirm?.Invoke(ctx);
        }

        // A unit target spell needs an enemy in range under the cursor (feedback otherwise).
        private bool TryBuildContext(SpellDefinition spell, out CastContext ctx) {
            ctx = BuildContext(spell, out bool targetOutOfRange);
            if (spell.Targeting != TargetingType.UnitTarget || ctx.TargetUnit != null) return true;
            _eventBus.Publish(new GameplayFeedbackEvent(targetOutOfRange ? "Cible hors de portée" : "Aucune cible"));
            return false;
        }

        private CastContext BuildContext(SpellDefinition spell, out bool targetOutOfRange) {
            targetOutOfRange = false;
            Vector3 origin = owner.transform.position;
            Ray ray = _camera.ScreenPointToRay(_pointer);
            Vector3 point = _ground.Raycast(ray, out float distance) ? ray.GetPoint(distance) : origin;
            Vector3 direction = SpellMath.FlatDirection(origin, point, owner.transform.forward);

            var ctx = new CastContext {
                Origin = origin,
                Direction = direction,
                TargetPoint = SpellMath.AimPoint(origin, point, direction, spell.Range, spell.FixedRange),
            };

            if (spell.Targeting == TargetingType.UnitTarget) {
                Character target = TargetPicker.EnemyUnderCursor(owner, ray, point, pickRadius, _rayHits, _overlapHits);
                if (target != null) {
                    Vector3 d = target.transform.position - origin;
                    d.y = 0f;
                    if (d.magnitude - target.BodyRadius <= spell.Range) ctx.TargetUnit = target;
                    else targetOutOfRange = true;
                }
            }
            return ctx;
        }
    }
}
