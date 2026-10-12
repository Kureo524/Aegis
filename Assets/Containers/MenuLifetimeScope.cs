using Features.Flow;
using Features.StateMachines;
using Features.UI.Panels;
using VContainer;
using VContainer.Unity;

namespace Containers {
    /// <summary>Menu scene scope. Shared services come from the RootLifetimeScope.</summary>
    public class MenuLifetimeScope : LifetimeScope {
        public MainMenuPanel mainMenu;

        protected override void Configure(IContainerBuilder builder) {
            builder.RegisterComponent(mainMenu);
            builder.RegisterEntryPoint<SceneEntryPoint>().WithParameter(GameState.Menu);
        }
    }
}
