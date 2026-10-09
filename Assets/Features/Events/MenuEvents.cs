using Systems.EventBusses;

namespace Features.Events {
    // [NEW] Events for menu actions (design doc §14.3)
    public readonly struct PlayRequestedEvent : IEvent { }
    public readonly struct QuitRequestedEvent : IEvent { }
}
