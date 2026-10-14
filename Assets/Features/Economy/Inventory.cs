using System;
using System.Collections.Generic;
using Features.Events;
using Systems.EventBusses;
using VContainer.Unity;

namespace Features.Economy {
    /// <summary>
    /// The run's items (design doc §9.3): 6 slots like in MOBAs, no components, no selling yet. An item's
    /// stats are applied through PlayerUpgrades with the item slot as Source. Game scope.
    /// </summary>
    public sealed class Inventory : IStartable {
        public const int Capacity = 6;

        private readonly PlayerUpgrades _upgrades;
        private readonly IEventBus _eventBus;
        private readonly List<ItemDefinition> _items = new();

        public IReadOnlyList<ItemDefinition> Items => _items;
        public bool IsFull => _items.Count >= Capacity;

        public Inventory(PlayerUpgrades upgrades, IEventBus eventBus) {
            _upgrades = upgrades;
            _eventBus = eventBus;
        }

        public void Start() => Publish();

        public bool Contains(ItemDefinition item) => _items.Contains(item);

        public bool CanAdd(ItemDefinition item) => item != null && !IsFull && !(item.Unique && Contains(item)) && GroupOwner(item) == null;

        /// <summary>The owned item that shares <paramref name="item"/>'s unique group (null: none).</summary>
        public ItemDefinition GroupOwner(ItemDefinition item) {
            if (item == null || string.IsNullOrEmpty(item.UniqueGroup)) return null;
            foreach (ItemDefinition owned in _items)
                if (owned.UniqueGroup == item.UniqueGroup) return owned;
            return null;
        }

        public bool TryAdd(ItemDefinition item) {
            if (!CanAdd(item)) return false;
            _items.Add(item);
            var slot = new Slot(_items.Count - 1, item);
            _upgrades.ApplyPermanent(item.Stats, slot);
            _upgrades.AddPassive(item.Passive, slot);
            Publish();
            return true;
        }

        private void Publish() => _eventBus.Publish(new InventoryChangedEvent(_items.ToArray(), Capacity));

        // Modifier source of an owned item: one per copy, so a copy can be removed alone later
        private sealed class Slot {
            public readonly int Index;
            public readonly ItemDefinition Item;
            public Slot(int index, ItemDefinition item) {
                Index = index;
                Item = item;
            }
        }
    }
}
