using Features.Events;
using Systems.EventBusses;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Features.Characters {
    /// <summary>
    /// Spawns the player from its prefab (through the container, so every component of the prefab
    /// gets injected), initializes it from its definition and announces it on the bus
    /// (design doc §14.4). Game scope entry point.
    /// </summary>
    public sealed class PlayerSpawner : IStartable {
        private readonly IObjectResolver _resolver;
        private readonly IEventBus _eventBus;
        private readonly Character _prefab;
        private readonly CharacterDefinition _definition;
        private readonly Transform _spawnPoint;

        public PlayerSpawner(IObjectResolver resolver, IEventBus eventBus,
                             Character prefab, CharacterDefinition definition, Transform spawnPoint) {
            _resolver = resolver;
            _eventBus = eventBus;
            _prefab = prefab;
            _definition = definition;
            _spawnPoint = spawnPoint;
        }

        public void Start() {
            Character player = _resolver.Instantiate(_prefab, _spawnPoint.position, _spawnPoint.rotation);
            player.name = $"Player ({_definition.DisplayName})";
            player.Init(_definition);
            _eventBus.Publish(new PlayerSpawnedEvent(player.transform));
        }
    }
}
