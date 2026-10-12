using Features.Characters;
using UnityEngine;
using VContainer;

namespace Features.UI {
    /// <summary>
    /// World-space HP bar above a character: two sprites (background + fill) facing the camera.
    /// Sprites rather than UI so hundreds of them stay cheap. Same-prefab component: listens to its
    /// character's C# events directly.
    /// </summary>
    public class HealthBarView : MonoBehaviour {
        [SerializeField] private SpriteRenderer background;
        [SerializeField] private SpriteRenderer fill;
        [SerializeField] private float width = 1f;
        [SerializeField] private float fillHeight = 0.1f;
        [Tooltip("Gap between the top of the body and the bar.")]
        [SerializeField] private float heightOffset = 0.35f;
        [SerializeField] private Color playerColor = new(0.30f, 0.85f, 0.35f);
        [SerializeField] private Color enemyColor = new(0.90f, 0.25f, 0.20f);
        [SerializeField] private Color neutralColor = new(0.95f, 0.80f, 0.25f);

        private Transform _camera;
        private Character _character;

        [Inject]
        public void Construct(Camera mainCamera) {
            _camera = mainCamera.transform;
        }

        public void Bind(Character character) {
            if (_character != null) {
                _character.Health.Changed -= OnHealthChanged;
                _character.Died -= OnDied;
                _character.Revived -= OnRevived;
            }

            _character = character;
            character.Health.Changed += OnHealthChanged;
            character.Died += OnDied;
            character.Revived += OnRevived;

            fill.color = character.Faction switch {
                Faction.Player => playerColor,
                Faction.Enemy => enemyColor,
                _ => neutralColor,
            };
            transform.localPosition = new Vector3(0f, character.Definition.BodySize.y + heightOffset, 0f);
            gameObject.SetActive(true);
            SetRatio(character.Health.Ratio);
        }

        private void OnHealthChanged(float current, float max) => SetRatio(max > 0f ? current / max : 0f);
        private void OnDied(Character _) => gameObject.SetActive(false);
        private void OnRevived(Character c) {
            gameObject.SetActive(true);
            SetRatio(c.Health.Ratio);
        }

        // Fill anchored on the left: shrink and shift by half of what was lost.
        private void SetRatio(float ratio) {
            ratio = Mathf.Clamp01(ratio);
            fill.transform.localScale = new Vector3(width * ratio, fillHeight, 1f);
            fill.transform.localPosition = new Vector3(-width * (1f - ratio) / 2f, 0f, -0.001f);
        }

        private void LateUpdate() {
            if (_camera != null) transform.rotation = _camera.rotation;
        }

        private void OnDestroy() {
            if (_character == null || _character.Health == null) return;
            _character.Health.Changed -= OnHealthChanged;
            _character.Died -= OnDied;
            _character.Revived -= OnRevived;
        }
    }
}
