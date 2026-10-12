using System.Collections.Generic;
using Features.CameraControl;
using Features.Characters;
using Features.Combat;
using Features.Spells;
using Systems.Pooling;
using Features.Flow;
using Features.StateMachines;
using Features.UI;
using Features.UI.DebugTools;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Containers {
    /// <summary>
    /// Game scene scope (TestScene for now). Destroyed with the scene, so all run state is reset
    /// on every load (design doc §14.4). Shared services come from the RootLifetimeScope.
    /// </summary>
    public class GameLifetimeScope : LifetimeScope {
        public Camera mainCamera;
        public CameraController cameraController;
        public QuitOverlay quitOverlay;
        public CharacterDebugPanel characterDebugPanel;
        public PlayerHud playerHud;
        public DamageNumbersView damageNumbers;

        [Header("Player")]
        [Tooltip("Player prefab variant (Assets/Prefabs/Characters/Player).")]
        public Character playerPrefab;
        public CharacterDefinition playerDefinition;
        public Transform playerSpawn;

        [Header("Enemies")]
        [Tooltip("Fixed characters spawned at scene start (training dummies).")]
        public List<CharacterSpawn> enemySpawns = new();

        protected override void Configure(IContainerBuilder builder) {
            builder.RegisterComponent(mainCamera);
            builder.Register<PrefabPool>(Lifetime.Scoped);
            builder.Register<ProjectileLauncher>(Lifetime.Scoped);
            builder.Register<SpellExecutor>(Lifetime.Scoped);
            builder.RegisterComponent(cameraController);
            if (quitOverlay != null) builder.RegisterComponent(quitOverlay);
            if (characterDebugPanel != null) builder.RegisterComponent(characterDebugPanel);
            if (playerHud != null) builder.RegisterComponent(playerHud);
            if (damageNumbers != null) builder.RegisterComponent(damageNumbers);

            builder.RegisterEntryPoint<PlayerSpawner>()
                .WithParameter(playerPrefab)
                .WithParameter(playerDefinition)
                .WithParameter(playerSpawn);
            builder.RegisterEntryPoint<EnemySpawner>()
                .WithParameter<IReadOnlyList<CharacterSpawn>>(enemySpawns);

            builder.RegisterEntryPoint<SceneEntryPoint>().WithParameter(GameState.Game);
        }
    }
}
