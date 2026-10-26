using System;
using System.Collections.Generic;
using Features.Events;
using Systems.EventBusses;
using VContainer.Unity;

namespace Features.Run {
    /// <summary>
    /// The stage of the next runs (design doc §9.1), root scope. Null until a stage is picked
    /// (StagePickRequestedEvent, sent by the champion select screen when the run starts): a game
    /// scene opened directly (tests, editor) then plays its own stage.
    /// </summary>
    public sealed class StageSelection : IStartable, IDisposable {
        private readonly StageCatalog _catalog;
        private readonly IEventBus _eventBus;
        private IDisposable _subscription;

        public StageSelection(StageCatalog catalog, IEventBus eventBus) {
            _catalog = catalog;
            _eventBus = eventBus;
        }

        public IReadOnlyList<StageDefinition> Stages =>
            _catalog != null ? _catalog.Stages : Array.Empty<StageDefinition>();

        public StageDefinition Default => _catalog != null ? _catalog.Default : null;

        /// <summary>The picked stage; null: the game scene's own one.</summary>
        public StageDefinition Current { get; private set; }

        public void Start() => _subscription = _eventBus.Subscribe<StagePickRequestedEvent>(e => Select(e.Stage));

        private void Select(StageDefinition stage) {
            if (stage == null || _catalog == null || Current == stage) return;
            bool listed = false;
            foreach (StageDefinition s in _catalog.Stages) listed |= s == stage;
            if (!listed) return;
            Current = stage;
            _eventBus.Publish(new StageSelectedEvent(stage));
        }

        public void Dispose() => _subscription?.Dispose();
    }
}
