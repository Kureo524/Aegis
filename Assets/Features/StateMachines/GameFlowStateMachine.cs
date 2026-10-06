using System;
using Systems.StateMachine;

namespace Features.StateMachines {
    
    [Serializable] public enum GameState { Menu, Game, Pause, GameOver }
    
    public class GameFlowStateMachine : GameStateMachine<GameState> { }
}
