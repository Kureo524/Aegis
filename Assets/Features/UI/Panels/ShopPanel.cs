using System.Collections.Generic;
using Features.Economy;
using Features.Events;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI.Panels {
    /// <summary>
    /// Shop screen (design doc §9.3), drawn from the ShopService snapshots only (ShopStateEvent):
    /// gold, inventory, offers with their price, greyed with the reason when not buyable. Buying and
    /// leaving are bus requests. Own UIDocument; anvils open above it (AnvilPanel).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ShopPanel : MonoBehaviour {
        private IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();
        private VisualElement _overlay, _offers, _inventory;
        private Label _title, _gold, _limits;
        private ShopStateEvent _state;
        private IReadOnlyList<ItemDefinition> _items = new List<ItemDefinition>();
        private int _goldValue;

        public bool IsOpen => _state.Open;

        [Inject]
        public void Construct(IEventBus eventBus) {
            _eventBus = eventBus;
            _subscriptions.Add(eventBus.Subscribe<ShopStateEvent>(e => {
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
            _overlay = MenuStyles.Overlay(out VisualElement box);
            box.style.width = 4 * (OfferCard.Width + 10) + 90;   // 4 cards per row next to the scrollbar

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            _title = MenuStyles.Title("Boutique");
            _title.style.marginBottom = 0;
            _gold = UiStyles.Text("", 20, new Color(1f, 0.82f, 0.3f));
            header.Add(_title);
            header.Add(_gold);
            box.Add(header);

            _limits = MenuStyles.Hint("");
            _limits.style.marginBottom = 6;
            box.Add(_limits);

            _inventory = new VisualElement();
            _inventory.style.flexDirection = FlexDirection.Row;
            _inventory.style.marginBottom = 8;
            box.Add(_inventory);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = 430;
            _offers = new VisualElement();
            _offers.style.flexDirection = FlexDirection.Row;
            _offers.style.flexWrap = Wrap.Wrap;
            _offers.style.justifyContent = Justify.Center;
            scroll.Add(_offers);
            box.Add(scroll);

            Button leave = MenuStyles.Button("Continuer", () => _eventBus.Publish(new ShopCloseRequestedEvent()));
            leave.style.marginTop = 10;
            box.Add(leave);

            GetComponent<UIDocument>().rootVisualElement.Add(_overlay);
            Refresh();
        }

        private void OnDestroy() => _subscriptions.Dispose();

        private void Refresh() {
            if (_overlay == null) return;
            _overlay.style.display = _state.Open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_state.Open) return;

            _title.text = _state.Full ? "Boutique complète" : "Boutique légère";
            _gold.text = $"{_goldValue} or";
            _limits.text = $"Élixir : {(_state.ElixirBought ? "acheté" : "1 max")}"
                           + (_state.Full ? $"   ·   Item : {(_state.ItemBought ? "acheté" : "1 max (enclumes à items comprises)")}" : "")
                           + "   ·   Enclumes à stats : sans limite";

            _inventory.Clear();
            for (int i = 0; i < Inventory.Capacity; i++) _inventory.Add(InventorySlot(i < _items.Count ? _items[i] : null));

            _offers.Clear();
            if (_state.Offers == null) return;
            for (int i = 0; i < _state.Offers.Count; i++) {
                ScriptableObject offer = _state.Offers[i];
                string refusal = _state.Refusals[i];
                (string name, Color color, Sprite icon, string body) = OfferCard.Describe(offer);
                _offers.Add(OfferCard.Build(icon, name, color, body, $"Acheter · {OfferCard.Cost(offer)} or",
                                            () => _eventBus.Publish(new ShopBuyRequestedEvent(offer)), refusal == null, refusal));
            }
        }

        public static VisualElement InventorySlot(ItemDefinition item) {
            var slot = new VisualElement();
            slot.style.width = slot.style.height = 38;
            slot.style.marginRight = 4;
            slot.style.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 0.9f);
            UiStyles.Border(slot, 1, item != null ? StatLabels.Color(item.Tier) : new Color(1f, 1f, 1f, 0.15f), 5);
            if (item != null && item.Icon != null) {
                slot.style.backgroundImage = new StyleBackground(item.Icon);
                slot.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            }
            if (item != null) slot.tooltip = item.DisplayName;
            return slot;
        }
    }
}
