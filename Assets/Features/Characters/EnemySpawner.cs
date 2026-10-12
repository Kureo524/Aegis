using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Features.Characters {
    /// <summary>One character to spawn: which prefab variant, which data, where.</summary>
    [Serializable]
    public struct CharacterSpawn {
        public Character Prefab;
        public CharacterDefinition Definition;
        public Transform Point;
    }

    /// <summary>
    /// Spawns the scene's fixed characters (training dummies for now) through the container.
    /// The wave spawner of phase 6 will replace it for real enemies.
    /// </summary>
    public sealed class EnemySpawner : IStartable {
        private readonly IObjectResolver _resolver;
        private readonly IReadOnlyList<CharacterSpawn> _spawns;

        public EnemySpawner(IObjectResolver resolver, IReadOnlyList<CharacterSpawn> spawns) {
            _resolver = resolver;
            _spawns = spawns;
        }

        public void Start() {
            foreach (CharacterSpawn spawn in _spawns) {
                if (spawn.Prefab == null || spawn.Definition == null || spawn.Point == null) continue;
                Character character = _resolver.Instantiate(spawn.Prefab, spawn.Point.position, spawn.Point.rotation);
                character.name = spawn.Definition.DisplayName;
                character.Init(spawn.Definition);
            }
        }
    }
}
