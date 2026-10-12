using System;
using UnityEngine;

namespace Features.Economy {
    /// <summary>One possible choice of a stat anvil (design doc §9.3): a permanent bonus of a rarity.</summary>
    [CreateAssetMenu(menuName = "Game/Economy/Stat Choice", fileName = "StatChoice_")]
    public class StatChoiceDefinition : ScriptableObject {
        [SerializeField] private RarityTier rarity;
        [SerializeField] private StatBonus[] stats = Array.Empty<StatBonus>();

        public RarityTier Rarity => rarity;
        public StatBonus[] Stats => stats;
    }
}
