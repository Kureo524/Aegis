using System;
using System.Collections.Generic;
using Features.Events;
using Systems.EventBusses;
using VContainer.Unity;

namespace Features.Run {
    /// <summary>
    /// Plays the end-of-wave rewards one after the other (design doc §9.3): for each RewardKind of
    /// the WaveRewardEvent, publishes a RewardStepEvent and waits for its RewardStepDoneEvent (shop
    /// closed, anvil resolved, augment screen closed). When none is left: RewardClosedEvent, the
    /// RunDirector starts the next countdown. Rewards earned during the wave (BonusRewardEarnedEvent,
    /// e.g. a stat anvil picked up, §9.5) open first. Game scope service.
    /// </summary>
    public sealed class RewardController : IStartable, IDisposable {
        private readonly IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();
        private readonly Queue<RewardKind> _pending = new();
        private readonly List<RewardKind> _earned = new();
        private bool _active;

        public RewardKind? Current { get; private set; }

        public RewardController(IEventBus eventBus) {
            _eventBus = eventBus;
        }

        public void Start() {
            _subscriptions.Add(_eventBus.Subscribe<WaveRewardEvent>(e => {
                _pending.Clear();
                foreach (RewardKind kind in _earned) _pending.Enqueue(kind);
                _earned.Clear();
                if (e.Rewards != null) foreach (RewardKind kind in e.Rewards) _pending.Enqueue(kind);
                _active = true;
                Next();
            }));
            _subscriptions.Add(_eventBus.Subscribe<BonusRewardEarnedEvent>(e => _earned.Add(e.Kind)));
            _subscriptions.Add(_eventBus.Subscribe<RewardStepDoneEvent>(_ => {
                if (_active) Next();
            }));
        }

        private void Next() {
            if (_pending.Count == 0) {
                _active = false;
                Current = null;
                _eventBus.Publish(new RewardClosedEvent());
                return;
            }
            Current = _pending.Dequeue();
            _eventBus.Publish(new RewardStepEvent(Current.Value));
        }

        public void Dispose() => _subscriptions.Dispose();
    }
}
