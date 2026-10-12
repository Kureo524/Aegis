using UnityEngine;

namespace Features.Economy {
    public enum AnvilKind { Stat, Item }

    /// <summary>
    /// An anvil (design doc §9.3): always 3 choices of the same rarity. Stat anvil: the rarity is
    /// drawn when it opens (RarityWeights: Silver / Gold / Prismatic), no reroll. Item anvil: fixed
    /// item tier, rerolls per choice. Fixed price for the whole run.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Economy/Anvil", fileName = "Anvil_")]
    public class AnvilDefinition : ScriptableObject {
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField] private AnvilKind kind;
        [Tooltip("Stat anvil: weights of Silver / Gold / Prismatic.")]
        [SerializeField] private float[] rarityWeights = { 70f, 25f, 5f };
        [Tooltip("Item anvil: tier of the 3 items.")]
        [SerializeField] private ItemTier itemTier = ItemTier.Legendary;
        [SerializeField, Min(0)] private int cost = 60;
        [SerializeField, Min(1)] private int choiceCount = 3;
        [SerializeField, Min(0)] private int rerollsPerChoice;

        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public AnvilKind Kind => kind;
        public float[] RarityWeights => rarityWeights;
        public ItemTier ItemTier => itemTier;
        public int Cost => cost;
        public int ChoiceCount => choiceCount;
        public int RerollsPerChoice => rerollsPerChoice;
    }
}
