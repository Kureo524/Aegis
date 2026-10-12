using System;
using Systems.Characters;
using Systems.EventBusses;
using Systems.Inputs;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Systems.Containers {
    // [NEW] Scope for the test scene: provides IEventBus + InputReader and injects scene components
    public class GameLifetimeScope : LifetimeScope {

        public PlayerCommandInput playerCommandInput;
        public CameraController cameraController;

        protected override void Configure(IContainerBuilder builder) {
            builder.Register<EventBus>(Lifetime.Singleton).As<IEventBus>();
            builder.Register<InputReader>(Lifetime.Singleton);

            builder.RegisterComponent(playerCommandInput);
            builder.RegisterComponent(cameraController);

            builder.RegisterEntryPoint<GameInputInitializer>();
        }
    }

    // [NEW] Initializes the InputReader. Cursor confinement is build-only (see Start).
    public class GameInputInitializer : IStartable, IDisposable {
        private readonly InputReader _inputReader;
        private readonly IEventBus _eventBus;

        public GameInputInitializer(InputReader inputReader, IEventBus eventBus) {
            _inputReader = inputReader;
            _eventBus = eventBus;
        }

        public void Start() {
            _inputReader.Initialize(_eventBus, EActionTypes.Game);

            // Cursor confinement is standalone-build-only and must NEVER run in the Editor.
            // Cursor.lockState is process-global: on Wayland/Hyprland a Confined lock leaks
            // out of the Game view and desyncs the pointer for the whole Editor, so clicks
            // land away from the visible cursor. Observed on this machine — do not remove
            // the guard.
#if !UNITY_EDITOR
            Cursor.lockState = CursorLockMode.Confined;
#endif
        }

        public void Dispose() {
#if !UNITY_EDITOR
            // Release the cursor when the scope is torn down
            Cursor.lockState = CursorLockMode.None;
#endif
        }
    }
}
