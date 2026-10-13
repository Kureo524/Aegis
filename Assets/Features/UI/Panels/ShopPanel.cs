using System;
using System.Collections.Generic;
using System.Linq;
using Features.Economy;
using Features.Events;
using Features.Stats;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI.Panels {
    /// <summary>
    /// Shop screen, LoL style (design doc §9.3), drawn from the ShopService snapshots only
    /// (ShopStateEvent): filters by stat on the left, a grid of icons with their price (greyed when
    /// refused, red price when too expensive), the details of the selected offer on the right
    /// (stats, passive, why it cannot be bought, Buy), the inventory and Continue at the bottom.
    /// Click selects; double click or right click buys. Buying and leaving are bus requests.
    /// Own UIDocument; anvils open above it (AnvilPanel).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ShopPanel : MonoBehaviour {
        private enum Filter { All, Attack, Critical, AttackSpeed, Magic, Mana, Health, Armor, MagicResist, Haste, Boots, Consumables }

        private static readonly (Filter filter, string label)[] Filters = {
            (Filter.All, "Tous"), (Filter.Attack, "Attaque"), (Filter.Critical, "Critique"), (Filter.AttackSpeed, "Vitesse d'attaque"),
            (Filter.Magic, "Puissance magique"), (Filter.Mana, "Mana"), (Filter.Health, "Santé"), (Filter.Armor, "Armure"),
            (Filter.MagicResist, "Résistance magique"), (Filter.Haste, "Accélération"), (Filter.Boots, "Bottes"), (Filter.Consumables, "Élixirs et enclumes"),
        };

        private IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();
        private UiTooltip _tooltip;
        private VisualElement _overlay, _grid, _inventory, _detail, _filters;
        private Label _title, _gold, _limits;
        private ShopStateEvent _state;
        private IReadOnlyList<ItemDefinition> _items = new List<ItemDefinition>();
        private int _goldValue;
        private Filter _filter = Filter.All;
        private ScriptableObject _selected;

        public bool IsOpen => _state.Open;
        /// <summary>The offer shown in the details panel (tests).</summary>
        public ScriptableObject Selected => _selected;

        [Inject]
        public void Construct(IEventBus eventBus) {
            _eventBus = eventBus;
            _subscriptions.Add(eventBus.Subscribe<ShopStateEvent>(e => {
                if (!_state.Open && e.Open) _selected = null;   // a new shop starts without selection
                _state = e;
                Refresh();
            }));
            _subscriptions.Add(eventBus.Subscribe<GoldChangedEvent>(e => {
                _goldValue = e.Gold;
                Refresh();
            }));
            _subscriptions.Add(eventBus.Subscribe<InventoryChangedEvent>(e => {
                _items = e.Items;
                Refresh();
            }));
        }

        private void Start() {
            _tooltip = new UiTooltip();
            _overlay = MenuStyles.Overlay(out VisualElement box);
            box.AddToClassList("ae-shop");
            box.style.width = StyleKeyword.Null;

            var header = new VisualElement();
            header.AddToClassList("ae-shop__header");
            var titles = new VisualElement();
            _title = MenuStyles.Title("Boutique");
            _title.style.marginBottom = 0;
            _title.style.unityTextAlign = TextAnchor.MiddleLeft;
            _limits = MenuStyles.Hint("");
            titles.Add(_title);
            titles.Add(_limits);
            _gold = new Label();
            _gold.AddToClassList("ae-gold");
            _gold.style.fontSize = 22;
            header.Add(titles);
            header.Add(_gold);
            box.Add(header);

            var body = new VisualElement();
            body.AddToClassList("ae-shop__body");
            _filters = new VisualElement();
            _filters.AddToClassList("ae-shop__filters");
            foreach ((Filter filter, string label) in Filters) {
                Filter f = filter;
                var button = new Button(() => { _filter = f; Refresh(); }) { text = label };
                button.AddToClassList("ae-filter");
                button.userData = f;
                _filters.Add(button);
            }
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

            var footer = new VisualElement();
            footer.AddToClassList("ae-shop__footer");
            _inventory = new VisualElement();
            _inventory.AddToClassList("ae-shop__inventory");
            footer.Add(_inventory);
            Button leave = MenuStyles.Button("Continuer", () => _eventBus.Publish(new ShopCloseRequestedEvent()));
            leave.AddToClassList("ae-button--primary");
            leave.style.width = 180;
            footer.Add(leave);
            box.Add(footer);

            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            root.Add(_overlay);
            root.Add(_tooltip.Root);
            Refresh();
        }

        private void OnDestroy() => _subscriptions.Dispose();

        /// <summary>Selects an offer (click), shown in the details panel.</summary>
        public void Select(ScriptableObject offer) {
            _selected = offer;
            Refresh();
        }

        private void Buy(ScriptableObject offer) => _eventBus.Publish(new ShopBuyRequestedEvent(offer));

        private void Refresh() {
            if (_overlay == null) return;
            _overlay.style.display = _state.Open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_state.Open) {
                _tooltip.Hide();
                return;
            }

            _title.text = _state.Full ? "Boutique" : "Boutique légère";
            _gold.text = $"{_goldValue} or";
            _limits.text = $"Élixir : {(_state.ElixirBought ? "acheté" : "1 max")}"
                           + (_state.Full ? $"   ·   Objet : {(_state.ItemBought ? "acheté" : "1 max (enclumes à objets comprises)")}" : "   ·   Objets : boutique complète")
                           + "   ·   Enclumes à stats : sans limite";
            foreach (VisualElement child in _filters.Children())
                child.EnableInClassList("ae-filter--active", (Filter)child.userData == _filter);

            _inventory.Clear();
            for (int i = 0; i < Inventory.Capacity; i++) _inventory.Add(PlayerHud.ItemSlot(i < _items.Count ? _items[i] : null, _tooltip));

            _grid.Clear();
            if (_state.Offers == null) return;
            var refusals = new Dictionary<ScriptableObject, string>();
            for (int i = 0; i < _state.Offers.Count; i++) refusals[_state.Offers[i]] = _state.Refusals[i];
            if (_selected != null && !refusals.ContainsKey(_selected)) _selected = null;

            List<ScriptableObject> consumables = _state.Offers.Where(o => o is not ItemDefinition && Matches(o)).ToList();
            List<ItemDefinition> items = _state.Offers.OfType<ItemDefinition>().Where(Matches).OrderBy(i => i.Cost).ToList();
            AddSection("Élixirs et enclumes", consumables, refusals);
            AddSection(_state.Full ? "Objets légendaires" : "", items.Cast<ScriptableObject>().ToList(), refusals);
            if (consumables.Count == 0 && items.Count == 0) _grid.Add(MenuStyles.Hint("Rien dans cette catégorie."));

            _selected ??= consumables.Concat(items).FirstOrDefault();
            RefreshDetail(_selected, _selected != null && refusals.TryGetValue(_selected, out string r) ? r : null);
        }

        private void AddSection(string title, List<ScriptableObject> offers, Dictionary<ScriptableObject, string> refusals) {
            if (offers.Count == 0) return;
            if (!string.IsNullOrEmpty(title)) {
                var label = new Label(title);
                label.AddToClassList("ae-shop__section");
                _grid.Add(label);
            }
            var row = new VisualElement();
            row.AddToClassList("ae-shop__row");
            foreach (ScriptableObject offer in offers) row.Add(OfferIcon(offer, refusals[offer]));
            _grid.Add(row);
        }

        private VisualElement OfferIcon(ScriptableObject offer, string refusal) {
            (string name, Color color, Sprite icon, string _) = OfferCard.Describe(offer);
            int cost = OfferCard.Cost(offer);
            var cell = new VisualElement();
            cell.AddToClassList("ae-offer");
            cell.EnableInClassList("ae-offer--refused", refusal != null && refusal != "Pas assez d'or");
            cell.EnableInClassList("ae-offer--poor", refusal == "Pas assez d'or");

            var frame = new VisualElement();
            frame.AddToClassList("ae-slot");
            frame.AddToClassList("ae-offer__icon");
            frame.EnableInClassList("ae-slot--selected", offer == _selected);
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
                if (e.button == 1) Buy(offer);   // right click buys, like LoL
            });
            _tooltip.Attach(cell, () => (name, cost > 0 ? $"{cost} or" : "", refusal));
            return cell;
        }

        private void RefreshDetail(ScriptableObject offer, string refusal) {
            _detail.Clear();
            if (offer == null) {
                _detail.Add(MenuStyles.Hint("Clique sur un objet pour voir ses détails. Double-clic ou clic droit : acheter."));
                return;
            }
            (string name, Color color, Sprite icon, string body) = OfferCard.Describe(offer);
            var head = new VisualElement();
            head.AddToClassList("ae-detail__head");
            var image = new VisualElement();
            image.AddToClassList("ae-slot");
            image.AddToClassList("ae-detail__icon");
            if (icon != null) image.style.backgroundImage = new StyleBackground(icon);
            head.Add(image);
            var titles = new VisualElement();
            titles.style.flexShrink = 1;
            var title = new Label(name);
            title.AddToClassList("ae-detail__name");
            title.style.color = color;
            titles.Add(title);
            int cost = OfferCard.Cost(offer);
            var price = new Label(cost > 0 ? $"{cost} or" : "Gratuit");
            price.AddToClassList("ae-gold");
            titles.Add(price);
            head.Add(titles);
            _detail.Add(head);

            var stats = new Label(offer is ItemDefinition shown ? StatLabels.Format(shown.Stats) : body);
            stats.AddToClassList("ae-detail__stats");
            _detail.Add(stats);

            if (offer is ItemDefinition item) {
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

        private bool Matches(ScriptableObject offer) {
            if (_filter == Filter.All) return true;
            if (offer is not ItemDefinition item) return _filter == Filter.Consumables;
            bool Has(params StatType[] stats) => item.Stats.Any(s => stats.Contains(s.Stat));
            return _filter switch {
                Filter.Attack => Has(StatType.PhysicalAttack, StatType.ArmorPenFlat, StatType.ArmorPenPercent),
                Filter.Critical => Has(StatType.CritChance),
                Filter.AttackSpeed => Has(StatType.AttackSpeed),
                Filter.Magic => Has(StatType.MagicAttack, StatType.MagicPenFlat, StatType.MagicPenPercent),
                Filter.Mana => Has(StatType.MaxMana, StatType.ManaRegen),
                Filter.Health => Has(StatType.MaxHealth, StatType.HealthRegen),
                Filter.Armor => Has(StatType.Armor),
                Filter.MagicResist => Has(StatType.MagicResist),
                Filter.Haste => Has(StatType.AbilityHaste),
                Filter.Boots => item.UniqueGroup == "Bottes",
                _ => false,
            };
        }
    }
}
