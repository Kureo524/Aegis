using System;
using Features.Events;
using Features.StateMachines;
using Systems.EventBusses;
using Systems.Inputs;
using VContainer.Unity;

namespace Features.Flow {
    /// <summary>Enables the action map matching the game state: Gameplay while in Game, UI
    /// otherwise (design doc §14.3).</summary>
    public sealed class InputMapSwitcher : IInitializable, IDisposable {
        private readonly IEventBus _eventBus;
        private readonly InputReader _inputReader;
        private IDisposable _subscription;

        public InputMapSwitcher(IEventBus eventBus, InputReader inputReader) {
            _eventBus = eventBus;
            _inputReader = inputReader;
        }

        public void Initialize() {
            _subscription = _eventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
        }

        public void Dispose() => _subscription?.Dispose();

        private void OnStateChanged(GameStateChangedEvent evt) {
            bool inGame = evt.To == GameState.Game;
            if (inGame) {
                _inputReader.DisableInput(EActionTypes.UI);
                _inputReader.EnableInput(EActionTypes.Game);
            }
            else {
                _inputReader.DisableInput(EActionTypes.Game);
                _inputReader.EnableInput(EActionTypes.UI);
            }
        }
    }
}
