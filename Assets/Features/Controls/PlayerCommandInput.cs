using Features.Characters;
using Features.Combat;
using Features.Events;
using Features.Movement;
using Features.Spells;
using Features.Stats;
using Systems.EventBusses;
using Systems.Inputs.Events;
using Systems.UIManagement;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Features.Controls {
    /// <summary>
    /// Turns the player's pointer commands into orders for the character (design doc §4).
    /// Right click on an enemy: attack it (chase, then auto-attack). Right click on the ground:
    /// move there, which cancels the attack (a windup in progress is lost), MOBA style.
    /// Hold Q and left click: attack-move (§4.6) to the clicked point, attacking first the enemy
    /// closest to the click (if any within attackMoveSelectRadius). Aiming lasts while Q is held
    /// (the attack range is shown, each click gives an order); releasing Q, right click or Escape
    /// end it without giving any order.
    /// Lives on the Player prefab variant.
    /// </summary>
    public class PlayerCommandInput : MonoBehaviour {
        [SerializeField] public Character character;
        [SerializeField] public CharacterMovement characterMovement;
        [SerializeField] public BasicAttacker attacker;
        [Tooltip("Thin sprites are hard to click: an enemy this close to the clicked ground point counts.")]
        [SerializeField] public float pickRadius = 0.8f;
        [Tooltip("Attack-move: an enemy this close to the clicked point becomes the first target.")]
        [SerializeField] public float attackMoveSelectRadius = 2f;
        [SerializeField] public RangeIndicator rangeIndicator;
        [SerializeField] public SpellCaster spellCaster;
        [SerializeField] public GameObject clickMarkerPrefab;   // must carry a ClickMarker

        private IEventBus _eventBus;
        private IObjectResolver _resolver;
        private Camera _camera;
        private readonly DisposableBag _subscriptions = new();
        private bool _aimingAttackMove;
        private bool _otherAimActive;   // R / F aiming: right click belongs to it
        private ClickMarker _currentMarker;
        private Plane _groundPlane;
        private readonly RaycastHit[] _rayHits = new RaycastHit[16];
        private readonly Collider[] _overlapHits = new Collider[16];

        private IPointerOverUi _ui;

        [Inject]
        public void Construct(IEventBus eventBus, IObjectResolver resolver, Camera mainCamera, IPointerOverUi ui) {
            _eventBus = eventBus;
            _ui = ui;
            _resolver = resolver;
            _camera = mainCamera;
        }

        private void Awake() {
            _groundPlane = new Plane(Vector3.up, Vector3.zero);
        }

        private void Start() {
            _subscriptions.Add(_eventBus.Subscribe<PointerCommandEvent>(OnPointerCommand));
            _subscriptions.Add(_eventBus.Subscribe<AttackMoveEvent>(e => {
                if (e.Pressed) StartAimingAttackMove();
                else StopAiming();
            }));
            _subscriptions.Add(_eventBus.Subscribe<ConfirmEvent>(OnConfirm));
            _subscriptions.Add(_eventBus.Subscribe<CancelEvent>(_ => StopAiming()));
            _subscriptions.Add(_eventBus.Subscribe<AimModeEvent>(e => {
                _otherAimActive = e.Mode == AimMode.Ability;
                if (_otherAimActive && _aimingAttackMove) StopAiming();
            }));
        }

        private void OnDestroy() => _subscriptions.Dispose();

        private void StartAimingAttackMove() {
            if (!character.IsAlive) return;
            _eventBus.Publish(new AimModeEvent(AimMode.AttackMove));   // cancels a spell aim
            _aimingAttackMove = true;
            if (rangeIndicator != null) rangeIndicator.Show(character.Stats.Value(StatType.AttackRange));
        }

        private void StopAiming() {
            _aimingAttackMove = false;
            if (rangeIndicator != null) rangeIndicator.Hide();
        }

        private void OnConfirm(ConfirmEvent evt) {
            if (!_aimingAttackMove || OrdersBlocked) return;
            if (_ui.IsPointerOverUi(evt.ScreenPosition)) return;
            if (!TryGetGroundPoint(evt.ScreenPosition, out Ray _, out Vector3 point)) return;

            Character preferred = ClosestEnemy(point, attackMoveSelectRadius);
            if (attacker.AttackMove(point, preferred)) ShowMarker(point, new Color(1f, 0.55f, 0.1f));
        }

        private bool TryGetGroundPoint(Vector2 screenPosition, out Ray ray, out Vector3 point) {
            ray = _camera.ScreenPointToRay(screenPosition);
            point = default;
            if (!_groundPlane.Raycast(ray, out float distance)) return false;
            point = ray.GetPoint(distance);
            return true;
        }

        private void OnPointerCommand(PointerCommandEvent evt) {
            if (_ui.IsPointerOverUi(evt.ScreenPosition)) return;   // a click on the UI is not an order

            // Right click while aiming only cancels the aim (§4.6); during an R / F aim it belongs to it
            if (_aimingAttackMove) {
                StopAiming();
                return;
            }
            if (_otherAimActive || OrdersBlocked) return;

            if (!TryGetGroundPoint(evt.ScreenPosition, out Ray ray, out Vector3 worldPoint)) return;

            Character enemy = PickEnemy(ray, worldPoint);
            if (enemy != null && attacker.Attack(enemy)) {
                ShowMarker(enemy.transform.position, Color.red);
                return;
            }

            attacker.Cancel();
            characterMovement.SetDestination(worldPoint);
            ShowMarker(worldPoint, Color.green);
        }

        // A cast in progress (without CanMoveWhileCasting) freezes the character's orders
        private bool OrdersBlocked => spellCaster != null && spellCaster.BlocksOrders;

        // Enemy under the cursor, else the closest one within pickRadius of the clicked point (§4.3).
        private Character PickEnemy(Ray ray, Vector3 groundPoint) =>
            TargetPicker.EnemyUnderCursor(character, ray, groundPoint, pickRadius, _rayHits, _overlapHits);

        private Character ClosestEnemy(Vector3 groundPoint, float radius) =>
            TargetPicker.ClosestEnemy(character, groundPoint, radius, _overlapHits);

        private void ShowMarker(Vector3 position, Color color) {
            if (clickMarkerPrefab == null) return;
            if (_currentMarker != null) Destroy(_currentMarker.gameObject);

            _currentMarker = _resolver.Instantiate(clickMarkerPrefab, position, Quaternion.identity)
                                      .GetComponent<ClickMarker>();
            if (_currentMarker != null) _currentMarker.SetColor(color);
        }
    }
}
