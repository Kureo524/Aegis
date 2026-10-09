using Features.StateMachines;
using Systems.EventBusses;

namespace Features.Events {
    // [NEW] Event published when the game state changes (design doc §14.3)
    public readonly struct GameStateChangedEvent : IEvent {
        public readonly GameState From;
        public readonly GameState To;

        public GameStateChangedEvent(GameState from, GameState to) {
            From = from;
            To = to;
        }
    }
}
