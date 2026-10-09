using System;
using Features.Events;
using Systems.EventBusses;
using UnityEngine;
using VContainer;

namespace Systems.Characters {
    public class CameraController : MonoBehaviour {
        [SerializeField] public Transform target;
        [SerializeField] public Vector3 offset = new Vector3(0, 10, -8);
        [SerializeField] private float followSpeed = 10f;
        
        [Header("Edge Scrolling")]
        [SerializeField] private bool edgeScrollingEnabled = true;
        [SerializeField] private float edgeSize = 20f;
        [SerializeField] private float edgeScrollSpeed = 15f;
        [SerializeField] private float scrollSmoothing = 8f;
        
        [Header("Zoom")]
        [SerializeField] private float minZoom = 5f;
        [SerializeField] private float maxZoom = 20f;
        [SerializeField] private float zoomSpeed = 10f;
        
        [Header("Lock")]
        [SerializeField] private bool isLocked = true;
        
        private IEventBus _eventBus;
        private Camera _cam;
        private Vector3 _currentOffset;
        private float _currentZoom;
        private Vector2 _mousePosition;
        private Vector3 _scrollVelocity;
        private IDisposable _cameraLockSubscription;
        private IDisposable _zoomSubscription;
        private IDisposable _pointerPositionSubscription;
        
        public bool IsLocked => isLocked;
        
        [Inject]
        public void Construct(IEventBus eventBus) {
            _eventBus = eventBus;
        }

        private void Awake() {
            _cam = GetComponent<Camera>();
            _currentOffset = offset;
            _currentZoom = offset.magnitude;
        }

        private void Start() {
            if (_eventBus == null) return;
            _cameraLockSubscription = _eventBus.Subscribe<CameraLockEvent>(_ => ToggleLock());
            _zoomSubscription = _eventBus.Subscribe<ZoomEvent>(OnZoom);
            _pointerPositionSubscription = _eventBus.Subscribe<PointerPositionEvent>(OnPointerPosition);
        }

        private void OnDisable() {
            _cameraLockSubscription?.Dispose();
            _zoomSubscription?.Dispose();
            _pointerPositionSubscription?.Dispose();
        }

        private void Update() {
            if (target == null) return;
            UpdateScrollVelocity();
        }

        private void LateUpdate() {
            if (target == null) return;

            if (isLocked) {
                // Follow target (LoL style)
                Vector3 targetPosition = target.position + _currentOffset;
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSpeed);
                transform.LookAt(target.position);
            }
            else {
                // Free camera: apply the smoothed edge-scroll velocity
                transform.position += _scrollVelocity * Time.deltaTime;
            }
        }

        private void OnZoom(ZoomEvent evt) {
            if (Mathf.Abs(evt.Delta) > 0.01f) {
                _currentZoom = Mathf.Clamp(_currentZoom - evt.Delta * zoomSpeed, minZoom, maxZoom);
                _currentOffset = _currentOffset.normalized * _currentZoom;
            }
        }

        private void OnPointerPosition(PointerPositionEvent evt) {
            _mousePosition = evt.Position;
        }

        private void UpdateScrollVelocity() {
            Vector3 targetVelocity = Vector3.zero;

            if (edgeScrollingEnabled && !isLocked && IsPointerInsideWindow()) {
                // Normalized edge influence: -1..1 on each axis, smooth (not just -1/0/1)
                float x = EdgeInfluence(_mousePosition.x, Screen.width);
                float y = EdgeInfluence(_mousePosition.y, Screen.height);

                // Build the direction in camera space, then flatten it to the ground plane
                Vector3 camForward = Vector3.ProjectOnPlane(_cam.transform.forward, Vector3.up).normalized;
                Vector3 camRight = Vector3.ProjectOnPlane(_cam.transform.right, Vector3.up).normalized;

                targetVelocity = (camRight * x + camForward * y) * edgeScrollSpeed;
            }

            // Smooth the velocity so starts/stops and direction changes feel fluid
            _scrollVelocity = Vector3.Lerp(_scrollVelocity, targetVelocity, Time.deltaTime * scrollSmoothing);
            if (_scrollVelocity.sqrMagnitude < 0.0001f) _scrollVelocity = Vector3.zero;
        }

        // Returns -1..0..1 depending on how deep the cursor is inside an edge zone (0 = neutral)
        private float EdgeInfluence(float position, float screenSize) {
            if (position <= edgeSize)
                return Mathf.Clamp01((edgeSize - position) / edgeSize) * -1f;
            if (position >= screenSize - edgeSize)
                return Mathf.Clamp01((position - (screenSize - edgeSize)) / edgeSize);

            return 0f;
        }

        private bool IsPointerInsideWindow() {
            return _mousePosition.x >= 0 && _mousePosition.x <= Screen.width
                && _mousePosition.y >= 0 && _mousePosition.y <= Screen.height;
        }
        
        public void SetTarget(Transform newTarget) {
            target = newTarget;
        }
        
        public void ToggleLock() {
            isLocked = !isLocked;
        }
    }
}
