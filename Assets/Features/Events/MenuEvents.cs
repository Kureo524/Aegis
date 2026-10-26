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
    /// <summary>Main menu "Play": open the champion select screen.</summary>
    public readonly struct ChampionSelectRequestedEvent : IEvent { }
    /// <summary>The player picks a champion of the roster for the next runs.</summary>
    public readonly struct ChampionPickRequestedEvent : IEvent {
        public readonly Characters.CharacterDefinition Champion;
        public ChampionPickRequestedEvent(Characters.CharacterDefinition champion) => Champion = champion;
    }
    /// <summary>The champion of the next runs changed (ChampionSelection).</summary>
    public readonly struct ChampionSelectedEvent : IEvent {
        public readonly Characters.CharacterDefinition Champion;
        public ChampionSelectedEvent(Characters.CharacterDefinition champion) => Champion = champion;
    }

    /// <summary>A UserOptions value changed (already saved).</summary>
    public readonly struct UserOptionsChangedEvent : IEvent { }

    /// <summary>Play the next runs on this stage (champion select screen → StageSelection).</summary>
    public readonly struct StagePickRequestedEvent : IEvent {
        public readonly Run.StageDefinition Stage;
        public StagePickRequestedEvent(Run.StageDefinition stage) => Stage = stage;
    }
    /// <summary>The stage of the next runs changed (StageSelection).</summary>
    public readonly struct StageSelectedEvent : IEvent {
        public readonly Run.StageDefinition Stage;
        public StageSelectedEvent(Run.StageDefinition stage) => Stage = stage;
    }
}
