using System;
using UnityEngine;

namespace Features.Economy {
    /// <summary>Who the item is made for (shop filters, design doc §9.3).</summary>
    [Flags]
    public enum ItemRole { None = 0, Marksman = 1, Mage = 2, Fighter = 4, Tank = 8, Assassin = 16 }

    /// <summary>
    /// An item (design doc §9.3): stats while it sits in the inventory and its own passive (same
    /// system as characters and augments, design doc §8.1). Legendary items and boots are sold in
    /// the shop; prismatic ones (adaptive force) only come from item anvils. A unique group allows
    /// one item of the group (boots, runic weapons…). Sold back for <see cref="SellValue"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Economy/Item", fileName = "Item_")]
    public class ItemDefinition : ScriptableObject {
        /// <summary>Share of the price given back when an item is sold.</summary>
        public const float SellRatio = 0.7f;
        /// <summary>Sell value of an item that was not bought (prismatic items from anvils).</summary>
        public const int UnboughtSellValue = 150;

        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField, Min(0)] private int cost = 250;
        [SerializeField] private ItemTier tier = ItemTier.Legendary;
        [SerializeField] private StatBonus[] stats = Array.Empty<StatBonus>();
        [Tooltip("At most one copy in the inventory.")]
        [SerializeField] private bool unique = true;
        [Tooltip("At most one item of this group in the inventory (e.g. \"Bottes\", \"Arme runique\", \"Fendoir\"). Empty = none.")]
        [SerializeField] private string uniqueGroup = "";
        [Tooltip("Optional effect on top of the stats.")]
        [SerializeField] private Passives.PassiveDefinition passive;
        [Tooltip("Who it is made for (shop filters).")]
        [SerializeField] private ItemRole roles;

        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int Cost => cost;
        public ItemTier Tier => tier;
        public StatBonus[] Stats => stats;
        public bool Unique => unique;
        public string UniqueGroup => uniqueGroup;
        public Passives.PassiveDefinition Passive => passive;
        public ItemRole Roles => roles;
        /// <summary>Gold given back when the item is sold (70 % of its price).</summary>
        public int SellValue => cost > 0 ? Mathf.RoundToInt(cost * SellRatio) : UnboughtSellValue;
    }
}
