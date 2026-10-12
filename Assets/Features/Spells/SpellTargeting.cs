using System;
using Features.Characters;
using Features.Combat;
using Features.Controls;
using Features.Events;
using Systems.EventBusses;
using Systems.Inputs.Events;
using UnityEngine;
using VContainer;

namespace Features.Spells {
    /// <summary>
    /// Aiming of the player's spells (design doc §7.4–7.5): shows the range and the spell shape under
    /// the cursor; left click confirms (builds the CastContext), right click / Escape cancel.
    /// Only one aim at a time: starting one publishes AimModeEvent, and any other aim mode (Q
    /// attack-move…) cancels this one. Player prefab variant.
    /// </summary>
    public class SpellTargeting : MonoBehaviour {
        [SerializeField] private Character owner;
        [SerializeField] private SpellIndicator indicator;
        [SerializeField] private RangeIndicator rangeIndicator;
        [Tooltip("Unit target spells: an enemy this close to the pointed ground point counts.")]
        [SerializeField] private float pickRadius = 0.8f;

        private IEventBus _eventBus;
        private Camera _camera;
        private readonly DisposableBag _subscriptions = new();
        private readonly RaycastHit[] _rayHits = new RaycastHit[16];
        private readonly Collider[] _overlapHits = new Collider[16];
        private Plane _ground = new(Vector3.up, Vector3.zero);

        private SpellDefinition _spell;
        private AimMode _mode;
        private Action<CastContext> _onConfirm;
        private Action _onCancel;
        private Vector2 _pointer;

        public bool IsAiming => _spell != null;
        public AimMode Mode => _spell != null ? _mode : AimMode.None;
        public SpellDefinition Spell => _spell;

        [Inject]
        public void Construct(IEventBus eventBus, Camera mainCamera) {
            _eventBus = eventBus;
            _camera = mainCamera;
        }

        private void Start() {
            _subscriptions.Add(_eventBus.Subscribe<PointerPositionEvent>(e => _pointer = e.Position));
            _subscriptions.Add(_eventBus.Subscribe<ConfirmEvent>(OnConfirm));
            _subscriptions.Add(_eventBus.Subscribe<CancelEvent>(_ => Cancel()));
            _subscriptions.Add(_eventBus.Subscribe<PointerCommandEvent>(_ => Cancel()));
            _subscriptions.Add(_eventBus.Subscribe<AimModeEvent>(e => {
                if (IsAiming && e.Mode != AimMode.None && e.Mode != _mode) Stop(publish: false);   // another aim took over
            }));
        }

        private void OnDestroy() => _subscriptions.Dispose();

        /// <summary>Starts aiming <paramref name="spell"/>; <paramref name="onConfirm"/> receives the cast context.</summary>
        public void BeginAim(SpellDefinition spell, AimMode mode, Action<CastContext> onConfirm, Action onCancel = null) {
            if (IsAiming) Stop(publish: false);
            _mode = mode;
            _eventBus.Publish(new AimModeEvent(mode));   // first: the others hide their own aim
            _spell = spell;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            ShowRange();
        }

        /// <summary>The aimed spell changed (elements added while aiming): re-resolved live (§7.4).</summary>
        public void UpdateSpell(SpellDefinition spell) {
            if (!IsAiming || spell == null) return;
            _spell = spell;
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

            CastContext ctx = BuildContext(out _);
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
            CastContext ctx = BuildContext(out bool targetOutOfRange);

            if (_spell.Targeting == TargetingType.UnitTarget && ctx.TargetUnit == null) {
                _eventBus.Publish(new GameplayFeedbackEvent(targetOutOfRange ? "Cible hors de portée" : "Aucune cible"));
                return;   // keep aiming
            }

            Action<CastContext> onConfirm = _onConfirm;
            Stop(publish: true);
            onConfirm?.Invoke(ctx);
        }

        private CastContext BuildContext(out bool targetOutOfRange) {
            targetOutOfRange = false;
            Vector3 origin = owner.transform.position;
            Ray ray = _camera.ScreenPointToRay(_pointer);
            Vector3 point = _ground.Raycast(ray, out float distance) ? ray.GetPoint(distance) : origin;

            Vector3 direction = SpellMath.FlatDirection(origin, point, owner.transform.forward);

            var ctx = new CastContext {
                Origin = origin,
                Direction = direction,
                TargetPoint = SpellMath.AimPoint(origin, point, direction, _spell.Range, _spell.FixedRange),
            };

            if (_spell.Targeting == TargetingType.UnitTarget) {
                Character target = TargetPicker.EnemyUnderCursor(owner, ray, point, pickRadius, _rayHits, _overlapHits);
                if (target != null) {
                    Vector3 d = target.transform.position - origin;
                    d.y = 0f;
                    if (d.magnitude - target.BodyRadius <= _spell.Range) ctx.TargetUnit = target;
                    else targetOutOfRange = true;
                }
            }
            return ctx;
        }
    }
}
