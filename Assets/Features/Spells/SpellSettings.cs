using UnityEngine;

namespace Features.Spells {
    /// <summary>Global spell rules and balance (design doc §7.2, §7.3, §7.7, §7.8). Registered in the root.</summary>
    [CreateAssetMenu(menuName = "Game/Settings/Spell Settings", fileName = "SpellSettings")]
    public class SpellSettings : ScriptableObject {
        [Header("Element selection")]
        [SerializeField] private int maxTotalStacks = 3;
        [SerializeField] private int maxStacksPerElement = 3;

        [Header("Mana cost")]
        [Tooltip("Multiplier by total number of stacks: index 0 = 1 stack, 1 = 2 stacks…")]
        [SerializeField] private float[] costCurve = { 1f, 1.5f, 2.25f };

        [Header("Element levels")]
        [SerializeField] private int maxElementLevel = 5;

        public int MaxTotalStacks => maxTotalStacks;
        public int MaxStacksPerElement => maxStacksPerElement;
        public int MaxElementLevel => maxElementLevel;

        /// <summary>Cost multiplier for <paramref name="totalStacks"/> stacks (last entry beyond the table).</summary>
        public float CostMultiplier(int totalStacks) {
            if (totalStacks <= 0 || costCurve.Length == 0) return 0f;
            return costCurve[Mathf.Min(totalStacks, costCurve.Length) - 1];
        }

#if UNITY_EDITOR
        private void OnValidate() {
            if (costCurve.Length < maxTotalStacks)
                Debug.LogWarning($"[SpellSettings] {name}: costCurve needs {maxTotalStacks} entries.", this);
        }
#endif
    }
}
