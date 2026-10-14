using System;
using UnityEngine;

namespace Features.Meta {
    /// <summary>
    /// Meta-progression rules (design doc §9.7): the persistent currency earned at the end of a run
    /// and the trees. Registered in the root scope.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Settings/Meta Settings", fileName = "MetaSettings")]
    public class MetaSettings : ScriptableObject {
        [SerializeField] private string currencyName = "éclats";
        [Tooltip("Currency per wave cleared, on a win (full clear, bonus waves included).")]
        [SerializeField, Min(0)] private int perWaveOnWin = 6;
        [SerializeField, Min(0)] private int winBonus = 40;
        [Tooltip("Currency per wave cleared on a defeat (0 = only a full clear pays, as the design doc says).")]
        [SerializeField, Min(0)] private int perWaveOnDefeat;
        [SerializeField] private MetaTreeDefinition[] trees = Array.Empty<MetaTreeDefinition>();

        public string CurrencyName => currencyName;
        public MetaTreeDefinition[] Trees => trees;

        public int Reward(bool won, int wavesCleared) =>
            won ? perWaveOnWin * wavesCleared + winBonus : perWaveOnDefeat * wavesCleared;

        public MetaTreeDefinition TreeOf(Characters.CharacterDefinition character) {
            foreach (MetaTreeDefinition t in trees) if (t != null && t.Character == character) return t;
            return trees.Length > 0 ? trees[0] : null;
        }
    }
}
