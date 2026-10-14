using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Features.Economy;
using Features.Events;
using Features.Run;
using Features.Stats;
using Systems.EventBusses;
using Systems.Inputs.Events;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI.Panels {
    /// <summary>
    /// The shop screen (design doc §9.3), open at any time (shop key, HUD button; it also opens by
    /// itself at the start of every break), drawn from the ShopService snapshots only:
    /// - top: Recommended / All items tabs, a search field, the gold, close;
    /// - left: filters by role (marksman, mage…) and by stat;
    /// - center: the offers by section (Consumables, Anvils, Boots, Legendary items), icon + price,
    ///   greyed when refused, price in red when too expensive;
    /// - right: the selected offer (stats, passive, unique group, why it cannot be bought, Buy), or
    ///   the selected inventory item with its sell value (Sell);
    /// - bottom: the inventory, the break timer / "between waves only" during a wave, Start the wave.
    /// Click selects; double click or right click buys. Buying, selling and leaving are bus requests.
    /// Own UIDocument; anvils open above it (AnvilPanel).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ShopPanel : MonoBehaviour {
        private enum Tab { Recommended, All }

        private enum Filter {
            All, Marksman, Mage, Fighter, Assassin, Tank,
            Attack, Critical, AttackSpeed, Magic, Penetration, Mana, Health, Armor, MagicResist, Haste, MoveSpeed, Lifesteal,
        }

        private static readonly (Filter filter, string label, ItemRole role)[] Roles = {
            (Filter.All, "Tous", ItemRole.None), (Filter.Marksman, "Tireur", ItemRole.Marksman), (Filter.Mage, "Mage", ItemRole.Mage),
            (Filter.Fighter, "Combattant", ItemRole.Fighter), (Filter.Assassin, "Assassin", ItemRole.Assassin), (Filter.Tank, "Tank", ItemRole.Tank),
        };

        private static readonly (Filter filter, string label, StatType icon, StatType[] stats)[] StatFilters = {
            (Filter.Attack, "Attaque", StatType.PhysicalAttack, new[] { StatType.PhysicalAttack }),
            (Filter.Critical, "Critique", StatType.CritChance, new[] { StatType.CritChance, StatType.CritDamage }),
            (Filter.AttackSpeed, "Vitesse d'attaque", StatType.AttackSpeed, new[] { StatType.AttackSpeed }),
            (Filter.Magic, "Puissance magique", StatType.MagicAttack, new[] { StatType.MagicAttack }),
            (Filter.Penetration, "Pénétration", StatType.ArmorPenPercent, new[] { StatType.ArmorPenFlat, StatType.ArmorPenPercent, StatType.MagicPenFlat, StatType.MagicPenPercent }),
            (Filter.Mana, "Mana", StatType.MaxMana, new[] { StatType.MaxMana, StatType.ManaRegen }),
            (Filter.Health, "Santé", StatType.MaxHealth, new[] { StatType.MaxHealth, StatType.HealthRegen }),
            (Filter.Armor, "Armure", StatType.Armor, new[] { StatType.Armor }),
            (Filter.MagicResist, "Résistance magique", StatType.MagicResist, new[] { StatType.MagicResist }),
            (Filter.Haste, "Accélération", StatType.AbilityHaste, new[] { StatType.AbilityHaste }),
            (Filter.MoveSpeed, "Vitesse de déplacement", StatType.MoveSpeed, new[] { StatType.MoveSpeed }),
            (Filter.Lifesteal, "Vol de vie", StatType.Lifesteal, new[] { StatType.Lifesteal, StatType.Omnivamp }),
        };

        private IEventBus _eventBus;
        private UiTheme _theme;
        private readonly DisposableBag _subscriptions = new();
        private UiTooltip _tooltip;
        private VisualElement _overlay, _grid, _inventory, _detail, _filters;
        private Button _recommendedTab, _allTab, _startWave;
        private TextField _search;
        private Label _gold, _status;

        private bool _open;
        private ShopStateEvent _state;
        private IReadOnlyList<ItemDefinition> _items = new List<ItemDefinition>();
        private IReadOnlyList<ItemDefinition> _recommended = Array.Empty<ItemDefinition>();
        private string _champion = "";
        private int _goldValue;
        private RunStatusEvent _run;
        private bool _hasRun;
        private Tab _tab = Tab.All;
        private Filter _filter = Filter.All;
        private string _query = "";
        private ScriptableObject _selected;
        private int _selectedSlot = -1;

        public bool IsOpen => _open;
        /// <summary>The offer shown in the details panel (tests).</summary>
        public ScriptableObject Selected => _selected;
        /// <summary>The inventory slot shown in the details panel (-1: an offer is shown).</summary>
        public int SelectedSlot => _selectedSlot;

        [Inject]
        public void Construct(IEventBus eventBus, UiTheme theme) {
            _eventBus = eventBus;
            _theme = theme;
            _subscriptions.Add(eventBus.Subscribe<ShopStateEvent>(e => { _state = e; Refresh(); }));
            _subscriptions.Add(eventBus.Subscribe<GoldChangedEvent>(e => { _goldValue = e.Gold; Refresh(); }));
            _subscriptions.Add(eventBus.Subscribe<InventoryChangedEvent>(e => {
                _items = e.Items;
                if (_selectedSlot >= _items.Count) _selectedSlot = -1;
                Refresh();
            }));
            _subscriptions.Add(eventBus.Subscribe<PlayerChampionEvent>(e => {
                _recommended = e.RecommendedItems;
                _champion = e.Name;
                if (_recommended.Count > 0 && _selected == null) _tab = Tab.Recommended;
                Refresh();
            }));
            _subscriptions.Add(eventBus.Subscribe<RunStatusEvent>(e => {
                _run = e;
                _hasRun = true;
                RefreshStatus();
            }));
            _subscriptions.Add(eventBus.Subscribe<RunStateChangedEvent>(e => {
                // Every break after a wave opens the shop by itself
                if (e.To == RunState.PreWave && e.From == RunState.Reward) Show(true);
            }));
            _subscriptions.Add(eventBus.Subscribe<ShopKeyEvent>(_ => Show(!_open)));
            _subscriptions.Add(eventBus.Subscribe<ShopToggleRequestedEvent>(_ => Show(!_open)));
            _subscriptions.Add(eventBus.Subscribe<ShopCloseRequestedEvent>(_ => Show(false)));
        }

        private void Start() {
            _tooltip = new UiTooltip();
            _overlay = MenuStyles.Overlay(out VisualElement box);
            _overlay.AddToClassList("ae-shop-overlay");
            box.AddToClassList("ae-shop");
            box.style.width = StyleKeyword.Null;

            box.Add(BuildHeader());

            var body = new VisualElement();
            body.AddToClassList("ae-shop__body");
            _filters = new VisualElement();
            _filters.AddToClassList("ae-shop__filters");
            BuildFilters();
            body.Add(_filters);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("ae-shop__grid");
            _grid = new VisualElement();
            scroll.Add(_grid);
            body.Add(scroll);

            _detail = new VisualElement();
            _detail.AddToClassList("ae-shop__detail");
            body.Add(_detail);
            box.Add(body);

            box.Add(BuildFooter());

            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            root.Add(_overlay);
            root.Add(_tooltip.Root);
            Refresh();
        }

        private VisualElement BuildHeader() {
            var header = new VisualElement();
            header.AddToClassList("ae-shop__header");
            Label title = MenuStyles.Title("Boutique");
            title.AddToClassList("ae-shop__title");
            header.Add(title);

            var tabs = new VisualElement();
            tabs.AddToClassList("ae-shop__tabs");
            _recommendedTab = new Button(() => SetTab(Tab.Recommended)) { text = "Recommandés" };
            _allTab = new Button(() => SetTab(Tab.All)) { text = "Tous les objets" };
            _recommendedTab.AddToClassList("ae-tab");
            _allTab.AddToClassList("ae-tab");
            tabs.Add(_recommendedTab);
            tabs.Add(_allTab);
            header.Add(tabs);

            _search = new TextField { value = "" };
            _search.AddToClassList("ae-shop__search");
            _search.textEdition.placeholder = "Rechercher un objet…";
            _search.RegisterValueChangedCallback(e => {
                _query = e.newValue ?? "";
                if (_query.Length > 0) _tab = Tab.All;
                Refresh();
            });
            header.Add(_search);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            header.Add(spacer);

            var goldRow = new VisualElement();
            goldRow.AddToClassList("ae-goldrow");
            var coin = new VisualElement();
            coin.AddToClassList("ae-coin");
            goldRow.Add(coin);
            _gold = new Label();
            _gold.AddToClassList("ae-gold");
            _gold.style.fontSize = 22;
            goldRow.Add(_gold);
            header.Add(goldRow);

            Button close = MenuStyles.Button("✕", () => Show(false));
            close.AddToClassList("ae-shop__close");
            header.Add(close);
            return header;
        }

        private void BuildFilters() {
            _filters.Add(Section("Rôles"));
            foreach ((Filter filter, string label, ItemRole _) in Roles) _filters.Add(FilterButton(filter, label, null));
            _filters.Add(Section("Statistiques"));
            foreach ((Filter filter, string label, StatType icon, StatType[] _) in StatFilters)
                _filters.Add(FilterButton(filter, label, _theme != null ? _theme.Icon(icon) : null));
        }

        private static Label Section(string text) {
            var label = new Label(text);
            label.AddToClassList("ae-shop__filter-title");
            return label;
        }

        private Button FilterButton(Filter filter, string label, Sprite icon) {
            var button = new Button(() => { _filter = filter; _tab = Tab.All; Refresh(); });
            button.AddToClassList("ae-filter");
            button.userData = filter;
            if (icon != null) {
                var image = new VisualElement { pickingMode = PickingMode.Ignore };
                image.AddToClassList("ae-filter__icon");
                image.style.backgroundImage = new StyleBackground(icon);
                button.Add(image);
            }
            button.Add(new Label(label) { pickingMode = PickingMode.Ignore });
            return button;
        }

        private VisualElement BuildFooter() {
            var footer = new VisualElement();
            footer.AddToClassList("ae-shop__footer");
            var inventoryBox = new VisualElement();
            inventoryBox.AddToClassList("ae-shop__inventory-box");
            inventoryBox.Add(Section("Inventaire"));
            _inventory = new VisualElement();
            _inventory.AddToClassList("ae-shop__inventory");
            inventoryBox.Add(_inventory);
            footer.Add(inventoryBox);

            _status = new Label();
            _status.AddToClassList("ae-shop__status");
            footer.Add(_status);

            _startWave = MenuStyles.Button("Lancer la vague ▶", () => _eventBus.Publish(new NextWaveRequestedEvent()));
            _startWave.AddToClassList("ae-button--primary");
            _startWave.AddToClassList("ae-shop__start");
            footer.Add(_startWave);
            return footer;
        }

        private void OnDestroy() => _subscriptions.Dispose();

        public void Show(bool open) {
            if (_open == open) return;
            _open = open;
            if (!open) _tooltip?.Hide();
            _eventBus?.Publish(new ShopVisibilityChangedEvent(open));
            Refresh();
        }

        private void SetTab(Tab tab) {
            _tab = tab;
            if (tab == Tab.Recommended) {
                _filter = Filter.All;
                _query = "";
                _search?.SetValueWithoutNotify("");
            }
            Refresh();
        }

        /// <summary>The "All items" tab with no filter nor search (tests, shortcuts).</summary>
        public void ShowAllItems() {
            _filter = Filter.All;
            _query = "";
            _search?.SetValueWithoutNotify("");
            SetTab(Tab.All);
        }

        /// <summary>True when the "Recommended" tab is shown.</summary>
        public bool ShowsRecommended => _tab == Tab.Recommended;

        /// <summary>Selects an offer (click), shown in the details panel.</summary>
        public void Select(ScriptableObject offer) {
            _selected = offer;
            _selectedSlot = -1;
            Refresh();
        }

        /// <summary>Selects an owned item (inventory click): details and its sell value.</summary>
        public void SelectSlot(int slot) {
            _selectedSlot = slot >= 0 && slot < _items.Count ? slot : -1;
            Refresh();
        }

        private void Buy(ScriptableObject offer) => _eventBus.Publish(new ShopBuyRequestedEvent(offer));

        private void Refresh() {
            if (_overlay == null) return;
            _overlay.style.display = _open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_open) return;

            _gold.text = _goldValue.ToString();
            _recommendedTab.EnableInClassList("ae-tab--active", _tab == Tab.Recommended);
            _allTab.EnableInClassList("ae-tab--active", _tab == Tab.All);
            _recommendedTab.style.display = _recommended.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (VisualElement child in _filters.Children())
                if (child.userData is Filter f) child.EnableInClassList("ae-filter--active", _tab == Tab.All && f == _filter);

            RefreshInventory();
            RefreshStatus();

            _grid.Clear();
            if (_state.Offers == null) return;
            var refusals = new Dictionary<ScriptableObject, string>();
            for (int i = 0; i < _state.Offers.Count; i++) refusals[_state.Offers[i]] = _state.Refusals[i];
            if (_selected != null && !refusals.ContainsKey(_selected)) _selected = null;

            var shown = new List<ScriptableObject>();
            if (_tab == Tab.Recommended) {
                List<ScriptableObject> picks = _recommended.Where(i => i != null && refusals.ContainsKey(i)).Cast<ScriptableObject>().ToList();
                AddSection(string.IsNullOrEmpty(_champion) ? "Recommandés" : $"Recommandés pour {_champion}", picks, refusals, shown);
                AddSection("Consommables", _state.Offers.OfType<ElixirDefinition>().Cast<ScriptableObject>().ToList(), refusals, shown);
            }
            else {
                AddSection("Consommables", _state.Offers.OfType<ElixirDefinition>().Where(Matches).Cast<ScriptableObject>().ToList(), refusals, shown);
                AddSection("Enclumes", _state.Offers.OfType<AnvilDefinition>().Where(Matches).Cast<ScriptableObject>().ToList(), refusals, shown);
                List<ItemDefinition> items = _state.Offers.OfType<ItemDefinition>().Where(Matches).OrderBy(i => i.Cost).ThenBy(i => i.DisplayName).ToList();
                AddSection("Bottes", items.Where(IsBoots).Cast<ScriptableObject>().ToList(), refusals, shown);
                AddSection("Objets légendaires", items.Where(i => !IsBoots(i)).Cast<ScriptableObject>().ToList(), refusals, shown);
            }
            if (shown.Count == 0) _grid.Add(MenuStyles.Hint(_query.Length > 0 ? $"Aucun objet ne correspond à « {_query} »." : "Rien dans cette catégorie."));

            if (_selectedSlot >= 0) RefreshOwned(_items[_selectedSlot], _selectedSlot);
            else {
                _selected ??= shown.FirstOrDefault();
                RefreshDetail(_selected, _selected != null && refusals.TryGetValue(_selected, out string r) ? r : null);
            }
        }

        private static bool IsBoots(ItemDefinition item) => item.UniqueGroup == "Bottes";

        private void RefreshInventory() {
            _inventory.Clear();
            for (int i = 0; i < Inventory.Capacity; i++) {
                int slot = i;
                VisualElement cell = PlayerHud.ItemSlot(i < _items.Count ? _items[i] : null, _tooltip);
                cell.EnableInClassList("ae-slot--selected", slot == _selectedSlot);
                if (i < _items.Count) cell.RegisterCallback<ClickEvent>(_ => SelectSlot(slot));
                _inventory.Add(cell);
            }
        }

        private void RefreshStatus() {
            if (_status == null) return;
            bool breakTime = _state.CanBuy && _hasRun && _run.State == RunState.PreWave;
            if (!_state.CanBuy) _status.text = ShopService.BetweenWavesOnly + " : prépare ton build en attendant.";
            else _status.text = breakTime ? $"Achats ouverts · prochaine vague dans {Mathf.CeilToInt(_run.TimeLeft)} s" : "Achats ouverts";
            _status.EnableInClassList("ae-shop__status--closed", !_state.CanBuy);
            _startWave.style.display = breakTime ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void AddSection(string title, List<ScriptableObject> offers, Dictionary<ScriptableObject, string> refusals, List<ScriptableObject> shown) {
            if (offers.Count == 0) return;
            var label = new Label(title);
            label.AddToClassList("ae-shop__section");
            _grid.Add(label);
            var row = new VisualElement();
            row.AddToClassList("ae-shop__row");
            foreach (ScriptableObject offer in offers) {
                row.Add(OfferIcon(offer, refusals[offer]));
                shown.Add(offer);
            }
            _grid.Add(row);
        }

        private VisualElement OfferIcon(ScriptableObject offer, string refusal) {
            (string name, Color _, Sprite icon, string _) = OfferCard.Describe(offer);
            int cost = OfferCard.Cost(offer);
            bool poor = refusal == "Pas assez d'or";
            var cell = new VisualElement();
            cell.AddToClassList("ae-offer");
            cell.EnableInClassList("ae-offer--refused", refusal != null && !poor && refusal != ShopService.BetweenWavesOnly);
            cell.EnableInClassList("ae-offer--poor", poor);
            cell.EnableInClassList("ae-offer--owned", offer is ItemDefinition owned && _items.Contains(owned));

            var frame = new VisualElement();
            frame.AddToClassList("ae-slot");
            frame.AddToClassList("ae-offer__icon");
            frame.EnableInClassList("ae-slot--selected", offer == _selected && _selectedSlot < 0);
            if (icon != null) frame.style.backgroundImage = new StyleBackground(icon);
            cell.Add(frame);
            var price = new Label(cost > 0 ? cost.ToString() : "—");
            price.AddToClassList("ae-offer__price");
            cell.Add(price);

            cell.RegisterCallback<ClickEvent>(e => {
                if (e.clickCount >= 2) Buy(offer);
                else Select(offer);
            });
            cell.RegisterCallback<PointerDownEvent>(e => {
                if (e.button == 1) Buy(offer);   // right click buys
            });
            _tooltip.Attach(cell, () => (name, cost > 0 ? $"{cost} or" : "", refusal));
            return cell;
        }

        private VisualElement DetailHead(string name, Color color, Sprite icon, string price, bool prismatic) {
            var head = new VisualElement();
            head.AddToClassList("ae-detail__head");
            var image = new VisualElement();
            image.AddToClassList("ae-slot");
            image.AddToClassList("ae-detail__icon");
            if (prismatic) image.AddToClassList("ae-slot--prismatic");
            if (icon != null) image.style.backgroundImage = new StyleBackground(icon);
            head.Add(image);
            var titles = new VisualElement();
            titles.style.flexShrink = 1;
            var title = new Label(name);
            title.AddToClassList("ae-detail__name");
            title.style.color = color;
            titles.Add(title);
            var cost = new Label(price);
            cost.AddToClassList("ae-gold");
            titles.Add(cost);
            head.Add(titles);
            return head;
        }

        private void AddItemBody(ItemDefinition item) {
            string roles = RoleNames(item.Roles);
            if (roles.Length > 0) {
                var tags = new Label(roles);
                tags.AddToClassList("ae-detail__tags");
                _detail.Add(tags);
            }
            var stats = new Label(StatLabels.Format(item.Stats));
            stats.AddToClassList("ae-detail__stats");
            _detail.Add(stats);
            if (item.Passive != null) {
                var passive = new Label($"<b>{item.Passive.DisplayName}</b> : {item.Passive.Description}");
                passive.AddToClassList("ae-detail__text");
                _detail.Add(passive);
            }
            if (!string.IsNullOrEmpty(item.UniqueGroup)) {
                var group = new Label($"Un seul objet « {item.UniqueGroup} »");
                group.AddToClassList("ae-dim");
                group.style.fontSize = 11;
                group.style.marginTop = 6;
                _detail.Add(group);
            }
        }

        private void RefreshDetail(ScriptableObject offer, string refusal) {
            _detail.Clear();
            if (offer == null) {
                _detail.Add(MenuStyles.Hint("Clique sur un objet pour voir ses détails. Double-clic ou clic droit : acheter."));
                return;
            }
            (string name, Color color, Sprite icon, string body) = OfferCard.Describe(offer);
            int cost = OfferCard.Cost(offer);
            _detail.Add(DetailHead(name, color, icon, cost > 0 ? $"{cost} or" : "Gratuit", offer is ItemDefinition { Tier: ItemTier.Prismatic }));

            if (offer is ItemDefinition item) {
                AddItemBody(item);
                var resale = new Label($"Revente : {item.SellValue} or");
                resale.AddToClassList("ae-dim");
                resale.style.fontSize = 11;
                resale.style.marginTop = 4;
                _detail.Add(resale);
            }
            else {
                var text = new Label(body);
                text.AddToClassList("ae-detail__stats");
                _detail.Add(text);
            }

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            _detail.Add(spacer);
            if (refusal != null) {
                var why = new Label(refusal);
                why.AddToClassList("ae-detail__refusal");
                _detail.Add(why);
            }
            Button buy = MenuStyles.Button("Acheter", () => Buy(offer));
            buy.AddToClassList("ae-button--primary");
            buy.SetEnabled(refusal == null);
            _detail.Add(buy);
        }

        private void RefreshOwned(ItemDefinition item, int slot) {
            _detail.Clear();
            (string name, Color color, Sprite icon, string _) = OfferCard.Describe(item);
            _detail.Add(DetailHead(name, color, icon, "Dans ton inventaire", item.Tier == ItemTier.Prismatic));
            AddItemBody(item);
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            _detail.Add(spacer);
            if (!_state.CanBuy) {
                var why = new Label(ShopService.BetweenWavesOnly);
                why.AddToClassList("ae-detail__refusal");
                _detail.Add(why);
            }
            Button sell = MenuStyles.Button($"Vendre · {item.SellValue} or", () => _eventBus.Publish(new ShopSellRequestedEvent(slot)));
            sell.AddToClassList("ae-button--sell");
            sell.SetEnabled(_state.CanBuy);
            _detail.Add(sell);
            Button back = MenuStyles.Button("Retour à la boutique", () => SelectSlot(-1));
            back.style.marginTop = 4;
            _detail.Add(back);
        }

        private static string RoleNames(ItemRole roles) {
            var names = new List<string>();
            foreach ((Filter _, string label, ItemRole role) in Roles)
                if (role != ItemRole.None && (roles & role) != 0) names.Add(label);
            return string.Join(" · ", names);
        }

        private bool Matches(ScriptableObject offer) {
            (string name, Color _, Sprite _, string _) = OfferCard.Describe(offer);
            if (_query.Length > 0 && Fold(name).IndexOf(Fold(_query), StringComparison.Ordinal) < 0) return false;
            if (_filter == Filter.All) return true;
            if (offer is not ItemDefinition item) return false;
            foreach ((Filter f, string _, ItemRole role) in Roles)
                if (f == _filter) return (item.Roles & role) != 0;
            foreach ((Filter f, string _, StatType _, StatType[] stats) in StatFilters)
                if (f == _filter) return item.Stats.Any(s => stats.Contains(s.Stat));
            return false;
        }

        // Search ignores case and accents ("epee" finds "Épée")
        private static string Fold(string text) {
            string decomposed = (text ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString();
        }
    }
}
