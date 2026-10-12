using Systems.EventBusses;

namespace Features.Events {
    /// <summary>Requests handled by GameFlowController (design doc §14.3).</summary>
    public readonly struct PlayRequestedEvent : IEvent { }
    public readonly struct QuitRequestedEvent : IEvent { }
}
