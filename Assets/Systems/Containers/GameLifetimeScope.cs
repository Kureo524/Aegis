using Features.StateMachines;
using Systems.StateMachine;
using VContainer;
using VContainer.Unity;

namespace Systems.Containers {
    
    
    public class GameLifetimeScope : LifetimeScope {
    
        public GameFlowStateMachine stateMachine;
    
        protected override void Configure(IContainerBuilder builder) {
            builder.RegisterComponent(stateMachine);

        }
    }
}
