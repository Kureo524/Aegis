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
    /// HP bar, mana bar with the cost of the current selection previewed on it (§7.3), XP bar,
    /// signature ability slot (F), spell cooldowns (§7.9) and the run's gold (§9.2).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PlayerHud : MonoBehaviour {
        [SerializeField] private Color hpColor = new(0.30f, 0.80f, 0.35f);
        [SerializeField] private Color manaColor = new(0.25f, 0.50f, 0.95f);
        [SerializeField] private Color xpColor = new(0.55f, 0.40f, 0.95f);
        [SerializeField] private float feedbackDuration = 1.5f;

        private IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();
        private VisualElement _hpFill, _shieldFill, _manaFill, _manaCost, _xpFill;
        private Label _hpText, _manaText, _level, _spell, _feedback, _gold;
        private int _goldValue;
        private ElementBarView _elements;
        private float _feedbackUntil;
        private VisualElement _signatureSlot, _signatureIcon, _signatureShade;
        private Label _signatureCooldown;
        private float _elementalReadyAt, _signatureReadyAt;
        private Spells.SpellDefinition _signature;

        // Last known values: events can arrive before the UI is built
        private float _hp, _hpMax, _shield, _mana, _manaMax;
        private XpChangedEvent _xp;
        private ElementSelectionChangedEvent _selection;
        private PlayerElementsEvent? _elementDefs;
        private ElementLevelsChangedEvent? _levels;
        private bool _hasElements;

        [Inject]
        public void Construct(IEventBus eventBus) {
            _eventBus = eventBus;
            _subscriptions.Add(eventBus.Subscribe<PlayerHealthChangedEvent>(e => { _hp = e.Current; _hpMax = e.Max; _shield = e.Shield; Refresh(); }));
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
            _subscriptions.Add(eventBus.Subscribe<GoldChangedEvent>(e => {
                _goldValue = e.Gold;
                if (_gold != null) _gold.text = $"{_goldValue} or";
            }));
            _subscriptions.Add(eventBus.Subscribe<SignatureAssignedEvent>(e => { _signature = e.Spell; RefreshSignature(); }));
            _subscriptions.Add(eventBus.Subscribe<CooldownStartedEvent>(e => {
                float readyAt = Time.time + e.Duration;
                if (e.Slot == CooldownSlot.Elemental) _elementalReadyAt = readyAt;
                else _signatureReadyAt = readyAt;
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
            _spell = UiStyles.Text("", 15, Color.white);
            _spell.style.marginBottom = 4;

            _elements = new ElementBarView();
            _elements.Root.style.marginBottom = 8;
            _elements.LevelUpClicked += slot => _eventBus.Publish(new ElementLevelUpRequestedEvent(slot));
            _elements.Root.Add(BuildSignatureSlot());

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
            _gold = UiStyles.Text($"{_goldValue} or", 16, new Color(1f, 0.82f, 0.30f));
            _gold.style.width = 70;
            _gold.style.marginLeft = 10;

            row.Add(_level);
            row.Add(bars);
            row.Add(_gold);

            column.Add(_feedback);
            column.Add(_spell);
            column.Add(_elements.Root);
            column.Add(row);
            root.Add(column);

            if (_elementDefs.HasValue) _elements.SetElements(_elementDefs.Value);
            if (_levels.HasValue) _elements.SetLevels(_levels.Value);
            _elements.SetSelection(_selection);
            ShowElements();
            RefreshSignature();
            Refresh();
        }

        private VisualElement BuildSignatureSlot() {
            _signatureSlot = new VisualElement { pickingMode = PickingMode.Ignore };
            _signatureSlot.style.width = _signatureSlot.style.height = 44;
            _signatureSlot.style.marginLeft = 14;
            _signatureSlot.style.alignSelf = Align.FlexEnd;
            _signatureSlot.style.marginBottom = 9;
            _signatureSlot.style.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.9f);
            UiStyles.Border(_signatureSlot, 2, new Color(0.85f, 0.70f, 0.30f), 22);

            _signatureIcon = new VisualElement { pickingMode = PickingMode.Ignore };
            UiStyles.Fill(_signatureIcon);
            _signatureIcon.style.left = _signatureIcon.style.right = _signatureIcon.style.top = _signatureIcon.style.bottom = 6;
            _signatureIcon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            _signatureSlot.Add(_signatureIcon);

            _signatureShade = new VisualElement { pickingMode = PickingMode.Ignore };
            UiStyles.Fill(_signatureShade);
            _signatureShade.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            UiStyles.Border(_signatureShade, 0, Color.clear, 22);
            _signatureSlot.Add(_signatureShade);

            _signatureCooldown = UiStyles.Text("", 15, Color.white);
            UiStyles.Fill(_signatureCooldown);
            _signatureCooldown.style.unityTextAlign = TextAnchor.MiddleCenter;
            _signatureSlot.Add(_signatureCooldown);

            var key = UiStyles.Text("F", 11, Color.white);
            key.style.position = Position.Absolute;
            key.style.right = 2;
            key.style.bottom = 0;
            _signatureSlot.Add(key);
            return _signatureSlot;
        }

        private void RefreshSignature() {
            if (_signatureSlot == null) return;
            _signatureSlot.style.display = _signature != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (_signature != null && _signature.Icon != null) _signatureIcon.style.backgroundImage = new StyleBackground(_signature.Icon);
        }

        private void Update() {
            if (_feedback == null) return;
            if (_feedback.text.Length > 0 && Time.unscaledTime >= _feedbackUntil) _feedback.text = "";

            float signatureLeft = _signatureReadyAt - Time.time;
            _signatureShade.style.display = signatureLeft > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            _signatureCooldown.text = signatureLeft > 0f ? Mathf.CeilToInt(signatureLeft).ToString() : "";
            if (_elementalReadyAt > 0f) Refresh();   // spell line shows the elemental cooldown
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
            float total = Mathf.Max(_hpMax, _hp + _shield);
            _hpFill.style.width = Percent(_hp, total);
            _shieldFill.style.left = Percent(_hp, total);
            _shieldFill.style.width = Percent(_shield, total);
            _shieldFill.style.display = _shield > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            string shield = _shield > 0f ? $"  (+{Mathf.CeilToInt(_shield)})" : "";
            _hpText.text = $"{Mathf.CeilToInt(_hp)} / {Mathf.CeilToInt(_hpMax)}{shield}";

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
            else {
                float cooldown = _elementalReadyAt - Time.time;
                string ready = cooldown > 0f ? $"  ·  {cooldown:0.0} s" : "";
                _spell.text = $"{_selection.Spell.DisplayName}  ·  {Mathf.CeilToInt(cost)} mana{ready}";
            }
            bool onCooldown = _elementalReadyAt - Time.time > 0f;
            _spell.style.color = !affordable ? new Color(1f, 0.45f, 0.4f) : onCooldown ? new Color(0.7f, 0.7f, 0.75f) : Color.white;

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
