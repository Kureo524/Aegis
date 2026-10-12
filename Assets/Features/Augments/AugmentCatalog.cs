using System;
using System.Collections.Generic;
using Features.Economy;
using UnityEngine;

namespace Features.Augments {
    /// <summary>Augments on offer and the draft rules (design doc §9.4). Registered by the GameLifetimeScope.</summary>
    [CreateAssetMenu(menuName = "Game/Augment Catalog", fileName = "AugmentCatalog")]
    public class AugmentCatalog : ScriptableObject {
        [SerializeField] private AugmentDefinition[] augments = Array.Empty<AugmentDefinition>();

        [Header("Slots (AugmentSlots / MaxAugmentSlots)")]
        [SerializeField, Min(1)] private int baseSlots = 4;
        [SerializeField, Min(1)] private int maxSlots = 6;

        [Header("Draft")]
        [SerializeField, Min(1)] private int choiceCount = 3;
        [Tooltip("Rarity of a draft (all 3 choices): Silver / Gold / Prismatic weights.")]
        [SerializeField] private float[] rarityWeights = { 55f, 33f, 12f };
        [Tooltip("Rerolls for the whole run: one per base slot (each replaces one choice).")]
        [SerializeField, Min(0)] private int rerolls = 4;
        [Tooltip("Chance that a draft carries a golden reroll: its first reroll gives rarity +1.")]
        [SerializeField, Range(0f, 1f)] private float goldRerollChance = 0.05f;

        [Header("All slots full")]
        [Tooltip("Stat anvil opened with the drafted rarity (the 'Anvil' option).")]
        [SerializeField] private AnvilDefinition fullSlotsAnvil;
        [Tooltip("Gold instead of a slot once at MaxSlots: Silver / Gold / Prismatic.")]
        [SerializeField] private int[] goldPerRarity = { 150, 300, 500 };

        public IReadOnlyList<AugmentDefinition> Augments => augments;
        public int BaseSlots => baseSlots;
        public int MaxSlots => Mathf.Max(baseSlots, maxSlots);
        public int ChoiceCount => choiceCount;
        public float[] RarityWeights => rarityWeights;
        public int Rerolls => rerolls;
        public float GoldRerollChance => goldRerollChance;
        public AnvilDefinition FullSlotsAnvil => fullSlotsAnvil;
        public int GoldFor(RarityTier rarity) => (int)rarity < goldPerRarity.Length ? goldPerRarity[(int)rarity] : 0;
    }
}
