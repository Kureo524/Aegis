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
    /// - top: title, search, gold, close;
    /// - left, three drawers that fold: Elixirs and stat anvils, Boots, My items (the inventory:
    ///   click an item for its details and its sell value);
    /// - the main panel: the categories as icons on top (all, recommended, marksman, mage…), the
    ///   stat filters as icons on its right side, the item anvils (prismatic, legendary of the
    ///   category's role) above the legendary items of the category;
    /// - right: the selected offer (stats, passive, unique group, why it cannot be bought, Buy), or
    ///   the selected owned item (Sell);
    /// - bottom: the break timer / "between waves only" during a wave, Start the wave.
    /// Click selects; double click or right click buys. Buying, selling and leaving are bus requests.
    /// Own UIDocument; anvils open above it (AnvilPanel).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ShopPanel : MonoBehaviour {
        private const float DoubleClickTime = 0.4f;

        private static readonly (ItemRole role, string label)[] Roles = {
            (ItemRole.Marksman, "Tireur"), (ItemRole.Mage, "Mage"), (ItemRole.Fighter, "Combattant"),
            (ItemRole.Assassin, "Assassin"), (ItemRole.Tank, "Tank"),
        };

        private static readonly (string label, StatType icon, StatType[] stats)[] StatFilters = {
            ("Attaque", StatType.PhysicalAttack, new[] { StatType.PhysicalAttack }),
            ("Critique", StatType.CritChance, new[] { StatType.CritChance, StatType.CritDamage }),
            ("Vitesse d'attaque", StatType.AttackSpeed, new[] { StatType.AttackSpeed }),
            ("Puissance magique", StatType.MagicAttack, new[] { StatType.MagicAttack }),
            ("Pénétration", StatType.ArmorPenPercent, new[] { StatType.ArmorPenFlat, StatType.ArmorPenPercent, StatType.MagicPenFlat, StatType.MagicPenPercent }),
            ("Mana", StatType.MaxMana, new[] { StatType.MaxMana, StatType.ManaRegen }),
            ("Santé", StatType.MaxHealth, new[] { StatType.MaxHealth, StatType.HealthRegen }),
            ("Armure", StatType.Armor, new[] { StatType.Armor }),
            ("Résistance magique", StatType.MagicResist, new[] { StatType.MagicResist }),
            ("Accélération de compétence", StatType.AbilityHaste, new[] { StatType.AbilityHaste }),
            ("Vitesse de déplacement", StatType.MoveSpeed, new[] { StatType.MoveSpeed }),
            ("Vol de vie", StatType.Lifesteal, new[] { StatType.Lifesteal, StatType.Omnivamp }),
        };

        /// <summary>The three drawers of the left column, top to bottom.</summary>
        private enum Drawer { Consumables, Boots, Owned }

        private IEventBus _eventBus;
        private UiTheme _theme;
        private readonly DisposableBag _subscriptions = new();
        private UiTooltip _tooltip;
        private VisualElement _overlay, _anvils, _grid, _detail, _categories, _statFilters;
        private readonly VisualElement[] _drawerBodies = new VisualElement[3];
        private readonly Button[] _drawerHeads = new Button[3];
        private readonly bool[] _folded = new bool[3];
        private Button _startWave;
        private TextField _search;
        private Label _gold, _status, _gridTitle;
        private readonly Dictionary<ScriptableObject, VisualElement> _cells = new();
        private readonly List<VisualElement> _slots = new();

        private bool _open;
        private ShopStateEvent _state;
        private IReadOnlyList<ItemDefinition> _items = new List<ItemDefinition>();
        private IReadOnlyList<ItemDefinition> _recommended = Array.Empty<ItemDefinition>();
        private string _champion = "";
        private int _goldValue;
        private RunStatusEvent _run;
        private bool _hasRun;
        private bool _showRecommended;
        private ItemRole _category = ItemRole.None;
        private int _statFilter = -1;
        private string _query = "";
        private ScriptableObject _selected;
        private int _selectedSlot = -1;
        private Dictionary<ScriptableObject, string> _refusals = new();
        private ScriptableObject _lastClicked;
        private float _lastClickTime;

        public bool IsOpen => _open;
        /// <summary>The offer shown in the details panel (tests).</summary>
        public ScriptableObject Selected => _selected;
        /// <summary>The inventory slot shown in the details panel (-1: an offer is shown).</summary>
        public int SelectedSlot => _selectedSlot;
        /// <summary>True when the "Recommended" category is shown.</summary>
        public bool ShowsRecommended => _showRecommended;
        /// <summary>The role of the category shown (None: every item).</summary>
        public ItemRole Category => _category;

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
                if (_recommended.Count > 0 && _selected == null) _showRecommended = true;
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
            body.Add(BuildDrawers());
            body.Add(BuildMain());
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

            _search = new TextField { value = "" };
            _search.AddToClassList("ae-shop__search");
            _search.textEdition.placeholder = "Rechercher un objet…";
            _search.RegisterValueChangedCallback(e => {
                _query = e.newValue ?? "";
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

        // ---- left column: three drawers that fold ----
        private VisualElement BuildDrawers() {
            var column = new ScrollView(ScrollViewMode.Vertical);
            column.AddToClassList("ae-shop__drawers");
            string[] titles = { "Élixirs & enclumes à stats", "Bottes", "Mes objets" };
            for (int i = 0; i < 3; i++) {
                int index = i;
                var drawer = new VisualElement();
                drawer.AddToClassList("ae-drawer");
                Button head = new Button(() => {
                    _folded[index] = !_folded[index];
                    Refresh();
                }) { text = titles[i] };
                head.AddToClassList("ae-drawer__head");
                head.AddToClassList("ae-shop__drawer-title");
                drawer.Add(head);
                var content = new VisualElement();
                content.AddToClassList("ae-drawer__body");
                drawer.Add(content);
                _drawerHeads[i] = head;
                _drawerBodies[i] = content;
                column.Add(drawer);
            }
            return column;
        }

        // ---- the main panel: categories on top, stat filters on the right, anvils then items ----
        private VisualElement BuildMain() {
            var main = new VisualElement();
            main.AddToClassList("ae-shop__main");

            _categories = new VisualElement();
            _categories.AddToClassList("ae-shop__categories");
            _categories.Add(IconButton(_theme != null ? _theme.Icon(ItemRole.None) : null, "Tous les objets", () => SetCategory(ItemRole.None, false),
                                       "category-all"));
            _categories.Add(IconButton(_theme != null ? _theme.RecommendedIcon : null, "Recommandés", () => SetCategory(ItemRole.None, true),
                                       "category-recommended"));
            foreach ((ItemRole role, string label) in Roles)
                _categories.Add(IconButton(_theme != null ? _theme.Icon(role) : null, label, () => SetCategory(role, false), role));
            main.Add(_categories);

            var row = new VisualElement();
            row.AddToClassList("ae-shop__main-row");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("ae-shop__grid");
            var anvilsTitle = new Label("Enclumes à objets");
            anvilsTitle.AddToClassList("ae-shop__section");
            scroll.Add(anvilsTitle);
            _anvils = new VisualElement();
            _anvils.AddToClassList("ae-shop__row");
            _anvils.AddToClassList("ae-shop__anvils");
            scroll.Add(_anvils);
            _gridTitle = new Label();
            _gridTitle.AddToClassList("ae-shop__section");
            scroll.Add(_gridTitle);
            _grid = new VisualElement();
            _grid.AddToClassList("ae-shop__row");
            scroll.Add(_grid);
            row.Add(scroll);

            _statFilters = new VisualElement();
            _statFilters.AddToClassList("ae-shop__stats");
            _statFilters.Add(IconButton(_theme != null ? _theme.NoFilterIcon : null, "Aucun filtre", () => SetStatFilter(-1), -1));
            for (int i = 0; i < StatFilters.Length; i++) {
                int index = i;
                _statFilters.Add(IconButton(_theme != null ? _theme.Icon(StatFilters[i].icon) : null, StatFilters[i].label, () => SetStatFilter(index), index));
            }
            row.Add(_statFilters);
            main.Add(row);
            return main;
        }

        private Button IconButton(Sprite icon, string label, Action onClick, object key) {
            var button = new Button(onClick) { userData = key };
            button.AddToClassList("ae-iconbutton");
            var image = new VisualElement { pickingMode = PickingMode.Ignore };
            image.AddToClassList("ae-iconbutton__icon");
            if (icon != null) image.style.backgroundImage = new StyleBackground(icon);
            button.Add(image);
            _tooltip.Attach(button, () => (label, "", null));
            return button;
        }

        private VisualElement BuildFooter() {
            var footer = new VisualElement();
            footer.AddToClassList("ae-shop__footer");
            _status = new Label();
            _status.AddToClassList("ae-shop__status");
            footer.Add(_status);
            var hint = new Label("Double-clic ou clic droit : acheter");
            hint.AddToClassList("ae-shop__hint");
            footer.Add(hint);
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

        /// <summary>A category of the top bar: a role (None: every item) or the champion's recommendations.</summary>
        public void SetCategory(ItemRole role, bool recommended = false) {
            _category = recommended ? ItemRole.None : role;
            _showRecommended = recommended && _recommended.Count > 0;
            Refresh();
        }

        /// <summary>A stat filter of the right bar (-1: none).</summary>
        public void SetStatFilter(int index) {
            _statFilter = index >= 0 && index < StatFilters.Length ? index : -1;
            if (_statFilter >= 0) _showRecommended = false;
            Refresh();
        }

        /// <summary>Every item, no filter nor search (tests, shortcuts).</summary>
        public void ShowAllItems() {
            _statFilter = -1;
            _query = "";
            _search?.SetValueWithoutNotify("");
            SetCategory(ItemRole.None);
        }

        /// <summary>Selects an offer (click), shown in the details panel.</summary>
        public void Select(ScriptableObject offer) {
            _selected = offer;
            _selectedSlot = -1;
            RefreshSelection();
        }

        /// <summary>Selects an owned item ("My items" click): details and its sell value.</summary>
        public void SelectSlot(int slot) {
            _selectedSlot = slot >= 0 && slot < _items.Count ? slot : -1;
            RefreshSelection();
        }

        private void Buy(ScriptableObject offer) => _eventBus.Publish(new ShopBuyRequestedEvent(offer));

        private void Refresh() {
            if (_overlay == null) return;
            _overlay.style.display = _open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_open) return;

            _gold.text = _goldValue.ToString();
            foreach (VisualElement child in _categories.Children()) {
                bool active = child.userData switch {
                    "category-all" => !_showRecommended && _category == ItemRole.None,
                    "category-recommended" => _showRecommended,
                    ItemRole role => !_showRecommended && _category == role,
                    _ => false,
                };
                child.EnableInClassList("ae-iconbutton--active", active);
                if ("category-recommended".Equals(child.userData)) child.style.display = _recommended.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            foreach (VisualElement child in _statFilters.Children())
                child.EnableInClassList("ae-iconbutton--active", child.userData is int f && f == _statFilter);
            for (int i = 0; i < 3; i++) {
                _drawerHeads[i].EnableInClassList("ae-drawer__head--folded", _folded[i]);
                _drawerBodies[i].style.display = _folded[i] ? DisplayStyle.None : DisplayStyle.Flex;
            }
            RefreshStatus();

            _cells.Clear();
            _anvils.Clear();
            _grid.Clear();
            _drawerBodies[(int)Drawer.Consumables].Clear();
            _drawerBodies[(int)Drawer.Boots].Clear();
            RefreshOwnedItems();
            if (_state.Offers == null) return;
            _refusals = new Dictionary<ScriptableObject, string>();
            for (int i = 0; i < _state.Offers.Count; i++) _refusals[_state.Offers[i]] = _state.Refusals[i];
            if (_selected != null && !_refusals.ContainsKey(_selected)) _selected = null;

            var shown = new List<ScriptableObject>();
            // Left: elixirs and stat anvils, then boots
            Fill(_drawerBodies[(int)Drawer.Consumables],
                 _state.Offers.Where(o => o is ElixirDefinition || o is AnvilDefinition { Kind: AnvilKind.Stat }).Where(MatchesSearch), shown);
            List<ItemDefinition> items = _state.Offers.OfType<ItemDefinition>().Where(MatchesSearch).OrderBy(i => i.Cost).ThenBy(i => i.DisplayName).ToList();
            Fill(_drawerBodies[(int)Drawer.Boots], items.Where(IsBoots).Where(MatchesStat), shown);
            for (int d = 0; d < 2; d++)
                if (_drawerBodies[d].childCount == 0) _drawerBodies[d].Add(MenuStyles.Hint("Rien avec ce filtre."));

            // Main: the item anvils of the category, then its legendary items
            Fill(_anvils, _state.Offers.OfType<AnvilDefinition>().Where(a => a.Kind == AnvilKind.Item && MatchesCategory(a) && MatchesSearch(a)), shown);
            IEnumerable<ItemDefinition> legendaries;
            if (_showRecommended) {
                legendaries = _recommended.Where(i => i != null && _refusals.ContainsKey(i));
                _gridTitle.text = string.IsNullOrEmpty(_champion) ? "Recommandés" : $"Recommandés pour {_champion}";
            }
            else {
                legendaries = items.Where(i => !IsBoots(i) && (_category == ItemRole.None || (i.Roles & _category) != 0)).Where(MatchesStat);
                _gridTitle.text = CategoryTitle();
            }
            Fill(_grid, legendaries, shown);
            if (_grid.childCount == 0)
                _grid.Add(MenuStyles.Hint(_query.Length > 0 ? $"Aucun objet ne correspond à « {_query} »." : "Rien dans cette catégorie."));
            if (_selectedSlot < 0) _selected ??= shown.FirstOrDefault();
            RefreshSelection();
        }

        private string CategoryTitle() {
            string title = "Objets légendaires";
            foreach ((ItemRole role, string label) in Roles)
                if (role == _category) title += $" · {label}";
            if (_statFilter >= 0) title += $" · {StatFilters[_statFilter].label}";
            return title;
        }

        private void Fill(VisualElement container, IEnumerable<ScriptableObject> offers, List<ScriptableObject> shown) {
            foreach (ScriptableObject offer in offers) {
                VisualElement cell = OfferIcon(offer, _refusals[offer]);
                _cells[offer] = cell;
                container.Add(cell);
                shown.Add(offer);
            }
        }

        private static bool IsBoots(ItemDefinition item) => item.UniqueGroup == "Bottes";

        // The legendary anvils follow the category (all of them under "every item"); prismatic ones always show
        private bool MatchesCategory(AnvilDefinition anvil) =>
            anvil.Role == ItemRole.None || _showRecommended || _category == ItemRole.None || (anvil.Role & _category) != 0;

        private bool MatchesStat(ItemDefinition item) =>
            _statFilter < 0 || item.Stats.Any(s => StatFilters[_statFilter].stats.Contains(s.Stat));

        private bool MatchesSearch(ScriptableObject offer) {
            if (_query.Length == 0) return true;
            (string name, Color _, Sprite _, string _) = OfferCard.Describe(offer);
            return Fold(name).IndexOf(Fold(_query), StringComparison.Ordinal) >= 0;
        }

        // "My items": the 6 slots; click shows the item and its sell value
        private void RefreshOwnedItems() {
            VisualElement body = _drawerBodies[(int)Drawer.Owned];
            body.Clear();
            _slots.Clear();
            for (int i = 0; i < Inventory.Capacity; i++) {
                int slot = i;
                VisualElement cell = PlayerHud.ItemSlot(i < _items.Count ? _items[i] : null, _tooltip);
                cell.AddToClassList("ae-shop__owned");
                if (i < _items.Count) cell.RegisterCallback<ClickEvent>(_ => SelectSlot(slot));
                body.Add(cell);
                _slots.Add(cell);
            }
        }

        // Selection only touches the highlight and the details: the cells stay (double click works on them)
        private void RefreshSelection() {
            if (_overlay == null || !_open) return;
            foreach ((ScriptableObject offer, VisualElement cell) in _cells)
                cell.Q(className: "ae-offer__icon")?.EnableInClassList("ae-slot--selected", offer == _selected && _selectedSlot < 0);
            for (int i = 0; i < _slots.Count; i++) _slots[i].EnableInClassList("ae-slot--selected", i == _selectedSlot);
            if (_selectedSlot >= 0 && _selectedSlot < _items.Count) RefreshOwned(_items[_selectedSlot], _selectedSlot);
            else RefreshDetail(_selected, _selected != null && _refusals.TryGetValue(_selected, out string r) ? r : null);
        }

        private void RefreshStatus() {
            if (_status == null) return;
            bool breakTime = _state.CanBuy && _hasRun && _run.State == RunState.PreWave;
            if (!_state.CanBuy) _status.text = ShopService.BetweenWavesOnly + " : prépare ton build en attendant.";
            else _status.text = breakTime ? $"Achats ouverts · prochaine vague dans {Mathf.CeilToInt(_run.TimeLeft)} s" : "Achats ouverts";
            _status.EnableInClassList("ae-shop__status--closed", !_state.CanBuy);
            _startWave.style.display = breakTime ? DisplayStyle.Flex : DisplayStyle.None;
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

            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.AddToClassList("ae-slot");
            frame.AddToClassList("ae-offer__icon");
            if (icon != null) frame.style.backgroundImage = new StyleBackground(icon);
            cell.Add(frame);
            var price = new Label(cost > 0 ? cost.ToString() : "—") { pickingMode = PickingMode.Ignore };
            price.AddToClassList("ae-offer__price");
            cell.Add(price);

            // Own double click (by offer and time): a refresh between the two clicks keeps it working
            cell.RegisterCallback<PointerDownEvent>(e => {
                if (e.button == 1) {
                    Buy(offer);   // right click buys
                    return;
                }
                if (e.button != 0) return;
                float now = Time.realtimeSinceStartup;
                bool twice = _lastClicked == offer && now - _lastClickTime <= DoubleClickTime;
                _lastClicked = twice ? null : offer;
                _lastClickTime = now;
                if (twice) Buy(offer);
                else Select(offer);
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
            foreach ((ItemRole role, string label) in Roles)
                if ((roles & role) != 0) names.Add(label);
            return string.Join(" · ", names);
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
