using System;
using Features.Events;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.AI;
using VContainer.Unity;

namespace Features.Enemies {
    /// <summary>
    /// Keeps the horde's <see cref="FlowField"/> pointed at the player (design doc §9.6). The grid
    /// walkability is read once from the baked NavMesh (walls are carved in it), the field is
    /// recomputed at most every FlowFieldInterval, and only when the player changed cell.
    /// Game scope service.
    /// </summary>
    public sealed class FlowFieldService : IStartable, ITickable, IDisposable {
        private readonly HordeSettings _settings;
        private readonly IEventBus _eventBus;
        private IDisposable _subscription;
        private Transform _player;
        private float _nextUpdate;
        private int _lastCell = -2;

        public FlowField Field { get; private set; }

        public FlowFieldService(HordeSettings settings, IEventBus eventBus) {
            _settings = settings;
            _eventBus = eventBus;
            // At construction: the player is spawned in another entry point's Start
            _subscription = _eventBus.Subscribe<PlayerSpawnedEvent>(e => _player = e.Player);
        }

        public void Start() {
            Vector2 size = _settings.GridSize;
            float cell = _settings.CellSize;
            int width = Mathf.Max(1, Mathf.CeilToInt(size.x / cell));
            int depth = Mathf.Max(1, Mathf.CeilToInt(size.y / cell));
            Vector3 origin = _settings.GridCenter - new Vector3(width * cell, 0f, depth * cell) / 2f;
            float tolerance = cell * 0.5f;
            Field = new FlowField(origin, width, depth, cell,
                p => NavMesh.SamplePosition(p, out NavMeshHit hit, tolerance, NavMesh.AllAreas)
                     && Mathf.Abs(hit.position.x - p.x) <= tolerance && Mathf.Abs(hit.position.z - p.z) <= tolerance);
        }

        public void Tick() {
            if (Field == null || _player == null || Time.time < _nextUpdate) return;
            _nextUpdate = Time.time + _settings.FlowFieldInterval;
            int cell = Field.CellOf(_player.position);
            if (cell == _lastCell) return;
            _lastCell = cell;
            Field.Compute(_player.position);
        }

        /// <summary>Forces a recomputation on the next tick (player teleported…).</summary>
        public void Invalidate() {
            _lastCell = -2;
            _nextUpdate = 0f;
        }

        public void Dispose() => _subscription?.Dispose();
    }
}
