using System;
using Features.Events;
using Systems.Inputs;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI {
    /// <summary>
    /// Bottom-center player HUD (UI Toolkit), fed only by bus events: feedback line, the spell bar
    /// (AbilityBarView: Q W E R + D F, ranks, "+", cooldowns, §7.9), level badge, HP bar (with
    /// shield), mana bar, XP bar and the run's gold (§9.2).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PlayerHud : MonoBehaviour {
        [SerializeField] private Color hpColor = new(0.30f, 0.80f, 0.35f);
        [SerializeField] private Color manaColor = new(0.25f, 0.50f, 0.95f);
        [SerializeField] private Color xpColor = new(0.55f, 0.40f, 0.95f);
        [SerializeField] private float feedbackDuration = 1.5f;

        private IEventBus _eventBus;
        private ControlsSettings _controls;
        private readonly DisposableBag _subscriptions = new();
        private VisualElement _hpFill, _shieldFill, _manaFill, _xpFill;
        private Label _hpText, _manaText, _level, _feedback, _gold;
        private int _goldValue;
        private AbilityBarView _abilities;
        private float _feedbackUntil;

        // Last known values: events can arrive before the UI is built
        private float _hp, _hpMax, _shield, _mana, _manaMax;
        private XpChangedEvent _xp;
        private AbilitiesChangedEvent? _abilityState;
        private readonly float[] _cooldownEnds = new float[Spells.AbilitySlots.Count];

        [Inject]
        public void Construct(IEventBus eventBus, ControlsSettings controls) {
            _eventBus = eventBus;
            _controls = controls;
            _subscriptions.Add(eventBus.Subscribe<PlayerHealthChangedEvent>(e => { _hp = e.Current; _hpMax = e.Max; _shield = e.Shield; Refresh(); }));
            _subscriptions.Add(eventBus.Subscribe<PlayerManaChangedEvent>(e => { _mana = e.Current; _manaMax = e.Max; Refresh(); }));
            _subscriptions.Add(eventBus.Subscribe<XpChangedEvent>(e => { _xp = e; Refresh(); }));
            _subscriptions.Add(eventBus.Subscribe<AbilitiesChangedEvent>(e => {
                _abilityState = e;
                _abilities?.SetAbilities(e);
            }));
            _subscriptions.Add(eventBus.Subscribe<CooldownStartedEvent>(e => {
                if (e.Slot >= 0 && e.Slot < _cooldownEnds.Length) _cooldownEnds[e.Slot] = Time.time + e.Duration;
                _abilities?.StartCooldown(e.Slot, e.Duration);
            }));
            _subscriptions.Add(eventBus.Subscribe<GameplayFeedbackEvent>(e => ShowFeedback(e.Message)));
            _subscriptions.Add(eventBus.Subscribe<GoldChangedEvent>(e => {
                _goldValue = e.Gold;
                if (_gold != null) _gold.text = $"{_goldValue} or";
            }));
        }

        private void Start() {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.style.position = Position.Absolute;
            column.style.bottom = 20;
            column.style.left = column.style.right = 0;
            column.style.alignItems = Align.Center;

            _feedback = UiStyles.Text("", 15, new Color(1f, 0.55f, 0.45f));
            _feedback.style.marginBottom = 6;

            _abilities = new AbilityBarView();
            _abilities.Root.style.marginBottom = 8;
            _abilities.RankUpClicked += slot => _eventBus.Publish(new AbilityRankUpRequestedEvent(slot));
            _abilities.SetKeys(KeyLabels(_controls != null ? _controls.KeyboardLayout : KeyboardLayout.Azerty));

            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            _level = UiStyles.Text("1", 20, Color.white);
            _level.style.width = _level.style.height = 44;
            _level.style.marginRight = 8;
            _level.style.marginLeft = 28;   // balances the gold label on the right
            _level.style.unityTextAlign = TextAnchor.MiddleCenter;
            _level.style.backgroundColor = new Color(0.12f, 0.12f, 0.18f, 0.9f);
            UiStyles.Border(_level, 2, new Color(0.85f, 0.70f, 0.30f), 22);

            var bars = new VisualElement { pickingMode = PickingMode.Ignore };
            bars.style.width = 360;

            var hpBar = Bar(20, out _hpFill, hpColor);
            // Shield: white, right after the HP (LoL style)
            _shieldFill = new VisualElement { pickingMode = PickingMode.Ignore };
            _shieldFill.style.position = Position.Absolute;
            _shieldFill.style.top = _shieldFill.style.bottom = 0;
            _shieldFill.style.backgroundColor = new Color(0.92f, 0.92f, 0.95f, 0.9f);
            hpBar.Add(_shieldFill);
            _hpText = BarText(hpBar, 12);

            var manaBar = Bar(14, out _manaFill, manaColor);
            manaBar.style.marginTop = 3;
            _manaText = BarText(manaBar, 10);

            var xpBar = Bar(6, out _xpFill, xpColor);
            xpBar.style.marginTop = 3;

            bars.Add(hpBar);
            bars.Add(manaBar);
            bars.Add(xpBar);
            _gold = UiStyles.Text($"{_goldValue} or", 16, new Color(1f, 0.82f, 0.30f));
            _gold.style.width = 70;
            _gold.style.marginLeft = 10;

            row.Add(_level);
            row.Add(bars);
            row.Add(_gold);

            column.Add(_feedback);
            column.Add(_abilities.Root);
            column.Add(row);
            root.Add(column);

            if (_abilityState.HasValue) _abilities.SetAbilities(_abilityState.Value);
            for (int slot = 0; slot < _cooldownEnds.Length; slot++) _abilities.StartCooldown(slot, _cooldownEnds[slot] - Time.time);
            Refresh();
        }

        // The keys sit at the QWERTY positions Q W E R (A Z E R on AZERTY), then D F
        private static string[] KeyLabels(KeyboardLayout layout) =>
            layout == KeyboardLayout.Azerty ? new[] { "A", "Z", "E", "R", "D", "F" } : new[] { "Q", "W", "E", "R", "D", "F" };

        private void Update() {
            if (_feedback == null) return;
            if (_feedback.text.Length > 0 && Time.unscaledTime >= _feedbackUntil) _feedback.text = "";
            _abilities.Tick();
        }

        private void OnDestroy() => _subscriptions.Dispose();

        private void ShowFeedback(string message) {
            _feedbackUntil = Time.unscaledTime + feedbackDuration;
            if (_feedback != null) _feedback.text = message;
        }

        private void Refresh() {
            if (_hpFill == null) return;
            float total = Mathf.Max(_hpMax, _hp + _shield);
            _hpFill.style.width = Percent(_hp, total);
            _shieldFill.style.left = Percent(_hp, total);
            _shieldFill.style.width = Percent(_shield, total);
            _shieldFill.style.display = _shield > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            string shield = _shield > 0f ? $"  (+{Mathf.CeilToInt(_shield)})" : "";
            _hpText.text = $"{Mathf.CeilToInt(_hp)} / {Mathf.CeilToInt(_hpMax)}{shield}";

            _manaFill.style.width = Percent(_mana, _manaMax);
            _manaText.text = $"{Mathf.FloorToInt(_mana)} / {Mathf.CeilToInt(_manaMax)}";

            _xpFill.style.width = _xp.XpToNext > 0 ? Length.Percent(Mathf.Clamp01((float)_xp.Xp / _xp.XpToNext) * 100f) : Length.Percent(100f);
            _level.text = Math.Max(_xp.Level, 1).ToString();
        }

        private static Length Percent(float value, float max) => Length.Percent(max > 0f ? Mathf.Clamp01(value / max) * 100f : 0f);

        private static Label BarText(VisualElement bar, float size) {
            Label text = UiStyles.Text("", size, Color.white);
            UiStyles.Fill(text);
            text.style.unityTextAlign = TextAnchor.MiddleCenter;
            bar.Add(text);
            return text;
        }

        private static VisualElement Bar(float height, out VisualElement fill, Color color) {
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.style.height = height;
            bar.style.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.85f);
            UiStyles.Border(bar, 1, new Color(0f, 0f, 0f, 0.9f), 3);
            bar.style.overflow = Overflow.Hidden;

            fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.style.height = Length.Percent(100);
            fill.style.width = Length.Percent(100);
            fill.style.backgroundColor = color;
            bar.Add(fill);
            return bar;
        }
    }
}
