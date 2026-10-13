using System;
using UnityEngine;

namespace Features.Economy {
    /// <summary>
    /// An item (design doc §9.3), copied from LoL: stats while it sits in the inventory and its own
    /// passive (same system as characters and augments, design doc §8.1). Legendary items and boots
    /// are sold in the full shop; prismatic ones (adaptive force, Arena style) only come from item
    /// anvils. A unique group allows one item of the group (boots, Spellblade, Hydra…).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Economy/Item", fileName = "Item_")]
    public class ItemDefinition : ScriptableObject {
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField, Min(0)] private int cost = 250;
        [SerializeField] private ItemTier tier = ItemTier.Legendary;
        [SerializeField] private StatBonus[] stats = Array.Empty<StatBonus>();
        [Tooltip("At most one copy in the inventory.")]
        [SerializeField] private bool unique = true;
        [Tooltip("At most one item of this group in the inventory (LoL: \"Bottes\", \"Lame enchantée\", \"Hydre\"…). Empty = none.")]
        [SerializeField] private string uniqueGroup = "";
        [Tooltip("Optional effect on top of the stats.")]
        [SerializeField] private Passives.PassiveDefinition passive;

        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int Cost => cost;
        public ItemTier Tier => tier;
        public StatBonus[] Stats => stats;
        public bool Unique => unique;
        public string UniqueGroup => uniqueGroup;
        public Passives.PassiveDefinition Passive => passive;
    }
}
