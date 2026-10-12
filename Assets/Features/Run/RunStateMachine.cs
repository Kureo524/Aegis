using System;
using Systems.StateMachine;

namespace Features.Run {
    /// <summary>States of a wave inside a run (design doc §14.2). Only append at the end: the rules are serialized as integers.</summary>
    [Serializable] public enum RunState { PreWave, Combat, Overtime, Reward }

    /// <summary>
    /// The run's state machine (design doc §9.1, §14.2), driven by <see cref="RunDirector"/>.
    /// Lives in the game scene; its transition rules are written by Tools > Project Setup.
    /// </summary>
    public class RunStateMachine : GameStateMachine<RunState> { }
}
