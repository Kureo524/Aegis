using VContainer.Unity;

namespace Features.Characters {
    /// <summary>Initializes the scene's player character from its definition when the game scope
    /// starts. Will become a spawner once the player is instantiated from a prefab.</summary>
    public sealed class PlayerBootstrap : IStartable {
        private readonly Character _player;
        private readonly CharacterDefinition _definition;

        public PlayerBootstrap(Character player, CharacterDefinition definition) {
            _player = player;
            _definition = definition;
        }

        public void Start() => _player.Init(_definition);
    }
}
