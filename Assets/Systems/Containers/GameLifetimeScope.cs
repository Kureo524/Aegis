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

    // [NEW] Initializes the InputReader and confines the mouse to the game window
    public class GameInputInitializer : IStartable, IDisposable {
        private readonly InputReader _inputReader;
        private readonly IEventBus _eventBus;

        public GameInputInitializer(InputReader inputReader, IEventBus eventBus) {
            _inputReader = inputReader;
            _eventBus = eventBus;
        }

        public void Start() {
            _inputReader.Initialize(_eventBus, EActionTypes.Game);

            // Keep the cursor inside the game window (visible, but clamped to the borders)
            Cursor.lockState = CursorLockMode.Confined;
        }

        public void Dispose() {
            // Release the cursor when the scope is torn down
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
