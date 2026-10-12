using System;
using System.Collections.Generic;
using UnityEngine;

namespace Features.Run {
    /// <summary>
    /// What opens at the end of a wave (design doc §9.3). FreeStatAnvil comes from loot picked up
    /// during the wave (§9.5). Only append at the end.
    /// </summary>
    public enum RewardKind { Augment, FreePrismaticAnvil, LightShop, FullShop, FreeStatAnvil }

    /// <summary>
    /// Order of the end-of-wave events (design doc §9.3): a prelude, then a loop for the regular
    /// waves; in bonus waves a full shop after each wave plus an augment every N waves.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Run/Wave Reward Schedule", fileName = "WaveRewardSchedule")]
    public class WaveRewardSchedule : ScriptableObject {
        [SerializeField] private RewardKind[] prelude = { RewardKind.Augment, RewardKind.FreePrismaticAnvil };
        [SerializeField] private RewardKind[] loop = { RewardKind.LightShop, RewardKind.Augment, RewardKind.FullShop };
        [Tooltip("Bonus waves: an augment after every N-th bonus wave (on top of the full shop).")]
        [SerializeField, Min(1)] private int bonusAugmentEvery = 2;

        /// <summary>Rewards after regular wave <paramref name="waveIndex"/> (0-based).</summary>
        public IReadOnlyList<RewardKind> AfterWave(int waveIndex) {
            if (waveIndex < prelude.Length) return new[] { prelude[waveIndex] };
            if (loop.Length == 0) return Array.Empty<RewardKind>();
            return new[] { loop[(waveIndex - prelude.Length) % loop.Length] };
        }

        /// <summary>Rewards after bonus wave <paramref name="bonusIndex"/> (0-based).</summary>
        public IReadOnlyList<RewardKind> AfterBonusWave(int bonusIndex) =>
            (bonusIndex + 1) % bonusAugmentEvery == 0
                ? new[] { RewardKind.FullShop, RewardKind.Augment }
                : new[] { RewardKind.FullShop };
    }
}
