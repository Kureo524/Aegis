using Features.Flow;
using Features.StateMachines;
using Features.UI.Panels;
using Systems.Inputs;
using VContainer;
using VContainer.Unity;

namespace Systems.Containers {
    public class MenuLifetimeScope : LifetimeScope {

        public GameFlowStateMachine stateMachine;
        public MainMenuPanel mainMenu;

        protected override void Configure(IContainerBuilder builder) {
            builder.Register<InputReader>(Lifetime.Singleton);
            builder.RegisterComponent(stateMachine);
            builder.RegisterComponent(mainMenu);
            builder.RegisterEntryPoint<GameFlowController>();
        }
    }
}