using System;
using System.Collections.Generic;
using Features.Characters;
using Features.Events;
using Systems.EventBusses;
using UnityEngine;
using VContainer.Unity;

namespace Features.Enemies {
    /// <summary>
    /// Central tick of the enemy AI (design doc §9.8, §12: no decision Update per enemy). Holds the
    /// active <see cref="EnemyAI"/>s and lets each of them think once per ThinkInterval, the
    /// decisions being spread over the frames. Also the AI's view of the player.
    /// Game scope service.
    /// </summary>
    public sealed class EnemyDirector : ITickable, IDisposable {
        private readonly HordeSettings _settings;
        private readonly IDisposable _subscription;
        private readonly List<EnemyAI> _enemies = new();
        private int _cursor;
        private float _budget;

        /// <summary>The player, once spawned.</summary>
        public Character Player { get; private set; }
        public int Count => _enemies.Count;

        public EnemyDirector(HordeSettings settings, IEventBus eventBus) {
            _settings = settings;
            _subscription = eventBus.Subscribe<PlayerSpawnedEvent>(e => Player = e.Player.GetComponent<Character>());
        }

        public void Register(EnemyAI enemy) {
            if (!_enemies.Contains(enemy)) _enemies.Add(enemy);
        }

        public void Unregister(EnemyAI enemy) {
            int index = _enemies.IndexOf(enemy);
            if (index < 0) return;
            _enemies.RemoveAt(index);
            if (index < _cursor) _cursor--;
        }

        public void Tick() {
            int count = _enemies.Count;
            if (count == 0) return;
            // Each enemy once per ThinkInterval: count × dt / interval decisions this frame
            _budget += count * Time.deltaTime / _settings.ThinkInterval;
            int decisions = Mathf.Min(count, (int)_budget);
            _budget = Mathf.Min(_budget - decisions, count);

            for (int i = 0; i < decisions; i++) {
                if (_cursor >= _enemies.Count) _cursor = 0;
                if (_enemies.Count == 0) return;
                _enemies[_cursor++].Think();
            }
        }

        public void Dispose() => _subscription.Dispose();
    }
}
