using Features.Movement;
using UnityEngine;
using VContainer;

namespace Features.Characters {
    /// <summary>
    /// 2.5D look of a character (design doc §3): a sprite on a child of the physics root that always
    /// faces the camera (billboard: same rotation as the camera, so it reads flat on screen with no
    /// perspective skew), flipped left / right with the horizontal movement on screen.
    /// Sprites are drawn facing right.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CharacterVisual : MonoBehaviour {
        [SerializeField] private CharacterMovement movement;
        [Tooltip("Screen-space horizontal speed share below which the facing does not change.")]
        [SerializeField] private float flipThreshold = 0.1f;

        private SpriteRenderer _renderer;
        private Transform _camera;

        public SpriteRenderer Renderer => _renderer;
        public bool FacesLeft => _renderer.flipX;

        [Inject]
        public void Construct(Camera mainCamera) {
            _camera = mainCamera.transform;
        }

        private void Awake() {
            _renderer = GetComponent<SpriteRenderer>();
        }

        public void SetSprite(Sprite sprite) => _renderer.sprite = sprite;

        // TODO(phase 6): hundreds of enemies → one central billboard update instead of one LateUpdate each.
        private void LateUpdate() {
            if (_camera == null) return;
            transform.rotation = _camera.rotation;

            if (movement == null) return;
            float side = Vector3.Dot(movement.MoveDirection, _camera.right);
            if (Mathf.Abs(side) > flipThreshold) _renderer.flipX = side < 0f;
        }
    }
}
