using Systems.EventBusses;

namespace Features.Events {
    /// <summary>Requests handled by GameFlowController (design doc §14.3).</summary>
    public readonly struct PlayRequestedEvent : IEvent { }
    public readonly struct QuitRequestedEvent : IEvent { }
    /// <summary>Game → Pause (Escape with nothing to cancel).</summary>
    public readonly struct PauseRequestedEvent : IEvent { }
    /// <summary>Pause → Game.</summary>
    public readonly struct ResumeRequestedEvent : IEvent { }
    /// <summary>Back to the main menu (from Pause, Win, GameOver).</summary>
    public readonly struct MainMenuRequestedEvent : IEvent { }

    /// <summary>Open the options screen (main menu "Settings" button).</summary>
    public readonly struct OptionsRequestedEvent : IEvent { }
    /// <summary>A UserOptions value changed (already saved).</summary>
    public readonly struct UserOptionsChangedEvent : IEvent { }
}
