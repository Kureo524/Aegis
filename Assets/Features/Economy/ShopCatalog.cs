using System;
using System.Collections.Generic;
using UnityEngine;

namespace Features.Economy {
    /// <summary>
    /// Everything the shops and anvils can offer (design doc §9.3). Light shop: elixirs and stat
    /// anvils; full shop: + legendary items and item anvils. The free prismatic anvil of the
    /// schedule is <see cref="FreeAnvil"/>. Registered by the GameLifetimeScope.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Economy/Shop Catalog", fileName = "ShopCatalog")]
    public class ShopCatalog : ScriptableObject {
        [SerializeField] private ElixirDefinition[] elixirs = Array.Empty<ElixirDefinition>();
        [SerializeField] private AnvilDefinition[] statAnvils = Array.Empty<AnvilDefinition>();
        [SerializeField] private AnvilDefinition[] itemAnvils = Array.Empty<AnvilDefinition>();
        [Tooltip("All items: legendary ones are sold in the full shop, both tiers fill the item anvils.")]
        [SerializeField] private ItemDefinition[] items = Array.Empty<ItemDefinition>();
        [SerializeField] private StatChoiceDefinition[] statChoices = Array.Empty<StatChoiceDefinition>();
        [Tooltip("Opened for free by the reward schedule (after wave 2).")]
        [SerializeField] private AnvilDefinition freeAnvil;

        public IReadOnlyList<ElixirDefinition> Elixirs => elixirs;
        public IReadOnlyList<AnvilDefinition> StatAnvils => statAnvils;
        public IReadOnlyList<AnvilDefinition> ItemAnvils => itemAnvils;
        public IReadOnlyList<ItemDefinition> Items => items;
        public IReadOnlyList<StatChoiceDefinition> StatChoices => statChoices;
        public AnvilDefinition FreeAnvil => freeAnvil;
    }
}
