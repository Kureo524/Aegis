using Features.Run;
using Systems.EventBusses;

namespace Features.Events {
    /// <summary>The run's state machine changed (TimeScaleService, InputMapSwitcher, panels).</summary>
    public readonly struct RunStateChangedEvent : IEvent {
        public readonly RunState? From;
        public readonly RunState To;
        public RunStateChangedEvent(RunState? from, RunState to) {
            From = from;
            To = to;
        }
    }

    /// <summary>Everything the run HUD shows; published on every change and a few times per second for the timers.</summary>
    public readonly struct RunStatusEvent : IEvent {
        public readonly RunState State;
        /// <summary>1-based number of the current (or next) wave, bonus waves included.</summary>
        public readonly int Wave;
        public readonly int RegularWaves;
        public readonly int TotalWaves;
        public readonly int Kills;
        public readonly int Quota;
        public readonly int Lives;
        /// <summary>Combat: wave timer; PreWave: countdown; Overtime: next life loss.</summary>
        public readonly float TimeLeft;

        public RunStatusEvent(RunState state, int wave, int regularWaves, int totalWaves, int kills, int quota, int lives, float timeLeft) {
            State = state;
            Wave = wave;
            RegularWaves = regularWaves;
            TotalWaves = totalWaves;
            Kills = kills;
            Quota = quota;
            Lives = lives;
            TimeLeft = timeLeft;
        }

        public bool IsBonusWave => Wave > RegularWaves;
        public bool QuotaReached => Kills >= Quota;
    }

    /// <summary>End of a wave: the rewards scheduled for it (WaveRewardSchedule).</summary>
    public readonly struct WaveRewardEvent : IEvent {
        public readonly int Wave;
        public readonly bool Bonus;
        public readonly System.Collections.Generic.IReadOnlyList<RewardKind> Rewards;
        public WaveRewardEvent(int wave, bool bonus, System.Collections.Generic.IReadOnlyList<RewardKind> rewards) {
            Wave = wave;
            Bonus = bonus;
            Rewards = rewards;
        }
    }

    /// <summary>The end-of-wave screen was closed: the next countdown starts.</summary>
    public readonly struct RewardClosedEvent : IEvent { }

    public readonly struct LifeLostEvent : IEvent {
        public readonly int LivesLeft;
        public LifeLostEvent(int livesLeft) => LivesLeft = livesLeft;
    }

    /// <summary>A horde enemy died: it counts for the wave quota (training dummies do not).</summary>
    public readonly struct WaveEnemyKilledEvent : IEvent { }

    /// <summary>Victory (last bonus wave cleared) or defeat (no life left). GameFlowController → Win / GameOver.</summary>
    public readonly struct RunEndedEvent : IEvent {
        public readonly bool Won;
        public readonly int WavesCleared;
        public RunEndedEvent(bool won, int wavesCleared) {
            Won = won;
            WavesCleared = wavesCleared;
        }
    }

    /// <summary>Debug: end the current wave at once (quota and timer done).</summary>
    public readonly struct SkipWaveRequestedEvent : IEvent { }

    /// <summary>Start the next wave now instead of waiting for the end of the break (HUD button).</summary>
    public readonly struct NextWaveRequestedEvent : IEvent { }
}
