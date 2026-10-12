using System;
using Features.Events;
using Systems.EventBusses;
using VContainer.Unity;

namespace Features.Economy {
    /// <summary>
    /// The run's gold (design doc §9.2): credited at each kill (EnemyKilledEvent.Gold), spent in the
    /// shops later (phase 7). Game scope, so a new run starts at 0. Publishes GoldChangedEvent.
    /// </summary>
    public sealed class Wallet : IStartable, IDisposable {
        private readonly IEventBus _eventBus;
        private readonly IDisposable _subscription;

        public int Gold { get; private set; }

        public Wallet(IEventBus eventBus) {
            _eventBus = eventBus;
            _subscription = eventBus.Subscribe<EnemyKilledEvent>(e => Add(e.Gold));
        }

        public void Start() => _eventBus.Publish(new GoldChangedEvent(Gold, 0));

        public void Add(int amount) {
            if (amount <= 0) return;
            Gold += amount;
            _eventBus.Publish(new GoldChangedEvent(Gold, amount));
        }

        public bool TrySpend(int amount) {
            if (amount < 0 || amount > Gold) return false;
            Gold -= amount;
            _eventBus.Publish(new GoldChangedEvent(Gold, -amount));
            return true;
        }

        public void Dispose() => _subscription.Dispose();
    }
}
