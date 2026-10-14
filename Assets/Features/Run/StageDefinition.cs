using System;
using UnityEngine;

namespace Features.Run {
    /// <summary>What the player's death does to the run (design doc §9.1).</summary>
    public enum PlayerDeathRule { GameOver, LoseLife }

    /// <summary>
    /// A stage, "a BTD6 level" (design doc §9.1): its waves, its bonus waves, the stage lives and
    /// the timings around the waves. Played by <see cref="RunDirector"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Run/Stage Definition", fileName = "Stage_")]
    public class StageDefinition : ScriptableObject {
        [SerializeField] private string displayName = "Stage";
        [SerializeField] private WaveDefinition[] waves = Array.Empty<WaveDefinition>();
        [Tooltip("Played after the last wave (BonusWaveCount); the last one cleared = victory.")]
        [SerializeField] private WaveDefinition[] bonusWaves = Array.Empty<WaveDefinition>();
        [SerializeField] private WaveRewardSchedule rewardSchedule;

        [Header("Lives (StageLives)")]
        [SerializeField, Min(1)] private int lives = 3;
        [Tooltip("Overtime: one more life lost every N seconds while the quota is not reached (LifeLossInterval).")]
        [SerializeField, Min(1f)] private float lifeLossInterval = 30f;
        [Tooltip("GameOver: dying ends the run at once. LoseLife: it costs a stage life and the player respawns.")]
        [SerializeField] private PlayerDeathRule playerDeath = PlayerDeathRule.GameOver;

        [Header("Timings")]
        [Tooltip("Countdown before the first wave (seconds, not paused).")]
        [SerializeField, Min(0f)] private float firstWaveCountdown = 5f;
        [Tooltip("Break after the end-of-wave rewards: the time to shop (seconds, not paused). The HUD can start the wave sooner.")]
        [UnityEngine.Serialization.FormerlySerializedAs("preWaveCountdown")]
        [SerializeField, Min(0f)] private float intermission = 60f;

        public string DisplayName => displayName;
        public WaveDefinition[] Waves => waves;
        public WaveDefinition[] BonusWaves => bonusWaves;
        public WaveRewardSchedule RewardSchedule => rewardSchedule;
        public int Lives => lives;
        public float LifeLossInterval => lifeLossInterval;
        public PlayerDeathRule PlayerDeath => playerDeath;
        public float FirstWaveCountdown => firstWaveCountdown;
        public float Intermission => intermission;
        public int TotalWaves => waves.Length + bonusWaves.Length;
    }
}
