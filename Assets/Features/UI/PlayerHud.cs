using System;
using Features.Events;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI {
    /// <summary>
    /// Bottom-center player HUD (UI Toolkit): level badge, HP bar with numbers, XP bar.
    /// Fed only by bus events (PlayerHealthChanged, XpChanged).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PlayerHud : MonoBehaviour {
        [SerializeField] private Color hpColor = new(0.30f, 0.80f, 0.35f);
        [SerializeField] private Color xpColor = new(0.55f, 0.40f, 0.95f);

        private readonly DisposableBag _subscriptions = new();
        private VisualElement _hpFill, _xpFill;
        private Label _hpText, _level;

        // Last known values: events can arrive before the UI is built
        private float _hp, _hpMax;
        private XpChangedEvent _xp;

        [Inject]
        public void Construct(IEventBus eventBus) {
            _subscriptions.Add(eventBus.Subscribe<PlayerHealthChangedEvent>(e => {
                _hp = e.Current;
                _hpMax = e.Max;
                Refresh();
            }));
            _subscriptions.Add(eventBus.Subscribe<XpChangedEvent>(e => {
                _xp = e;
                Refresh();
            }));
        }

        private void Start() {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            var hud = new VisualElement { pickingMode = PickingMode.Ignore };
            hud.style.position = Position.Absolute;
            hud.style.bottom = 24;
            hud.style.left = 0;
            hud.style.right = 0;
            hud.style.flexDirection = FlexDirection.Row;
            hud.style.justifyContent = Justify.Center;
            hud.style.alignItems = Align.Center;

            _level = new Label("1");
            _level.style.width = 44;
            _level.style.height = 44;
            _level.style.marginRight = 8;
            _level.style.unityTextAlign = TextAnchor.MiddleCenter;
            _level.style.fontSize = 20;
            _level.style.unityFontStyleAndWeight = FontStyle.Bold;
            _level.style.color = Color.white;
            _level.style.backgroundColor = new Color(0.12f, 0.12f, 0.18f, 0.9f);
            SetBorder(_level, 2, new Color(0.85f, 0.70f, 0.30f), 22);

            var bars = new VisualElement();
            bars.style.width = 360;

            var hpBar = Bar(22, out _hpFill, hpColor);
            _hpText = new Label();
            _hpText.style.position = Position.Absolute;
            _hpText.style.left = 0;
            _hpText.style.right = 0;
            _hpText.style.top = 0;
            _hpText.style.bottom = 0;
            _hpText.style.unityTextAlign = TextAnchor.MiddleCenter;
            _hpText.style.fontSize = 13;
            _hpText.style.color = Color.white;
            _hpText.style.unityFontStyleAndWeight = FontStyle.Bold;
            hpBar.Add(_hpText);

            var xpBar = Bar(8, out _xpFill, xpColor);
            xpBar.style.marginTop = 4;

            bars.Add(hpBar);
            bars.Add(xpBar);
            hud.Add(_level);
            hud.Add(bars);
            root.Add(hud);
            Refresh();
        }

        private void OnDestroy() => _subscriptions.Dispose();

        private void Refresh() {
            if (_hpFill == null) return;
            float hpRatio = _hpMax > 0f ? Mathf.Clamp01(_hp / _hpMax) : 0f;
            _hpFill.style.width = Length.Percent(hpRatio * 100f);
            _hpText.text = $"{Mathf.CeilToInt(_hp)} / {Mathf.CeilToInt(_hpMax)}";

            float xpRatio = _xp.XpToNext > 0 ? Mathf.Clamp01((float)_xp.Xp / _xp.XpToNext) : 1f;
            _xpFill.style.width = Length.Percent(xpRatio * 100f);
            _level.text = Math.Max(_xp.Level, 1).ToString();
        }

        private static VisualElement Bar(float height, out VisualElement fill, Color color) {
            var bar = new VisualElement();
            bar.style.height = height;
            bar.style.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.85f);
            SetBorder(bar, 1, new Color(0f, 0f, 0f, 0.9f), 3);
            bar.style.overflow = Overflow.Hidden;

            fill = new VisualElement();
            fill.style.height = Length.Percent(100);
            fill.style.width = Length.Percent(100);
            fill.style.backgroundColor = color;
            bar.Add(fill);
            return bar;
        }

        private static void SetBorder(VisualElement e, float width, Color color, float radius) {
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = width;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = color;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = radius;
        }
    }
}
