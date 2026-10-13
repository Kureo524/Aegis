using Features.Characters;
using Features.Combat;
using UnityEngine;
using VContainer;

namespace Features.UI {
    /// <summary>
    /// World-space HP bar above a character: two sprites (background + fill) facing the camera.
    /// Sprites rather than UI so hundreds of them stay cheap. Same-prefab component: listens to its
    /// character's C# events directly. A shield shows as a white part after the HP (LoL style);
    /// the strongest status (§5.7, StatusKind order) shows as an icon left of the bar, else the icon
    /// of the spell whose mark the character carries (Ezreal W).
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
        [SerializeField] private SpriteRenderer statusIcon;
        [Tooltip("One sprite per StatusKind, in the enum order.")]
        [SerializeField] private Sprite[] statusSprites = System.Array.Empty<Sprite>();

        private Transform _camera;
        private Character _character;
        private SpriteRenderer _shield;
        private const float StatusIconSize = 0.28f;

        [Inject]
        public void Construct(Camera mainCamera) {
            _camera = mainCamera.transform;
        }

        public void Bind(Character character) {
            if (_character != null) {
                _character.Status.Changed -= OnStatusChanged;
                _character.Health.Changed -= OnHealthChanged;
                _character.Died -= OnDied;
                _character.Revived -= OnRevived;
            }

            _character = character;
            character.Health.Changed += OnHealthChanged;
            character.Status.Changed += OnStatusChanged;
            character.Died += OnDied;
            character.Revived += OnRevived;

            fill.color = character.Faction switch {
                Faction.Player => playerColor,
                Faction.Enemy => enemyColor,
                _ => neutralColor,
            };
            transform.localPosition = new Vector3(0f, character.Definition.BodySize.y + heightOffset, 0f);
            gameObject.SetActive(true);
            Refresh();
            OnStatusChanged();
        }

        private void OnStatusChanged() {
            if (statusIcon == null) return;
            Sprite icon = null;
            StatusEffects status = _character.Status;
            for (int i = 0; i < statusSprites.Length && icon == null; i++) {
                var kind = (StatusKind)i;
                bool active = kind switch {
                    StatusKind.Stun => status.IsStunned,
                    StatusKind.Root => status.IsRooted,
                    StatusKind.Silence => status.IsSilenced,
                    StatusKind.Slow => status.IsSlowed,
                    StatusKind.Burn => status.IsBurning,
                    StatusKind.Shred => status.IsShredded,
                    _ => status.IsBleeding,
                };
                if (active) icon = statusSprites[i];
            }
            float size = StatusIconSize;
            if (icon == null && status.MarkSpell != null) icon = status.MarkSpell.Icon;
            statusIcon.sprite = icon;
            statusIcon.enabled = icon != null;
            // Every icon at the same size on screen, whatever its resolution
            if (icon != null) statusIcon.transform.localScale = Vector3.one * (size / Mathf.Max(0.01f, icon.bounds.size.x));
        }

        private void OnHealthChanged(float current, float max) => Refresh();
        private void OnDied(Character _) => gameObject.SetActive(false);
        private void OnRevived(Character c) {
            gameObject.SetActive(true);
            Refresh();
        }

        private void Refresh() {
            Health health = _character.Health;
            float total = Mathf.Max(health.Max, health.Current + health.Shield);
            float hp = total > 0f ? health.Current / total : 0f;
            Place(fill, 0f, hp);
            if (health.Shield <= 0f && _shield == null) return;
            if (_shield == null) {   // created on the first shield: most characters never get one
                _shield = Instantiate(fill, fill.transform.parent);
                _shield.name = "Shield";
                _shield.color = new Color(0.92f, 0.92f, 0.95f);
            }
            _shield.enabled = health.Shield > 0f;
            Place(_shield, hp, total > 0f ? health.Shield / total : 0f);
        }

        // Fill anchored on the left of the bar, from start to start + ratio.
        private void Place(SpriteRenderer part, float start, float ratio) {
            ratio = Mathf.Clamp01(ratio);
            part.transform.localScale = new Vector3(width * ratio, fillHeight, 1f);
            part.transform.localPosition = new Vector3(-width / 2f + width * (start + ratio / 2f), 0f, -0.001f);
        }

        private void LateUpdate() {
            if (_camera != null) transform.rotation = _camera.rotation;
        }

        private void OnDestroy() {
            if (_character == null || _character.Health == null) return;
            _character.Status.Changed -= OnStatusChanged;
            _character.Health.Changed -= OnHealthChanged;
            _character.Died -= OnDied;
            _character.Revived -= OnRevived;
        }
    }
}
