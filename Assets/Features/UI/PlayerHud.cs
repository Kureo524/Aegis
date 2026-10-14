using System;
using System.Collections.Generic;
using Features.Augments;
using Features.Economy;
using Features.Events;
using Features.Stats;
using Systems.EventBusses;
using Systems.Inputs;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI {
    /// <summary>
    /// Bottom player HUD, LoL style (design doc §7.9), UI Toolkit styled by the theme, fed only by
    /// bus events:
    /// - left: one panel with two tabs, Stats (AD, AP, armor… from StatChangedEvent) and Augments;
    /// - center: buff bar (passive stacks, charged spellblade), feedback line, champion portrait
    ///   with its level, the spell bar (passive, Q W E R, D F) and the HP (shield) / mana / XP bars;
    /// - right: the 6 item slots, the active elixir and the gold.
    /// Every icon has a tooltip (UiTooltip).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PlayerHud : MonoBehaviour {
        [SerializeField] private float feedbackDuration = 1.5f;

        private IEventBus _eventBus;
        private ControlsSettings _controls;
        private UiTheme _theme;
        private readonly DisposableBag _subscriptions = new();
        private UiTooltip _tooltip;
        private AbilityBarView _abilities;
        private BarTicks _hpTicks;
        private VisualElement _hpFill, _shieldFill, _manaFill, _xpFill, _portrait, _items, _buffs, _statsPage, _augmentsPage;
        private Label _hpText, _manaText, _level, _feedback, _gold, _elixir, _augmentCount;
        private Button _statsTab, _augmentsTab;
        private float _feedbackUntil;

        // Last known values: events can arrive before the UI is built
        private float _hp, _hpMax, _shield, _mana, _manaMax;
        private int _goldValue;
        private XpChangedEvent _xp;
        private AbilitiesChangedEvent? _abilityState;
        private PlayerChampionEvent? _champion;
        private PlayerBuff[] _buffList = Array.Empty<PlayerBuff>();
        private IReadOnlyList<ItemDefinition> _inventory = Array.Empty<ItemDefinition>();
        private ElixirDefinition _activeElixir;
        private IReadOnlyList<AugmentDefinition> _augmentList = Array.Empty<AugmentDefinition>();
        private int _augmentSlots;
        private readonly Dictionary<StatType, float> _stats = new();
        private readonly Dictionary<StatType, Label> _statLabels = new();
        private readonly float[] _cooldownEnds = new float[Spells.AbilitySlots.Count];

        [Inject]
        public void Construct(IEventBus eventBus, ControlsSettings controls, UiTheme theme) {
            _eventBus = eventBus;
            _controls = controls;
            _theme = theme;
            _subscriptions.Add(eventBus.Subscribe<PlayerHealthChangedEvent>(e => { _hp = e.Current; _hpMax = e.Max; _shield = e.Shield; RefreshBars(); }));
            _subscriptions.Add(eventBus.Subscribe<PlayerManaChangedEvent>(e => { _mana = e.Current; _manaMax = e.Max; RefreshBars(); }));
            _subscriptions.Add(eventBus.Subscribe<XpChangedEvent>(e => { _xp = e; RefreshBars(); }));
            _subscriptions.Add(eventBus.Subscribe<AbilitiesChangedEvent>(e => { _abilityState = e; _abilities?.SetAbilities(e); }));
            _subscriptions.Add(eventBus.Subscribe<CooldownStartedEvent>(e => {
                if (e.Slot >= 0 && e.Slot < _cooldownEnds.Length) _cooldownEnds[e.Slot] = Time.time + e.Duration;
                _abilities?.StartCooldown(e.Slot, e.Duration);
            }));
            _subscriptions.Add(eventBus.Subscribe<PlayerChampionEvent>(e => { _champion = e; RefreshChampion(); }));
            _subscriptions.Add(eventBus.Subscribe<PlayerBuffsEvent>(e => { _buffList = e.Buffs; RefreshBuffs(); }));
            _subscriptions.Add(eventBus.Subscribe<GameplayFeedbackEvent>(e => ShowFeedback(e.Message)));
            _subscriptions.Add(eventBus.Subscribe<GoldChangedEvent>(e => { _goldValue = e.Gold; RefreshItems(); }));
            _subscriptions.Add(eventBus.Subscribe<InventoryChangedEvent>(e => { _inventory = e.Items; RefreshItems(); }));
            _subscriptions.Add(eventBus.Subscribe<ElixirChangedEvent>(e => { _activeElixir = e.Elixir; RefreshItems(); }));
            _subscriptions.Add(eventBus.Subscribe<AugmentsChangedEvent>(e => { _augmentList = e.Augments; _augmentSlots = e.Slots; RefreshAugments(); }));
            _subscriptions.Add(eventBus.Subscribe<StatChangedEvent>(e => { _stats[e.Type] = e.Value; RefreshStat(e.Type); }));
        }

        private void Start() {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.AddToClassList("ae-root");
            _tooltip = new UiTooltip();

            var hud = new VisualElement { pickingMode = PickingMode.Ignore };
            hud.AddToClassList("ae-hud");
            hud.Add(BuildLeft());
            hud.Add(BuildCenter());
            hud.Add(BuildRight());
            root.Add(hud);
            root.Add(_tooltip.Root);

            if (_abilityState.HasValue) _abilities.SetAbilities(_abilityState.Value);
            for (int slot = 0; slot < _cooldownEnds.Length; slot++) _abilities.StartCooldown(slot, _cooldownEnds[slot] - Time.time);
            ShowTab(stats: true);
            RefreshChampion();
            RefreshBars();
            RefreshItems();
            RefreshAugments();
            RefreshBuffs();
            foreach (StatType t in _statLabels.Keys) RefreshStat(t);
        }

        private void OnDestroy() => _subscriptions.Dispose();

        private void Update() {
            if (_feedback == null) return;
            if (_feedback.text.Length > 0 && Time.unscaledTime >= _feedbackUntil) _feedback.text = "";
            _abilities.Tick();
        }

        // ---- left: stats / augments ----
        private VisualElement BuildLeft() {
            var panel = new VisualElement();
            panel.AddToClassList("ae-dock-panel");
            panel.AddToClassList("ae-hud__side");
            panel.AddToClassList("ae-hud__side--left");

            var tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.marginBottom = 4;
            _statsTab = new Button(() => ShowTab(stats: true)) { text = "Statistiques" };
            _augmentsTab = new Button(() => ShowTab(stats: false)) { text = "Augments" };
            _statsTab.AddToClassList("ae-tab");
            _augmentsTab.AddToClassList("ae-tab");
            tabs.Add(_statsTab);
            tabs.Add(_augmentsTab);
            panel.Add(tabs);

            _statsPage = new VisualElement();
            _statsPage.style.flexDirection = FlexDirection.Row;
            _statsPage.style.flexWrap = Wrap.Wrap;
            if (_theme != null)
                foreach (UiTheme.StatIcon s in _theme.Stats) _statsPage.Add(StatCell(s));
            panel.Add(_statsPage);

            _augmentsPage = new VisualElement();
            _augmentCount = new Label();
            _augmentCount.AddToClassList("ae-dim");
            _augmentCount.style.fontSize = 11;
            _augmentsPage.Add(_augmentCount);
            panel.Add(_augmentsPage);
            return panel;
        }

        private VisualElement StatCell(UiTheme.StatIcon s) {
            var cell = new VisualElement();
            cell.AddToClassList("ae-stat");
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("ae-stat__icon");
            if (s.Icon != null) icon.style.backgroundImage = new StyleBackground(s.Icon);
            var value = new Label("0") { pickingMode = PickingMode.Ignore };
            value.AddToClassList("ae-stat__value");
            cell.Add(icon);
            cell.Add(value);
            _statLabels[s.Stat] = value;
            StatType stat = s.Stat;
            _tooltip.Attach(cell, () => (Capitalize(StatLabels.Name(stat)), FormatStat(stat, _stats.TryGetValue(stat, out float v) ? v : 0f), StatHint(stat)));
            return cell;
        }

        private void ShowTab(bool stats) {
            if (_statsPage == null) return;
            _statsPage.style.display = stats ? DisplayStyle.Flex : DisplayStyle.None;
            _augmentsPage.style.display = stats ? DisplayStyle.None : DisplayStyle.Flex;
            _statsTab.EnableInClassList("ae-tab--active", stats);
            _augmentsTab.EnableInClassList("ae-tab--active", !stats);
        }

        private void RefreshStat(StatType type) {
            if (!_statLabels.TryGetValue(type, out Label label)) return;
            label.text = FormatStat(type, _stats.TryGetValue(type, out float v) ? v : 0f);
        }

        private static string FormatStat(StatType type, float v) => type switch {
            StatType.AttackSpeed => v.ToString("0.00"),
            StatType.MoveSpeed => Mathf.RoundToInt(v * 65f).ToString(),   // LoL units (325 = 5)
            StatType.CritChance or StatType.Lifesteal or StatType.Omnivamp or StatType.ArmorPenPercent
                or StatType.MagicPenPercent or StatType.Tenacity => $"{Mathf.RoundToInt(v * 100f)} %",
            _ => Mathf.RoundToInt(v).ToString(),
        };

        private static string StatHint(StatType type) => type switch {
            StatType.PhysicalAttack => "Attaque physique (AD) : attaques de base et ratios AD des sorts.",
            StatType.MagicAttack => "Puissance magique (AP) : ratios AP des sorts et des objets.",
            StatType.Armor => "Réduit les dégâts physiques subis.",
            StatType.MagicResist => "Réduit les dégâts magiques subis.",
            StatType.AttackSpeed => "Attaques par seconde.",
            StatType.AbilityHaste => "Réduit les délais de récupération des sorts.",
            StatType.CritChance => "Chances qu'une attaque inflige un coup critique.",
            StatType.MoveSpeed => "Vitesse de déplacement.",
            StatType.Lifesteal => "Part des dégâts des attaques rendue en PV.",
            StatType.ArmorPenPercent => "Part de l'armure de la cible ignorée.",
            StatType.MagicPenPercent => "Part de la résistance magique de la cible ignorée.",
            StatType.Tenacity => "Raccourcit les contrôles et les ralentissements.",
            _ => "",
        };

        private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        private void RefreshAugments() {
            if (_augmentsPage == null) return;
            while (_augmentsPage.childCount > 1) _augmentsPage.RemoveAt(1);
            _augmentCount.text = $"{_augmentList.Count} / {Mathf.Max(_augmentSlots, _augmentList.Count)} augments";
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginTop = 4;
            for (int i = 0; i < Mathf.Max(_augmentSlots, _augmentList.Count); i++) {
                AugmentDefinition a = i < _augmentList.Count ? _augmentList[i] : null;
                var slot = new VisualElement();
                slot.AddToClassList("ae-augment");
                if (a != null) {
                    slot.AddToClassList("ae-augment--" + a.Rarity.ToString().ToLowerInvariant());
                    if (a.Icon != null) slot.style.backgroundImage = new StyleBackground(a.Icon);
                    _tooltip.Attach(slot, () => (a.DisplayName, $"Augment {StatLabels.Name(a.Rarity)}", a.Description));
                }
                row.Add(slot);
            }
            _augmentsPage.Add(row);
        }

        // ---- center: buffs, portrait, spells, bars ----
        private VisualElement BuildCenter() {
            var center = new VisualElement { pickingMode = PickingMode.Ignore };
            center.AddToClassList("ae-hud__center");

            _buffs = new VisualElement { pickingMode = PickingMode.Ignore };
            _buffs.AddToClassList("ae-buffs");
            center.Add(_buffs);

            _feedback = new Label { pickingMode = PickingMode.Ignore };
            _feedback.AddToClassList("ae-feedback");
            center.Add(_feedback);

            var main = new VisualElement { pickingMode = PickingMode.Ignore };
            main.AddToClassList("ae-dock-panel");
            main.AddToClassList("ae-hud__main");
            var ornament = new VisualElement { pickingMode = PickingMode.Ignore };
            ornament.AddToClassList("ae-hud__ornament");
            main.Add(ornament);

            var portraitColumn = new VisualElement { pickingMode = PickingMode.Ignore };
            portraitColumn.AddToClassList("ae-portrait");
            _portrait = new VisualElement { pickingMode = PickingMode.Ignore };
            _portrait.AddToClassList("ae-portrait__image");
            portraitColumn.Add(_portrait);
            _level = new Label("1") { pickingMode = PickingMode.Ignore };
            _level.AddToClassList("ae-portrait__level");
            portraitColumn.Add(_level);
            main.Add(portraitColumn);

            var right = new VisualElement { pickingMode = PickingMode.Ignore };
            right.style.marginLeft = 10;
            _abilities = new AbilityBarView(_tooltip);
            _abilities.RankUpClicked += slot => _eventBus.Publish(new AbilityRankUpRequestedEvent(slot));
            _abilities.SetKeys(KeyLabels(_controls != null ? _controls.KeyboardLayout : KeyboardLayout.Azerty));
            right.Add(_abilities.Root);

            VisualElement hpBar = Bar("ae-bar--hp", out _hpFill, out _hpText);
            _shieldFill = new VisualElement { pickingMode = PickingMode.Ignore };
            _shieldFill.AddToClassList("ae-bar__shield");
            hpBar.Insert(1, _shieldFill);
            _hpTicks = new BarTicks();
            hpBar.Insert(2, _hpTicks);
            right.Add(hpBar);
            right.Add(Bar("ae-bar--mana", out _manaFill, out _manaText));
            right.Add(Bar("ae-bar--xp", out _xpFill, out _));
            main.Add(right);
            center.Add(main);
            return center;
        }

        private static VisualElement Bar(string kind, out VisualElement fill, out Label text) {
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("ae-bar");
            bar.AddToClassList(kind);
            fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("ae-bar__fill");
            bar.Add(fill);
            var gloss = new VisualElement { pickingMode = PickingMode.Ignore };
            gloss.AddToClassList("ae-bar__gloss");
            bar.Add(gloss);
            text = new Label { pickingMode = PickingMode.Ignore };
            text.AddToClassList("ae-bar__text");
            bar.Add(text);
            return bar;
        }

        // The keys sit at the QWERTY positions Q W E R (A Z E R on AZERTY), then D F
        private static string[] KeyLabels(KeyboardLayout layout) =>
            layout == KeyboardLayout.Azerty ? new[] { "A", "Z", "E", "R", "D", "F" } : new[] { "Q", "W", "E", "R", "D", "F" };

        private void RefreshChampion() {
            if (_portrait == null || !_champion.HasValue) return;
            PlayerChampionEvent c = _champion.Value;
            if (c.Portrait != null) _portrait.style.backgroundImage = new StyleBackground(c.Portrait);
            _abilities.SetPassive(c.Passive);
        }

        private void RefreshBuffs() {
            if (_buffs == null) return;
            _buffs.Clear();
            int heroStacks = 0;
            foreach (PlayerBuff b in _buffList) {
                if (_champion.HasValue && _champion.Value.Passive != null && b.Name == _champion.Value.Passive.DisplayName) heroStacks = b.Stacks;
                var buff = new VisualElement();
                buff.AddToClassList("ae-buff");
                buff.EnableInClassList("ae-buff--charged", b.Charged);
                if (b.Icon != null) buff.style.backgroundImage = new StyleBackground(b.Icon);
                if (b.Stacks > 0) {
                    var stacks = new Label(b.Stacks.ToString()) { pickingMode = PickingMode.Ignore };
                    stacks.AddToClassList("ae-buff__stacks");
                    buff.Add(stacks);
                }
                PlayerBuff info = b;
                _tooltip.Attach(buff, () => (info.Name, info.Charged ? "Prêt" : $"{info.Stacks} cumul(s)", info.Description));
                _buffs.Add(buff);
            }
            _abilities?.SetPassiveStacks(heroStacks);
        }

        private void RefreshBars() {
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
            _hpTicks.SetMax(total);
            _xpFill.style.width = _xp.XpToNext > 0 ? Length.Percent(Mathf.Clamp01((float)_xp.Xp / _xp.XpToNext) * 100f) : Length.Percent(100f);
            _level.text = Math.Max(_xp.Level, 1).ToString();
        }

        private static Length Percent(float value, float max) => Length.Percent(max > 0f ? Mathf.Clamp01(value / max) * 100f : 0f);

        // ---- right: items, elixir, gold ----
        private VisualElement BuildRight() {
            var panel = new VisualElement();
            panel.AddToClassList("ae-dock-panel");
            panel.AddToClassList("ae-hud__side");
            panel.AddToClassList("ae-hud__side--right");
            panel.AddToClassList("ae-hud__items");
            _items = new VisualElement();
            _items.AddToClassList("ae-items");
            panel.Add(_items);
            _elixir = new Label { pickingMode = PickingMode.Ignore };
            _elixir.AddToClassList("ae-elixir");
            panel.Add(_elixir);
            var goldRow = new VisualElement { pickingMode = PickingMode.Ignore };
            goldRow.AddToClassList("ae-goldrow");
            var coin = new VisualElement { pickingMode = PickingMode.Ignore };
            coin.AddToClassList("ae-coin");
            goldRow.Add(coin);
            _gold = new Label { pickingMode = PickingMode.Ignore };
            _gold.AddToClassList("ae-gold");
            goldRow.Add(_gold);
            panel.Add(goldRow);
            return panel;
        }

        private void RefreshItems() {
            if (_items == null) return;
            _items.Clear();
            for (int i = 0; i < Inventory.Capacity; i++) _items.Add(ItemSlot(i < _inventory.Count ? _inventory[i] : null, _tooltip));
            _elixir.text = _activeElixir != null ? _activeElixir.DisplayName : "";
            _elixir.style.display = _activeElixir != null ? DisplayStyle.Flex : DisplayStyle.None;
            _gold.text = _goldValue.ToString();
        }

        /// <summary>An item slot with its tooltip (HUD, shop inventory).</summary>
        public static VisualElement ItemSlot(ItemDefinition item, UiTooltip tooltip) {
            var slot = new VisualElement();
            slot.AddToClassList("ae-slot");
            slot.AddToClassList("ae-item");
            if (item == null) {
                slot.AddToClassList("ae-slot--empty");
                return slot;
            }
            if (item.Tier == ItemTier.Prismatic) slot.AddToClassList("ae-slot--prismatic");
            if (item.Icon != null) slot.style.backgroundImage = new StyleBackground(item.Icon);
            tooltip?.Attach(slot, () => ItemTooltip(item));
            return slot;
        }

        public static (string, string, string) ItemTooltip(ItemDefinition item) {
            string tier = item.Tier == ItemTier.Prismatic ? "Prismatique" : string.IsNullOrEmpty(item.UniqueGroup) ? "Légendaire" : item.UniqueGroup;
            string text = StatLabels.Format(item.Stats);
            if (item.Passive != null) text += $"\n\n{item.Passive.DisplayName} : {item.Passive.Description}";
            return (item.DisplayName, item.Cost > 0 ? $"{tier} · {item.Cost} or" : tier, text);
        }

        /// <summary>LoL health bar ticks: a thin line every 100 HP, a bolder one every 1000.</summary>
        private sealed class BarTicks : VisualElement {
            private float _max;

            public BarTicks() {
                pickingMode = PickingMode.Ignore;
                AddToClassList("ae-bar__ticks");
                generateVisualContent += Draw;
            }

            public void SetMax(float max) {
                if (Mathf.Approximately(max, _max)) return;
                _max = max;
                MarkDirtyRepaint();
            }

            private void Draw(MeshGenerationContext ctx) {
                if (_max <= 100f) return;
                Rect r = contentRect;
                Painter2D p = ctx.painter2D;
                int step = _max > 6000f ? 1000 : 100;   // huge pools: the 1000s only
                for (int hp = step; hp < _max && hp < 100000; hp += step) {
                    bool big = hp % 1000 == 0;
                    float x = r.width * hp / _max;
                    p.strokeColor = new Color(0f, 0f, 0f, big ? 0.85f : 0.55f);
                    p.lineWidth = big ? 2f : 1f;
                    p.BeginPath();
                    p.MoveTo(new Vector2(x, 0f));
                    p.LineTo(new Vector2(x, big ? r.height : r.height * 0.55f));
                    p.Stroke();
                }
            }
        }

        private void ShowFeedback(string message) {
            _feedbackUntil = Time.unscaledTime + feedbackDuration;
            if (_feedback != null) _feedback.text = message;
        }
    }
}
