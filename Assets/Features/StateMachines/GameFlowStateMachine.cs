using System;
using Systems.StateMachine;

namespace Features.StateMachines {

    // Values are serialized as ints in the transition rules: only ever APPEND new states.
    [Serializable] public enum GameState { Menu, Loading, Game, Pause, Win, GameOver }

    /// <summary>Global game flow (design doc §14.2). Lives on the RootLifetimeScope prefab,
    /// where its transition rules are edited.</summary>
    public class GameFlowStateMachine : GameStateMachine<GameState> { }
}
