using System;
using Systems.StateMachine;

namespace Features.StateMachines {
    
    // [NEW] Added Loading and Win states per design doc §14.2
    [Serializable] public enum GameState { Menu, Loading, Game, Pause, Win, GameOver }
    
    public class GameFlowStateMachine : GameStateMachine<GameState> { }
}
