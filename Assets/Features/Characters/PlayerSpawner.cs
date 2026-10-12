using System;
using Features.Events;
using Features.Flow;
using Features.Movement;
using Systems.EventBusses;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Features.Characters {
    /// <summary>
    /// Spawns the player from its prefab (through the container, so every component of the prefab
    /// gets injected), initializes it from its definition and announces it on the bus
    /// (design doc §14.4). Game scope entry point.
    /// Until the stage lives and the game over (phase 7), a dead player comes back at the spawn
    /// point after GameFlowSettings.PlayerRespawnDelay, with full HP.
    /// </summary>
    public sealed class PlayerSpawner : IStartable, ITickable, IDisposable {
        private readonly IObjectResolver _resolver;
        private readonly IEventBus _eventBus;
        private readonly GameFlowSettings _flowSettings;
        private readonly Character _prefab;
        private readonly CharacterDefinition _definition;
        private readonly Transform _spawnPoint;
        private IDisposable _subscription;
        private Character _player;
        private float _respawnAt = -1f;

        public PlayerSpawner(IObjectResolver resolver, IEventBus eventBus, GameFlowSettings flowSettings,
                             Character prefab, CharacterDefinition definition, Transform spawnPoint) {
            _resolver = resolver;
            _eventBus = eventBus;
            _flowSettings = flowSettings;
            _prefab = prefab;
            _definition = definition;
            _spawnPoint = spawnPoint;
        }

        public void Start() {
            _player = _resolver.Instantiate(_prefab, _spawnPoint.position, _spawnPoint.rotation);
            _player.name = $"Player ({_definition.DisplayName})";
            _player.Init(_definition);
            _subscription = _eventBus.Subscribe<PlayerDiedEvent>(_ => _respawnAt = Time.time + _flowSettings.PlayerRespawnDelay);
            _eventBus.Publish(new PlayerSpawnedEvent(_player.transform));
        }

        public void Tick() {
            if (_respawnAt < 0f || Time.time < _respawnAt) return;
            _respawnAt = -1f;
            _player.GetComponent<CharacterMovement>().Teleport(_spawnPoint.position);
            _player.Revive();
        }

        public void Dispose() => _subscription?.Dispose();
    }
}
