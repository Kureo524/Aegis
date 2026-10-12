using UnityEngine;

namespace Features.Progression {
    /// <summary>
    /// XP needed per level and skill point rules (design doc §7.8, §9.2). Shared by every character
    /// that levels up; registered in the RootLifetimeScope.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Experience Curve", fileName = "ExperienceCurve")]
    public class ExperienceCurve : ScriptableObject {
        [Tooltip("XP needed to go from level i+1 to level i+2. Max level = length + 1.")]
        [SerializeField] private int[] xpToNextLevel = System.Array.Empty<int>();
        [Tooltip("Levels 1..N each give a skill point; levels above give an overcharge point instead.")]
        [SerializeField] private int lastSkillPointLevel = 15;

        public int MaxLevel => xpToNextLevel.Length + 1;
        public int LastSkillPointLevel => lastSkillPointLevel;

        /// <summary>XP needed to leave <paramref name="level"/>; 0 at max level.</summary>
        public int XpToNext(int level) =>
            level >= 1 && level < MaxLevel ? xpToNextLevel[level - 1] : 0;

#if UNITY_EDITOR
        private void OnValidate() {
            for (int i = 0; i < xpToNextLevel.Length; i++) {
                if (xpToNextLevel[i] <= 0) Debug.LogWarning($"[ExperienceCurve] {name}: level {i + 1} needs a positive XP amount.", this);
            }
            if (lastSkillPointLevel > MaxLevel)
                Debug.LogWarning($"[ExperienceCurve] {name}: lastSkillPointLevel is above the max level ({MaxLevel}).", this);
        }
#endif
    }
}
