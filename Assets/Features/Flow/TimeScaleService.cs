using System;
using Features.Events;
using Features.Run;
using Features.StateMachines;
using Systems.EventBusses;
using UnityEngine;
using VContainer.Unity;

namespace Features.Flow {
    /// <summary>
    /// The only place allowed to write Time.timeScale (design doc §14.2): 0 in Pause, Win and
    /// GameOver, and during the end-of-wave screens (RunState.Reward, §9.1); 1 otherwise.
    /// </summary>
    public sealed class TimeScaleService : IInitializable, IDisposable {
        private readonly IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();
        private GameState _gameState;
        private bool _inReward;

        public TimeScaleService(IEventBus eventBus) {
            _eventBus = eventBus;
        }

        public void Initialize() {
            _subscriptions.Add(_eventBus.Subscribe<GameStateChangedEvent>(e => {
                _gameState = e.To;
                if (e.To is GameState.Menu or GameState.Loading) _inReward = false;   // new scene, new run
                Apply();
            }));
            _subscriptions.Add(_eventBus.Subscribe<RunStateChangedEvent>(e => {
                _inReward = e.To == RunState.Reward;
                Apply();
            }));
        }

        public void Dispose() {
            _subscriptions.Dispose();
            Time.timeScale = 1f;
        }

        private void Apply() {
            bool frozen = _gameState is GameState.Pause or GameState.Win or GameState.GameOver || _inReward;
            Time.timeScale = frozen ? 0f : 1f;
        }
    }
}
