using Features.CameraControl;
using Features.Controls;
using Features.Flow;
using Features.StateMachines;
using Features.UI;
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

        protected override void Configure(IContainerBuilder builder) {
            builder.RegisterComponent(playerCommandInput);
            builder.RegisterComponent(cameraController);
            if (quitOverlay != null) builder.RegisterComponent(quitOverlay);

            builder.RegisterEntryPoint<SceneEntryPoint>().WithParameter(GameState.Game);
        }
    }
}
