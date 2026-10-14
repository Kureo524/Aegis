using System;
using System.Collections.Generic;
using Features.Events;
using Systems.EventBusses;
using VContainer.Unity;

namespace Features.Characters {
    /// <summary>
    /// The champion of the next runs (design doc §7.10), root scope: the roster's first one until the
    /// player picks another one on the champion select screen (ChampionPickRequestedEvent). Kept
    /// for the session; the game scope reads it to spawn the player.
    /// </summary>
    public sealed class ChampionSelection : IStartable, IDisposable {
        private readonly ChampionRoster _roster;
        private readonly IEventBus _eventBus;
        private IDisposable _subscription;

        public ChampionSelection(ChampionRoster roster, IEventBus eventBus) {
            _roster = roster;
            _eventBus = eventBus;
            Current = roster != null ? roster.Default : null;
        }

        public IReadOnlyList<CharacterDefinition> Champions =>
            _roster != null ? _roster.Champions : Array.Empty<CharacterDefinition>();

        /// <summary>Null when the roster is empty: the game scene then uses its own default.</summary>
        public CharacterDefinition Current { get; private set; }

        public void Start() => _subscription = _eventBus.Subscribe<ChampionPickRequestedEvent>(e => Select(e.Champion));

        private void Select(CharacterDefinition champion) {
            if (champion == null || _roster == null) return;
            bool listed = false;
            foreach (CharacterDefinition c in _roster.Champions) listed |= c == champion;
            if (!listed) return;
            Current = champion;
            _eventBus.Publish(new ChampionSelectedEvent(champion));
        }

        public void Dispose() => _subscription?.Dispose();
    }
}
