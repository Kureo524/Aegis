using System;
using Features.Characters;
using UnityEngine;

namespace Features.Enemies {
    /// <summary>One kind of enemy the horde spawner can pick: which prefab variant, which data, how often.</summary>
    [Serializable]
    public struct HordeSpawnEntry {
        public Character Prefab;
        public EnemyDefinition Definition;
        [Min(0f)] public float Weight;
    }

    /// <summary>
    /// Horde tuning of a stage (design doc §9.6, §9.8): flow field grid, AI rate, continuous spawn
    /// around the player. Phase 7 waves (WaveDefinition) will drive the spawn instead of the fixed
    /// rate here. Registered by the GameLifetimeScope.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Settings/Horde Settings", fileName = "HordeSettings")]
    public class HordeSettings : ScriptableObject {
        [Header("Flow field")]
        [Tooltip("Center of the grid (the playable ground).")]
        [SerializeField] private Vector3 gridCenter = Vector3.zero;
        [SerializeField] private Vector2 gridSize = new(100f, 100f);
        [SerializeField, Min(0.25f)] private float cellSize = 1f;
        [Tooltip("Seconds between two recomputations (only when the player changed cell).")]
        [SerializeField, Min(0.05f)] private float flowFieldInterval = 0.25f;

        [Header("AI")]
        [Tooltip("Seconds between two decisions of one enemy (decisions are spread over the frames).")]
        [SerializeField, Min(0.02f)] private float thinkInterval = 0.1f;
        [Tooltip("NavMesh enemies: seconds between two path refreshes.")]
        [SerializeField, Min(0.05f)] private float repathInterval = 0.4f;
        [Tooltip("Seconds a dead enemy stays before going back to the pool.")]
        [SerializeField, Min(0f)] private float despawnDelay = 0.5f;

        [Header("Spawn")]
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private HordeSpawnEntry[] spawnTable = Array.Empty<HordeSpawnEntry>();
        [SerializeField, Min(0.01f)] private float spawnInterval = 0.6f;
        [SerializeField, Min(0)] private int maxAlive = 40;
        [Tooltip("Spawn ring around the player, beyond the screen edge.")]
        [SerializeField, Min(0f)] private float minSpawnDistance = 16f;
        [SerializeField, Min(0f)] private float maxSpawnDistance = 22f;

        public Vector3 GridCenter => gridCenter;
        public Vector2 GridSize => gridSize;
        public float CellSize => cellSize;
        public float FlowFieldInterval => flowFieldInterval;
        public float ThinkInterval => thinkInterval;
        public float RepathInterval => repathInterval;
        public float DespawnDelay => despawnDelay;
        public bool SpawnOnStart => spawnOnStart;
        public HordeSpawnEntry[] SpawnTable => spawnTable;
        public float SpawnInterval => spawnInterval;
        public int MaxAlive => maxAlive;
        public float MinSpawnDistance => minSpawnDistance;
        public float MaxSpawnDistance => Mathf.Max(minSpawnDistance, maxSpawnDistance);
    }
}
