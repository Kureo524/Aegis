using System;
using Features.Events;
using Features.Run;
using Features.StateMachines;
using Systems.EventBusses;
using Systems.Inputs;
using VContainer.Unity;

namespace Features.Flow {
    /// <summary>Enables the action map matching the game state: Gameplay while in Game (except
    /// during the end-of-wave screens), UI otherwise (design doc §14.3).</summary>
    public sealed class InputMapSwitcher : IInitializable, IDisposable {
        private readonly IEventBus _eventBus;
        private readonly InputReader _inputReader;
        private readonly DisposableBag _subscriptions = new();
        private GameState _gameState;
        private bool _inReward;

        public InputMapSwitcher(IEventBus eventBus, InputReader inputReader) {
            _eventBus = eventBus;
            _inputReader = inputReader;
        }

        public void Initialize() {
            _subscriptions.Add(_eventBus.Subscribe<GameStateChangedEvent>(e => {
                _gameState = e.To;
                if (e.To is GameState.Menu or GameState.Loading) _inReward = false;
                Apply();
            }));
            _subscriptions.Add(_eventBus.Subscribe<RunStateChangedEvent>(e => {
                _inReward = e.To == RunState.Reward;
                Apply();
            }));
        }

        public void Dispose() => _subscriptions.Dispose();

        private void Apply() {
            if (_gameState == GameState.Game && !_inReward) {
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
