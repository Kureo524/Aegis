using System;
using Features.Events;
using Systems.EventBusses;
using VContainer.Unity;

namespace Features.Score {
    /// <summary>
    /// The run's score (design doc §9.9), built only from bus events: horde kills, bosses, waves
    /// cleared, then the victory bonus. Publishes ScoreChangedEvent on every change and
    /// RunScoredEvent once at the end of the run (MetaProgress keeps the best one). Nothing
    /// references it: removing it from the game scope only removes the score from the HUD and
    /// the end screen. Game scope entry point.
    /// </summary>
    public sealed class ScoreService : IStartable, IDisposable {
        private readonly ScoreRules _rules;
        private readonly IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();
        private int _lives;
        private bool _ended;

        public int Score { get; private set; }

        public ScoreService(ScoreRules rules, IEventBus eventBus) {
            _rules = rules;
            _eventBus = eventBus;
        }

        public void Start() {
            _subscriptions.Add(_eventBus.Subscribe<WaveEnemyKilledEvent>(_ => Add(_rules.PointsPerKill)));
            _subscriptions.Add(_eventBus.Subscribe<BossDefeatedEvent>(_ => Add(_rules.PointsPerBoss)));
            _subscriptions.Add(_eventBus.Subscribe<WaveRewardEvent>(_ => Add(_rules.PointsPerWave)));
            _subscriptions.Add(_eventBus.Subscribe<RunStatusEvent>(e => _lives = e.Lives));
            _subscriptions.Add(_eventBus.Subscribe<RunEndedEvent>(OnRunEnded));
            _eventBus.Publish(new ScoreChangedEvent(Score, 0));
        }

        // The last wave opens no reward screen: it is counted here, with the victory bonus
        private void OnRunEnded(RunEndedEvent e) {
            if (_ended) return;
            if (e.Won) Add(_rules.PointsPerWave + _rules.VictoryBonus + _lives * _rules.PointsPerLifeLeft);
            _ended = true;
            _eventBus.Publish(new RunScoredEvent(Score, e.Won));
        }

        private void Add(int points) {
            if (_ended || points <= 0) return;
            Score += points;
            _eventBus.Publish(new ScoreChangedEvent(Score, points));
        }

        public void Dispose() => _subscriptions.Dispose();
    }
}
