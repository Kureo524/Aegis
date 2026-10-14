using System;
using System.Collections.Generic;
using UnityEngine;

namespace Features.Run {
    /// <summary>
    /// What opens at the end of a wave (design doc §9.3). FreeStatAnvil comes from loot picked up
    /// during the wave (§9.5). Only append at the end.
    /// </summary>
    /// <summary>What a wave gives once cleared, before the break (the shop is always there, §9.3).</summary>
    public enum RewardKind { Augment = 0, FreePrismaticAnvil = 1, FreeStatAnvil = 4, None = 5 }

    /// <summary>
    /// Order of the end-of-wave rewards (design doc §9.3): a prelude, then a loop for the regular
    /// waves (None: just the break); in bonus waves an augment every N waves. The shop is open
    /// during every break, it is no reward step any more.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Run/Wave Reward Schedule", fileName = "WaveRewardSchedule")]
    public class WaveRewardSchedule : ScriptableObject {
        [SerializeField] private RewardKind[] prelude = { RewardKind.Augment, RewardKind.FreePrismaticAnvil };
        [SerializeField] private RewardKind[] loop = { RewardKind.None, RewardKind.Augment, RewardKind.None };
        [Tooltip("Bonus waves: an augment after every N-th bonus wave.")]
        [SerializeField, Min(1)] private int bonusAugmentEvery = 2;

        /// <summary>Rewards after regular wave <paramref name="waveIndex"/> (0-based).</summary>
        public IReadOnlyList<RewardKind> AfterWave(int waveIndex) {
            RewardKind kind;
            if (waveIndex < prelude.Length) kind = prelude[waveIndex];
            else if (loop.Length == 0) return Array.Empty<RewardKind>();
            else kind = loop[(waveIndex - prelude.Length) % loop.Length];
            return kind == RewardKind.None ? Array.Empty<RewardKind>() : new[] { kind };
        }

        /// <summary>Rewards after bonus wave <paramref name="bonusIndex"/> (0-based).</summary>
        public IReadOnlyList<RewardKind> AfterBonusWave(int bonusIndex) =>
            (bonusIndex + 1) % bonusAugmentEvery == 0 ? new[] { RewardKind.Augment } : Array.Empty<RewardKind>();
    }
}
