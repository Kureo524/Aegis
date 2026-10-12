using System;
using Features.Enemies;
using UnityEngine;

namespace Features.Run {
    /// <summary>
    /// One wave (design doc §9.1): a timer AND a kill quota end it. Enemies spawn continuously from
    /// its table; once the quota is reached they still come (gold only, no more XP) until the end of
    /// the timer. WaveScaling multiplies the enemies' health and damage.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Run/Wave Definition", fileName = "Wave_")]
    public class WaveDefinition : ScriptableObject {
        [SerializeField, Min(1)] private int killQuota = 20;
        [Tooltip("Seconds.")]
        [SerializeField, Min(1f)] private float timeLimit = 60f;

        [Header("Spawn")]
        [SerializeField] private HordeSpawnEntry[] spawnTable = Array.Empty<HordeSpawnEntry>();
        [SerializeField, Min(0.05f)] private float spawnInterval = 1.5f;
        [Tooltip("Spawn interval once the quota is reached (gold only).")]
        [SerializeField, Min(0.05f)] private float postQuotaSpawnInterval = 2.5f;
        [SerializeField, Min(1)] private int maxAlive = 20;

        [Header("Scaling (WaveScaling)")]
        [Tooltip("1 = enemy stats as in their definition.")]
        [SerializeField, Min(0.1f)] private float healthMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float damageMultiplier = 1f;

        public int KillQuota => killQuota;
        public float TimeLimit => timeLimit;
        public HordeSpawnEntry[] SpawnTable => spawnTable;
        public float SpawnInterval => spawnInterval;
        public float PostQuotaSpawnInterval => postQuotaSpawnInterval;
        public int MaxAlive => maxAlive;
        public float HealthMultiplier => healthMultiplier;
        public float DamageMultiplier => damageMultiplier;
    }
}
