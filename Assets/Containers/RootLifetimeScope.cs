using Features.CameraControl;
using Features.Flow;
using Features.StateMachines;
using Systems.EventBusses;
using Systems.Inputs;
using Systems.UIManagement;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Containers {
    /// <summary>
    /// Application-wide scope (design doc §14.4). Lives on a prefab referenced by the
    /// VContainerSettings asset (Assets/Data/Settings), so VContainer instantiates it once,
    /// before the first scene scope, and keeps it across scene loads. Every scene scope is
    /// automatically its child.
    /// </summary>
    public class RootLifetimeScope : LifetimeScope {
        [SerializeField] private GameFlowStateMachine stateMachine;
        [SerializeField] private GameFlowSettings gameFlowSettings;
        [SerializeField] private CameraSettings cameraSettings;

        protected override void Configure(IContainerBuilder builder) {
            // Infrastructure
            builder.Register<EventBus>(Lifetime.Singleton).As<IEventBus>();
            builder.Register<PanelManager>(Lifetime.Singleton).As<IPanelManager>();
            builder.RegisterEntryPoint<InputReader>().AsSelf();

            // Settings (ScriptableObjects)
            builder.RegisterInstance(gameFlowSettings);
            builder.RegisterInstance(cameraSettings);

            // Game flow
            builder.RegisterComponent(stateMachine);
            builder.RegisterEntryPoint<GameFlowController>();
            builder.RegisterEntryPoint<InputMapSwitcher>();
            builder.RegisterEntryPoint<TimeScaleService>();
            builder.RegisterEntryPoint<CursorConfinement>();
        }
    }
}
