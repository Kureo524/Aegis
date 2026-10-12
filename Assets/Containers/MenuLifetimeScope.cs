using Features.Flow;
using Features.StateMachines;
using Features.UI.Panels;
using VContainer;
using VContainer.Unity;

namespace Containers {
    /// <summary>Menu scene scope. Shared services come from the RootLifetimeScope.</summary>
    public class MenuLifetimeScope : LifetimeScope {
        public MainMenuPanel mainMenu;
        public OptionsPanel optionsPanel;

        protected override void Configure(IContainerBuilder builder) {
            builder.RegisterComponent(mainMenu);
            if (optionsPanel != null) builder.RegisterComponent(optionsPanel);
            builder.RegisterEntryPoint<SceneEntryPoint>().WithParameter(GameState.Menu);
        }
    }
}
