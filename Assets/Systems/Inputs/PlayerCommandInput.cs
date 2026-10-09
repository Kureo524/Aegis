using System;
using Features.Events;
using Systems.Characters;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace Systems.Inputs {
    public class PlayerCommandInput : MonoBehaviour {
        [SerializeField] public CharacterMovement characterMovement;
        [SerializeField] public Camera mainCamera;
        [SerializeField] public LayerMask groundLayer;
        [SerializeField] public GameObject clickMarkerPrefab;
        [SerializeField] public float markerDuration = 0.5f;

        private IEventBus _eventBus;
        private IDisposable _pointerCommandSubscription;
        private GameObject _currentMarker;
        private Plane _groundPlane;

        [Inject]
        public void Construct(IEventBus eventBus) {
            _eventBus = eventBus;
        }

        private void Awake() {
            if (characterMovement == null)
                characterMovement = GetComponent<CharacterMovement>();
            if (mainCamera == null)
                mainCamera = Camera.main;
            _groundPlane = new Plane(Vector3.up, Vector3.zero);
        }

        private void Start() {
            if (_eventBus == null) return;
            _pointerCommandSubscription = _eventBus.Subscribe<PointerCommandEvent>(OnPointerCommand);
        }

        private void OnDisable() {
            _pointerCommandSubscription?.Dispose();
        }

        private void OnPointerCommand(PointerCommandEvent evt) {
            Vector2 mousePos = evt.ScreenPosition;
            Ray ray = mainCamera.ScreenPointToRay(mousePos);

            // Check if clicking on UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            // Raycast to ground plane
            if (_groundPlane.Raycast(ray, out float distance)) {
                Vector3 worldPoint = ray.GetPoint(distance);
                characterMovement.SetDestination(worldPoint);
                ShowMarker(worldPoint, Color.green);
            }
        }

        private void ShowMarker(Vector3 position, Color color) {
            if (clickMarkerPrefab == null) return;
            
            if (_currentMarker != null)
                Destroy(_currentMarker);
            
            _currentMarker = Instantiate(clickMarkerPrefab, position, Quaternion.identity);
            var renderer = _currentMarker.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = color;
            
            Destroy(_currentMarker, markerDuration);
        }
    }
}
