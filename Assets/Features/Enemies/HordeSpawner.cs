using System;
using System.Collections.Generic;
using Features.Characters;
using Features.Events;
using Systems.EventBusses;
using Systems.Pooling;
using UnityEngine;
using UnityEngine.AI;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace Features.Enemies {
    /// <summary>
    /// Continuous horde spawn around the player (design doc §9.6, §9.8): one enemy every
    /// SpawnInterval, picked by weight in the spawn table, on a ring beyond the screen edge, on a
    /// walkable cell the flow field can reach, up to MaxAlive. Enemies come from the PrefabPool and
    /// go back to it DespawnDelay after their death. The player's death clears the horde.
    /// Phase 7 waves will drive it (quota, timer, scaling). Game scope service.
    /// </summary>
    public sealed class HordeSpawner : IStartable, ITickable, IDisposable {
        private const int PlacementAttempts = 10;

        private readonly HordeSettings _settings;
        private readonly PrefabPool _pool;
        private readonly EnemyDirector _director;
        private readonly FlowFieldService _flowField;
        private readonly IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();
        private readonly List<Character> _alive = new();
        private readonly List<(Character enemy, float releaseAt)> _dying = new();
        private float _nextSpawn;
        private int _pendingBurst;
        private float _totalWeight;

        public bool Spawning { get; private set; }
        public int Alive => _alive.Count;
        public IReadOnlyList<Character> AliveEnemies => _alive;

        public HordeSpawner(HordeSettings settings, PrefabPool pool, EnemyDirector director,
                            FlowFieldService flowField, IEventBus eventBus) {
            _settings = settings;
            _pool = pool;
            _director = director;
            _flowField = flowField;
            _eventBus = eventBus;
        }

        public void Start() {
            foreach (HordeSpawnEntry entry in _settings.SpawnTable) {
                if (entry.Prefab != null && entry.Definition != null) _totalWeight += entry.Weight;
            }
            _subscriptions.Add(_eventBus.Subscribe<PlayerDiedEvent>(_ => Clear()));
            _subscriptions.Add(_eventBus.Subscribe<HordeDebugEvent>(e => {
                if (e.Toggle) SetSpawning(!Spawning);
                _pendingBurst += e.Burst;
            }));
            SetSpawning(_settings.SpawnOnStart);
        }

        public void SetSpawning(bool spawning) {
            Spawning = spawning;
            _nextSpawn = Time.time + _settings.SpawnInterval;
            PublishStatus();
        }

        public void Tick() {
            ReleaseTheDead();
            Character player = _director.Player;
            if (player == null || !player.IsAlive || _totalWeight <= 0f) return;

            // Debug bursts ignore MaxAlive (stress tests); a few per frame
            for (int i = 0; i < 10 && _pendingBurst > 0; i++, _pendingBurst--) TrySpawn(player.transform.position);

            if (!Spawning || Time.time < _nextSpawn) return;
            _nextSpawn = Time.time + _settings.SpawnInterval;
            if (_alive.Count < _settings.MaxAlive) TrySpawn(player.transform.position);
        }

        /// <summary>Spawns one enemy of the table around <paramref name="center"/>. Null if no spot was found.</summary>
        public Character TrySpawn(Vector3 center) {
            HordeSpawnEntry entry = Pick();
            if (entry.Prefab == null || !TryFindSpot(center, entry.Definition, out Vector3 spot)) return null;
            return Spawn(entry.Prefab, entry.Definition, spot);
        }

        public Character Spawn(Character prefab, EnemyDefinition definition, Vector3 position) {
            Character enemy = _pool.Get(prefab);
            enemy.GetComponent<Movement.CharacterMovement>().Teleport(position);
            enemy.name = definition.DisplayName;
            enemy.Init(definition);
            enemy.Died += OnEnemyDied;
            _alive.Add(enemy);
            PublishStatus();
            return enemy;
        }

        /// <summary>Removes every enemy at once (no reward).</summary>
        public void Clear() {
            foreach (Character enemy in _alive) Release(enemy);
            foreach ((Character enemy, float _) in _dying) _pool.Release(enemy);
            _alive.Clear();
            _dying.Clear();
            _pendingBurst = 0;
            PublishStatus();
        }

        private void OnEnemyDied(Character enemy) {
            enemy.Died -= OnEnemyDied;
            _alive.Remove(enemy);
            _dying.Add((enemy, Time.time + _settings.DespawnDelay));
            PublishStatus();
        }

        private void ReleaseTheDead() {
            for (int i = _dying.Count - 1; i >= 0; i--) {
                if (Time.time < _dying[i].releaseAt) continue;
                _pool.Release(_dying[i].enemy);
                _dying.RemoveAt(i);
            }
        }

        private void Release(Character enemy) {
            enemy.Died -= OnEnemyDied;
            _pool.Release(enemy);
        }

        private HordeSpawnEntry Pick() {
            float roll = Random.value * _totalWeight;
            HordeSpawnEntry last = default;
            foreach (HordeSpawnEntry entry in _settings.SpawnTable) {
                if (entry.Prefab == null || entry.Definition == null || entry.Weight <= 0f) continue;
                last = entry;
                roll -= entry.Weight;
                if (roll <= 0f) return entry;
            }
            return last;
        }

        // On the ring around the player, on the NavMesh, and reachable through the flow field
        private bool TryFindSpot(Vector3 center, EnemyDefinition definition, out Vector3 spot) {
            FlowField field = _flowField.Field;
            for (int i = 0; i < PlacementAttempts; i++) {
                float angle = Random.value * Mathf.PI * 2f;
                float distance = Random.Range(_settings.MinSpawnDistance, _settings.MaxSpawnDistance);
                Vector3 candidate = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
                spot = hit.position;
                if (definition.Navigation == EnemyNavigation.FlowField && field != null && !field.IsReachable(field.CellOf(spot))) continue;
                return true;
            }
            spot = default;
            return false;
        }

        private void PublishStatus() => _eventBus.Publish(new HordeStatusEvent(Spawning, _alive.Count));

        public void Dispose() {
            _subscriptions.Dispose();
            foreach (Character enemy in _alive) if (enemy != null) enemy.Died -= OnEnemyDied;
        }
    }
}
