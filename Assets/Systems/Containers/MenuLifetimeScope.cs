using Features.Flow;
using Features.StateMachines;
using Features.UI;
using Features.UI.Panels;
using Systems.EventBusses;
using Systems.Inputs;
using VContainer;
using VContainer.Unity;

namespace Systems.Containers {
    public class MenuLifetimeScope : LifetimeScope {

        public GameFlowStateMachine stateMachine;
        public MainMenuPanel mainMenu;
        public GameHUD gameHUD;

        protected override void Configure(IContainerBuilder builder) {
            // [NEW] Register IEventBus as singleton (design doc §14.4)
            builder.Register<EventBus>(Lifetime.Singleton).As<IEventBus>();
            
            builder.Register<InputReader>(Lifetime.Singleton);
            builder.RegisterComponent(stateMachine);
            builder.RegisterComponent(mainMenu);
            builder.RegisterComponent(gameHUD);
            builder.RegisterEntryPoint<GameFlowController>();
        }
    }
}