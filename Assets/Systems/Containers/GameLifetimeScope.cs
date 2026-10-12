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

    // [NEW] Initializes the InputReader, confines the mouse to the game window
    public class GameInputInitializer : IStartable, ITickable, IDisposable {
        private readonly InputReader _inputReader;
        private readonly IEventBus _eventBus;

        public GameInputInitializer(InputReader inputReader, IEventBus eventBus) {
            _inputReader = inputReader;
            _eventBus = eventBus;
        }

        public void Start() {
            _inputReader.Initialize(_eventBus, EActionTypes.Game);

            // Free bonus for real builds: native confinement (no-op in the Editor).
            Cursor.lockState = CursorLockMode.Confined;
        }

        // Software clamp, driven every frame — this is what actually works in the Editor.
        public void Tick() {
            _inputReader.ClampCursorToWindow();
        }

        public void Dispose() {
            // Release the cursor when the scope is torn down
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
