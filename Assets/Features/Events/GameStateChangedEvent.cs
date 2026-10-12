using Features.StateMachines;
using Systems.EventBusses;

namespace Features.Events {
    /// <summary>Published by GameFlowController on every game state change (design doc §14.3).</summary>
    public readonly struct GameStateChangedEvent : IEvent {
        /// <summary>Null for the very first state of the session.</summary>
        public readonly GameState? From;
        public readonly GameState To;

        public GameStateChangedEvent(GameState? from, GameState to) {
            From = from;
            To = to;
        }
    }
}
