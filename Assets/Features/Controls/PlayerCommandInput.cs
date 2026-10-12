using System;
using Features.Movement;
using Systems.EventBusses;
using Systems.Inputs.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;
using VContainer.Unity;

namespace Features.Controls {
    /// <summary>
    /// Turns the player's pointer commands into orders for the character (design doc §4).
    /// Right click on the ground: move there, LoL style.
    /// </summary>
    public class PlayerCommandInput : MonoBehaviour {
        [SerializeField] public CharacterMovement characterMovement;
        [SerializeField] public Camera mainCamera;
        [SerializeField] public GameObject clickMarkerPrefab;   // must carry a ClickMarker

        private IEventBus _eventBus;
        private IObjectResolver _resolver;
        private IDisposable _pointerCommandSubscription;
        private ClickMarker _currentMarker;
        private Plane _groundPlane;

        [Inject]
        public void Construct(IEventBus eventBus, IObjectResolver resolver) {
            _eventBus = eventBus;
            _resolver = resolver;
        }

        private void Awake() {
            _groundPlane = new Plane(Vector3.up, Vector3.zero);
        }

        private void Start() {
            _pointerCommandSubscription = _eventBus.Subscribe<PointerCommandEvent>(OnPointerCommand);
        }

        private void OnDestroy() => _pointerCommandSubscription?.Dispose();

        private void OnPointerCommand(PointerCommandEvent evt) {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = mainCamera.ScreenPointToRay(evt.ScreenPosition);
            if (!_groundPlane.Raycast(ray, out float distance)) return;

            Vector3 worldPoint = ray.GetPoint(distance);
            characterMovement.SetDestination(worldPoint);
            ShowMarker(worldPoint, Color.green);
        }

        private void ShowMarker(Vector3 position, Color color) {
            if (clickMarkerPrefab == null) return;
            if (_currentMarker != null) Destroy(_currentMarker.gameObject);

            _currentMarker = _resolver.Instantiate(clickMarkerPrefab, position, Quaternion.identity)
                                      .GetComponent<ClickMarker>();
            if (_currentMarker != null) _currentMarker.SetColor(color);
        }
    }
}
