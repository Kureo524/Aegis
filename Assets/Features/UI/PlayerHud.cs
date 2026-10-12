using System;
using Features.Events;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI {
    /// <summary>
    /// Bottom-center player HUD (UI Toolkit), fed only by bus events:
    /// feedback line, resolved spell + mana cost, element slots (ElementBarView), level badge,
    /// HP bar, mana bar with the cost of the current selection previewed on it (§7.3), XP bar.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PlayerHud : MonoBehaviour {
        [SerializeField] private Color hpColor = new(0.30f, 0.80f, 0.35f);
        [SerializeField] private Color manaColor = new(0.25f, 0.50f, 0.95f);
        [SerializeField] private Color xpColor = new(0.55f, 0.40f, 0.95f);
        [SerializeField] private float feedbackDuration = 1.5f;

        private IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();
        private VisualElement _hpFill, _manaFill, _manaCost, _xpFill;
        private Label _hpText, _manaText, _level, _spell, _feedback;
        private ElementBarView _elements;
        private float _feedbackUntil;

        // Last known values: events can arrive before the UI is built
        private float _hp, _hpMax, _mana, _manaMax;
        private XpChangedEvent _xp;
        private ElementSelectionChangedEvent _selection;
        private PlayerElementsEvent? _elementDefs;
        private ElementLevelsChangedEvent? _levels;
        private bool _hasElements;

        [Inject]
        public void Construct(IEventBus eventBus) {
            _eventBus = eventBus;
            _subscriptions.Add(eventBus.Subscribe<PlayerHealthChangedEvent>(e => { _hp = e.Current; _hpMax = e.Max; Refresh(); }));
            _subscriptions.Add(eventBus.Subscribe<PlayerManaChangedEvent>(e => { _mana = e.Current; _manaMax = e.Max; Refresh(); }));
            _subscriptions.Add(eventBus.Subscribe<XpChangedEvent>(e => { _xp = e; Refresh(); }));
            _subscriptions.Add(eventBus.Subscribe<PlayerElementsEvent>(e => {
                _elementDefs = e;
                _hasElements = true;
                if (_elements != null) { _elements.SetElements(e); ShowElements(); }
            }));
            _subscriptions.Add(eventBus.Subscribe<ElementLevelsChangedEvent>(e => { _levels = e; _elements?.SetLevels(e); }));
            _subscriptions.Add(eventBus.Subscribe<ElementSelectionChangedEvent>(e => {
                _selection = e;
                _elements?.SetSelection(e);
                Refresh();
            }));
            _subscriptions.Add(eventBus.Subscribe<GameplayFeedbackEvent>(e => ShowFeedback(e.Message)));
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
            _spell = UiStyles.Text("", 15, Color.white);
            _spell.style.marginBottom = 4;

            _elements = new ElementBarView();
            _elements.Root.style.marginBottom = 8;
            _elements.LevelUpClicked += slot => _eventBus.Publish(new ElementLevelUpRequestedEvent(slot));

            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            _level = UiStyles.Text("1", 20, Color.white);
            _level.style.width = _level.style.height = 44;
            _level.style.marginRight = 8;
            _level.style.unityTextAlign = TextAnchor.MiddleCenter;
            _level.style.backgroundColor = new Color(0.12f, 0.12f, 0.18f, 0.9f);
            UiStyles.Border(_level, 2, new Color(0.85f, 0.70f, 0.30f), 22);

            var bars = new VisualElement { pickingMode = PickingMode.Ignore };
            bars.style.width = 360;

            var hpBar = Bar(20, out _hpFill, hpColor);
            _hpText = BarText(hpBar, 12);

            var manaBar = Bar(14, out _manaFill, manaColor);
            manaBar.style.marginTop = 3;
            _manaCost = new VisualElement { pickingMode = PickingMode.Ignore };
            _manaCost.style.position = Position.Absolute;
            _manaCost.style.top = _manaCost.style.bottom = 0;
            manaBar.Add(_manaCost);
            _manaText = BarText(manaBar, 10);

            var xpBar = Bar(6, out _xpFill, xpColor);
            xpBar.style.marginTop = 3;

            bars.Add(hpBar);
            bars.Add(manaBar);
            bars.Add(xpBar);
            row.Add(_level);
            row.Add(bars);

            column.Add(_feedback);
            column.Add(_spell);
            column.Add(_elements.Root);
            column.Add(row);
            root.Add(column);

            if (_elementDefs.HasValue) _elements.SetElements(_elementDefs.Value);
            if (_levels.HasValue) _elements.SetLevels(_levels.Value);
            _elements.SetSelection(_selection);
            ShowElements();
            Refresh();
        }

        private void Update() {
            if (_feedback != null && _feedback.text.Length > 0 && Time.unscaledTime >= _feedbackUntil) _feedback.text = "";
        }

        private void OnDestroy() => _subscriptions.Dispose();

        private void ShowElements() {
            if (_elements == null) return;
            DisplayStyle display = _hasElements ? DisplayStyle.Flex : DisplayStyle.None;
            _elements.Root.style.display = display;
            _spell.style.display = display;
        }

        private void ShowFeedback(string message) {
            _feedbackUntil = Time.unscaledTime + feedbackDuration;
            if (_feedback != null) _feedback.text = message;
        }

        private void Refresh() {
            if (_hpFill == null) return;
            _hpFill.style.width = Percent(_hp, _hpMax);
            _hpText.text = $"{Mathf.CeilToInt(_hp)} / {Mathf.CeilToInt(_hpMax)}";

            _manaFill.style.width = Percent(_mana, _manaMax);
            _manaText.text = $"{Mathf.FloorToInt(_mana)} / {Mathf.CeilToInt(_manaMax)}";

            // Cost preview: the part of the current mana the spell would consume (red if not enough)
            float cost = _selection.Total > 0 ? _selection.ManaCost : 0f;
            bool affordable = cost <= _mana + 0.0001f;
            _manaCost.style.display = cost > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            if (cost > 0f && _manaMax > 0f) {
                float start = Mathf.Max(0f, (affordable ? _mana - cost : 0f) / _manaMax);
                float width = Mathf.Min(cost, affordable ? cost : _manaMax) / _manaMax;
                _manaCost.style.left = Length.Percent(start * 100f);
                _manaCost.style.width = Length.Percent(Mathf.Min(width, 1f - start) * 100f);
                _manaCost.style.backgroundColor = affordable ? new Color(0.75f, 0.9f, 1f, 0.75f) : new Color(1f, 0.25f, 0.2f, 0.6f);
            }

            if (_selection.Total == 0) _spell.text = "";
            else if (_selection.Spell == null) _spell.text = "Aucun sort pour cette combinaison";
            else _spell.text = $"{_selection.Spell.DisplayName}  ·  {Mathf.CeilToInt(cost)} mana";
            _spell.style.color = affordable ? Color.white : new Color(1f, 0.45f, 0.4f);

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
