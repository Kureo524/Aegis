using Systems.EventBusses;

namespace Features.Events {
    /// <summary>The run's score changed (ScoreService → run HUD).</summary>
    public readonly struct ScoreChangedEvent : IEvent {
        public readonly int Score;
        public readonly int Delta;
        public ScoreChangedEvent(int score, int delta) {
            Score = score;
            Delta = delta;
        }
    }

    /// <summary>Final score of a finished run (ScoreService → MetaProgress, end screen).</summary>
    public readonly struct RunScoredEvent : IEvent {
        public readonly int Score;
        public readonly bool Won;
        public RunScoredEvent(int score, bool won) {
            Score = score;
            Won = won;
        }
    }

    /// <summary>The saved best score after a run (MetaProgress → end screen).</summary>
    public readonly struct BestScoreEvent : IEvent {
        public readonly int Best;
        public readonly bool NewRecord;
        public BestScoreEvent(int best, bool newRecord) {
            Best = best;
            NewRecord = newRecord;
        }
    }
}
