using Systems.EventBusses;

namespace Features.Events {
    /// <summary>The meta progression changed (currency, a node bought): menu screens refresh.</summary>
    public readonly struct MetaChangedEvent : IEvent { }

    /// <summary>A finished run paid this much persistent currency (end screen).</summary>
    public readonly struct MetaRewardEvent : IEvent {
        public readonly int Amount;
        public readonly string Currency;

        public MetaRewardEvent(int amount, string currency) {
            Amount = amount;
            Currency = currency;
        }
    }

    /// <summary>The main menu's "Progression" button: opens the meta tree.</summary>
    public readonly struct MetaTreeRequestedEvent : IEvent { }
}
