using System.Collections.Generic;
using Features.Economy;
using Features.Events;
using Features.UI.Panels;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI {
    /// <summary>
    /// The 6 item slots and the active elixir, bottom right of the HUD (design doc §9.8). Fed by
    /// InventoryChangedEvent / ElixirChangedEvent. Shares the GameHud UIDocument.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class InventoryView : MonoBehaviour {
        private readonly DisposableBag _subscriptions = new();
        private VisualElement _slots;
        private Label _elixir;
        private IReadOnlyList<ItemDefinition> _items = new List<ItemDefinition>();
        private ElixirDefinition _activeElixir;

        [Inject]
        public void Construct(IEventBus eventBus) {
            _subscriptions.Add(eventBus.Subscribe<InventoryChangedEvent>(e => {
                _items = e.Items;
                Refresh();
            }));
            _subscriptions.Add(eventBus.Subscribe<ElixirChangedEvent>(e => {
                _activeElixir = e.Elixir;
                Refresh();
            }));
        }

        private void Start() {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.style.position = Position.Absolute;
            column.style.right = 16;
            column.style.bottom = 16;
            column.style.alignItems = Align.FlexEnd;

            _elixir = UiStyles.Text("", 13, new Color(0.45f, 0.9f, 0.7f));
            _elixir.style.marginBottom = 4;
            column.Add(_elixir);

            _slots = new VisualElement { pickingMode = PickingMode.Ignore };
            _slots.style.flexDirection = FlexDirection.Row;
            column.Add(_slots);
            root.Add(column);
            Refresh();
        }

        private void OnDestroy() => _subscriptions.Dispose();

        private void Refresh() {
            if (_slots == null) return;
            _slots.Clear();
            for (int i = 0; i < Inventory.Capacity; i++) {
                VisualElement slot = ShopPanel.InventorySlot(i < _items.Count ? _items[i] : null);
                slot.pickingMode = PickingMode.Ignore;
                _slots.Add(slot);
            }
            _elixir.text = _activeElixir != null ? $"Élixir : {_activeElixir.DisplayName}" : "";
        }
    }
}
