using System;
using Features.Events;
using Features.StateMachines;
using Systems.EventBusses;
using UnityEngine;
using VContainer.Unity;

namespace Features.Flow {
    /// <summary>The only place allowed to write Time.timeScale (design doc §14.2).</summary>
    public sealed class TimeScaleService : IInitializable, IDisposable {
        private readonly IEventBus _eventBus;
        private IDisposable _subscription;

        public TimeScaleService(IEventBus eventBus) {
            _eventBus = eventBus;
        }

        public void Initialize() {
            _subscription = _eventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
        }

        public void Dispose() {
            _subscription?.Dispose();
            Time.timeScale = 1f;
        }

        private static void OnStateChanged(GameStateChangedEvent evt) {
            Time.timeScale = evt.To == GameState.Pause ? 0f : 1f;
        }
    }
}
