using Features.Characters;
using Features.Combat;
using UnityEngine;
using VContainer;

namespace Features.UI {
    /// <summary>
    /// World-space HP bar above a character, MOBA style: a black frame, a dark well, the fill with a
    /// light top edge and, on the player and bosses, a tick every 100 HP; sized from the body
    /// (wider for big creatures, a bit taller for the player). Sprites rather than UI so hundreds
    /// of them stay cheap. Same-prefab component: listens to its
    /// character's C# events directly. A shield shows as a white part after the HP (MOBA style);
    /// the strongest status (§5.7, StatusKind order) shows as an icon left of the bar, else the icon
    /// of the spell whose mark the character carries (marking spells).
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
        private SpriteRenderer _shield, _well, _highlight;
        private readonly System.Collections.Generic.List<SpriteRenderer> _ticks = new();
        private float _width, _height;
        private bool _withTicks;
        private const float StatusIconSize = 0.28f, Border = 0.035f;

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
            bool player = character.Faction == Faction.Player;
            bool big = character.Definition is Enemies.BossDefinition;
            _width = player ? width * 1.15f : big ? width * 2f : Mathf.Clamp(character.Definition.BodySize.x * 1.05f, 0.75f, 1.4f) * width;
            _height = player ? fillHeight * 1.35f : big ? fillHeight * 1.6f : fillHeight;
            _withTicks = player || big;
            background.color = new Color(0.01f, 0.01f, 0.02f, 0.95f);
            background.transform.localScale = new Vector3(_width + Border * 2f, _height + Border * 2f, 1f);
            if (_well == null) {
                _well = Instantiate(fill, fill.transform.parent);
                _well.name = "Well";
                _well.sortingOrder = fill.sortingOrder - 1;
                _highlight = Instantiate(fill, fill.transform.parent);
                _highlight.name = "Highlight";
                _highlight.sortingOrder = fill.sortingOrder + 2;
                _highlight.color = new Color(1f, 1f, 1f, 0.28f);
            }
            _well.color = new Color(0.16f, 0.10f, 0.10f, 1f);
            _well.transform.localScale = new Vector3(_width, _height, 1f);
            _well.transform.localPosition = new Vector3(0f, 0f, -0.0005f);
            if (statusIcon != null) statusIcon.transform.localPosition = new Vector3(-_width / 2f - 0.2f, 0f, -0.002f);
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
            if (_highlight != null) {
                Place(_highlight, 0f, hp);
                _highlight.transform.localScale = new Vector3(_highlight.transform.localScale.x, _height * 0.32f, 1f);
                _highlight.transform.localPosition += new Vector3(0f, _height * 0.3f, -0.001f);
            }
            PlaceTicks(total);
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
            float w = _width > 0f ? _width : width, h = _height > 0f ? _height : fillHeight;
            part.transform.localScale = new Vector3(w * ratio, h, 1f);
            part.transform.localPosition = new Vector3(-w / 2f + w * (start + ratio / 2f), 0f, -0.001f);
        }

        // A thin dark line every 100 HP (taller every 1000; only the 1000s on huge pools), up to 40
        private void PlaceTicks(float total) {
            int step = total > 4000f ? 1000 : 100;
            int count = _withTicks ? Mathf.Clamp(Mathf.CeilToInt(total / step) - 1, 0, 40) : 0;
            while (_ticks.Count < count) {
                SpriteRenderer t = Instantiate(fill, fill.transform.parent);
                t.name = "Tick";
                t.color = new Color(0f, 0f, 0f, 0.6f);
                t.sortingOrder = fill.sortingOrder + 3;
                _ticks.Add(t);
            }
            for (int i = 0; i < _ticks.Count; i++) {
                bool on = i < count;
                _ticks[i].enabled = on;
                if (!on) continue;
                int hp = (i + 1) * step;
                bool major = hp % 1000 == 0;
                float x = -_width / 2f + _width * hp / total;
                float h = major ? _height : _height * 0.55f;
                _ticks[i].transform.localScale = new Vector3(major ? 0.025f : 0.012f, h, 1f);
                _ticks[i].transform.localPosition = new Vector3(x, (_height - h) / 2f, -0.0015f);
            }
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
