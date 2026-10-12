using Features.Combat;
using Features.Progression;
using Features.Stats;
using UnityEngine;

namespace Features.Characters {
    /// <summary>
    /// Everything that makes one character different from another (design doc §8): one generic
    /// prefab + one of these = one character. Read-only at runtime.
    /// Elements, passive, signature ability, spell book and AI are added with their phases.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Character Definition", fileName = "Char_")]
    public class CharacterDefinition : ScriptableObject {
        [SerializeField] private string displayName;
        [SerializeField] private Faction faction = Faction.Enemy;

        [Header("Stats")]
        [SerializeField] private StatBlock baseStats;
        [SerializeField] private StatGrowth growthPerLevel;

        [Header("Combat")]
        [SerializeField] private BasicAttackDefinition basicAttack;

        public string DisplayName => displayName;
        public Faction Faction => faction;
        public StatBlock BaseStats => baseStats;
        public StatGrowth GrowthPerLevel => growthPerLevel;
        public BasicAttackDefinition BasicAttack => basicAttack;

#if UNITY_EDITOR
        private void OnValidate() {
            if (baseStats == null) Debug.LogWarning($"[CharacterDefinition] {name}: no base stats.", this);
            if (basicAttack == null) Debug.LogWarning($"[CharacterDefinition] {name}: no basic attack.", this);
        }
#endif
    }
}
