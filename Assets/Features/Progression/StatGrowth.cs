using System;
using System.Collections.Generic;
using Features.Stats;
using UnityEngine;

namespace Features.Progression {
    public enum GrowthMode {
        /// <summary>bonus = gain × (level − 1)</summary>
        Linear,
        /// <summary>bonus = gain × (n − 1) × (0.7025 + 0.0175 × (n − 1)), the usual MOBA non-linear curve</summary>
        Curved
    }

    [Serializable]
    public struct StatGrowthEntry {
        public StatType Stat;
        public float GainPerLevel;
        [Tooltip("Flat for most stats. AttackSpeed grows as a % bonus: use PercentAdd.")]
        public ModifierType Type;
    }

    /// <summary>Per-level stat gains of a character (design doc §5.5).</summary>
    [CreateAssetMenu(menuName = "Game/Stat Growth", fileName = "StatGrowth_")]
    public class StatGrowth : ScriptableObject {
        [SerializeField] private GrowthMode mode = GrowthMode.Linear;
        [SerializeField] private List<StatGrowthEntry> entries = new();

        public GrowthMode Mode => mode;
        public IReadOnlyList<StatGrowthEntry> Entries => entries;

        /// <summary>Total bonus of one entry at <paramref name="level"/> (0 at level 1).</summary>
        public float BonusAt(StatGrowthEntry entry, int level) {
            int n = Mathf.Max(level, 1) - 1;
            return mode switch {
                GrowthMode.Curved => entry.GainPerLevel * n * (0.7025f + 0.0175f * n),
                _ => entry.GainPerLevel * n,
            };
        }

#if UNITY_EDITOR
        private void OnValidate() {
            var seen = new HashSet<StatType>();
            foreach (StatGrowthEntry e in entries) {
                if (!seen.Add(e.Stat)) Debug.LogWarning($"[StatGrowth] {name}: duplicate entry for {e.Stat}.", this);
                if (e.Stat == StatType.AttackSpeed && e.Type != ModifierType.PercentAdd)
                    Debug.LogWarning($"[StatGrowth] {name}: AttackSpeed should grow as PercentAdd (design doc §5.5).", this);
            }
        }
#endif
    }
}
