using Features.Events;
using Systems.EventBusses;
using Systems.Inputs.Events;
using UnityEngine;
using VContainer;

namespace Features.CameraControl {
    /// <summary>
    /// MOBA-style camera. Locked: follows <see cref="target"/>. Unlocked: free, scrolled by moving
    /// the cursor to the screen edges. The angle never changes: the rotation is derived once from
    /// the offset (no LookAt, which tilted the camera while it lagged behind the character). Space / middle mouse toggles the lock (CameraLockEvent),
    /// the wheel zooms (ZoomEvent). Tuning comes from <see cref="CameraSettings"/>.
    /// The target is the player, received through PlayerSpawnedEvent.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour {
        [SerializeField] public Transform target;

        private CameraSettings _settings;
        private IEventBus _eventBus;
        private Camera _cam;
        private Vector3 _currentOffset;
        private float _currentZoom;
        private Vector2 _mousePosition;
        private Vector3 _scrollVelocity;
        private bool _isLocked;
        private readonly DisposableBag _subscriptions = new();

        public bool IsLocked => _isLocked;

        // Everything is set up at injection time: the player is spawned (PlayerSpawnedEvent) by an
        // entry point that can run before this component's Start.
        [Inject]
        public void Construct(IEventBus eventBus, CameraSettings settings) {
            _eventBus = eventBus;
            _settings = settings;
            _cam = GetComponent<Camera>();

            _currentOffset = _settings.Offset;
            _currentZoom = _settings.Offset.magnitude;
            _isLocked = _settings.StartLocked;
            transform.rotation = Quaternion.LookRotation(-_settings.Offset);

            _subscriptions.Add(_eventBus.Subscribe<CameraLockEvent>(_ => ToggleLock()));
            _subscriptions.Add(_eventBus.Subscribe<ZoomEvent>(OnZoom));
            _subscriptions.Add(_eventBus.Subscribe<PointerPositionEvent>(OnPointerPosition));
            _subscriptions.Add(_eventBus.Subscribe<PlayerSpawnedEvent>(e => SetTarget(e.Player)));
        }

        private void OnDestroy() => _subscriptions.Dispose();

        private void Update() {
            if (target == null) return;
            UpdateScrollVelocity();
        }

        private void LateUpdate() {
            if (target == null) return;

            if (_isLocked) {
                Vector3 targetPosition = target.position + _currentOffset;
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * _settings.FollowSpeed);
            }
            else {
                transform.position += _scrollVelocity * Time.deltaTime;
            }
        }

        // Snaps on the new target instead of sliding across the map to it.
        public void SetTarget(Transform newTarget) {
            target = newTarget;
            if (target != null) transform.position = target.position + _currentOffset;
        }

        public void ToggleLock() => _isLocked = !_isLocked;

        private void OnZoom(ZoomEvent evt) {
            if (Mathf.Abs(evt.Delta) <= 0.01f) return;
            _currentZoom = Mathf.Clamp(_currentZoom - evt.Delta * _settings.ZoomSpeed, _settings.MinZoom, _settings.MaxZoom);
            _currentOffset = _currentOffset.normalized * _currentZoom;
        }

        private void OnPointerPosition(PointerPositionEvent evt) => _mousePosition = evt.Position;

        private void UpdateScrollVelocity() {
            Vector3 targetVelocity = Vector3.zero;

            if (_settings.EdgeScrollingEnabled && !_isLocked && IsPointerInsideWindow()) {
                // -1..1 per axis, proportional to how deep the cursor is in the edge zone
                float x = EdgeInfluence(_mousePosition.x, Screen.width);
                float y = EdgeInfluence(_mousePosition.y, Screen.height);

                // Camera axes flattened onto the ground plane
                Vector3 camForward = Vector3.ProjectOnPlane(_cam.transform.forward, Vector3.up).normalized;
                Vector3 camRight = Vector3.ProjectOnPlane(_cam.transform.right, Vector3.up).normalized;

                targetVelocity = (camRight * x + camForward * y) * _settings.EdgeScrollSpeed;
            }

            // Smoothed so starts, stops and direction changes feel fluid
            _scrollVelocity = Vector3.Lerp(_scrollVelocity, targetVelocity, Time.deltaTime * _settings.ScrollSmoothing);
            if (_scrollVelocity.sqrMagnitude < 0.0001f) _scrollVelocity = Vector3.zero;
        }

        private float EdgeInfluence(float position, float screenSize) {
            float edge = _settings.EdgeSize;
            if (position <= edge) return -Mathf.Clamp01((edge - position) / edge);
            if (position >= screenSize - edge) return Mathf.Clamp01((position - (screenSize - edge)) / edge);
            return 0f;
        }

        private bool IsPointerInsideWindow() {
            return _mousePosition.x >= 0 && _mousePosition.x <= Screen.width
                && _mousePosition.y >= 0 && _mousePosition.y <= Screen.height;
        }
    }
}
