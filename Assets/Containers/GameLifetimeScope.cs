using Features.CameraControl;
using Features.Characters;
using Features.Controls;
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
        public PlayerCommandInput playerCommandInput;
        public CameraController cameraController;
        public QuitOverlay quitOverlay;
        public CharacterDebugPanel characterDebugPanel;

        [Header("Player")]
        public Character player;
        public CharacterDefinition playerDefinition;

        protected override void Configure(IContainerBuilder builder) {
            builder.RegisterComponent(playerCommandInput);
            builder.RegisterComponent(cameraController);
            if (quitOverlay != null) builder.RegisterComponent(quitOverlay);
            if (characterDebugPanel != null) builder.RegisterComponent(characterDebugPanel);

            builder.RegisterComponent(player);
            builder.RegisterEntryPoint<PlayerBootstrap>().WithParameter(playerDefinition);

            builder.RegisterEntryPoint<SceneEntryPoint>().WithParameter(GameState.Game);
        }
    }
}
