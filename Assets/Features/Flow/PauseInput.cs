using System;
using Features.Events;
using Systems.EventBusses;
using Systems.Inputs.Events;
using UnityEngine;
using VContainer.Unity;

namespace Features.Flow {
    /// <summary>
    /// Escape in game (design doc §4.1): cancels the current aim first (Q / R / F, handled by their
    /// owners); with nothing to cancel, requests the pause. Game scope entry point. Leaving the
    /// pause (Escape on the UI map, "Resume" button) belongs to the pause menu.
    /// </summary>
    public sealed class PauseInput : IInitializable, IDisposable {
        private readonly IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();
        private bool _spellAim;
        private bool _attackMoveHeld;
        private int _aimEndedFrame = -1;

        public PauseInput(IEventBus eventBus) {
            _eventBus = eventBus;
        }

        public void Initialize() {
            _subscriptions.Add(_eventBus.Subscribe<AimModeEvent>(e => {
                if (_spellAim && e.Mode == AimMode.None) _aimEndedFrame = Time.frameCount;
                _spellAim = e.Mode is AimMode.Spell or AimMode.Signature;
            }));
            _subscriptions.Add(_eventBus.Subscribe<AttackMoveEvent>(e => _attackMoveHeld = e.Pressed));
            _subscriptions.Add(_eventBus.Subscribe<CancelEvent>(_ => OnCancel()));
        }

        public void Dispose() => _subscriptions.Dispose();

        // Subscribers run in any order: an aim cancelled by this same Escape ended this frame.
        private void OnCancel() {
            if (_spellAim || _attackMoveHeld || _aimEndedFrame == Time.frameCount) return;
            _eventBus.Publish(new PauseRequestedEvent());
        }
    }
}
