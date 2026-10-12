using System;
using Features.Events;
using Systems.EventBusses;
using Systems.Inputs.Events;
using UnityEngine;
using VContainer;

namespace Features.UI {
    /// <summary>
    /// Debug "Quit" button for the test scene: on a tiling WM with a confined cursor, the window
    /// cannot be closed otherwise. Clicking it publishes <see cref="QuitRequestedEvent"/>;
    /// GameFlowController does the actual quitting.
    ///
    /// The button is only drawn with IMGUI; the click comes from ConfirmEvent (left click
    /// *press*), never from GUI.Button, which also fires while the button is merely held — at
    /// startup the cursor can sit on the button with a stale pressed state and the game used to
    /// quit by itself. Escape is deliberately not used: it is the cancel/pause key (§4.1).
    /// </summary>
    public class QuitOverlay : MonoBehaviour {
        [SerializeField] private int buttonWidth = 90;
        [SerializeField] private int buttonHeight = 32;
        [SerializeField] private int margin = 12;

        private IEventBus _eventBus;
        private IDisposable _subscription;

        [Inject]
        public void Construct(IEventBus eventBus) {
            _eventBus = eventBus;
        }

        private void Start() {
            _subscription = _eventBus.Subscribe<ConfirmEvent>(OnConfirm);
        }

        private void OnDestroy() => _subscription?.Dispose();

        private void OnGUI() {
            GUI.Box(ButtonRect(), "Quit");
        }

        private void OnConfirm(ConfirmEvent evt) {
            // Input System reports Y from the bottom, IMGUI from the top.
            var guiPosition = new Vector2(evt.ScreenPosition.x, Screen.height - evt.ScreenPosition.y);
            if (ButtonRect().Contains(guiPosition)) _eventBus.Publish(new QuitRequestedEvent());
        }

        private Rect ButtonRect() =>
            new(Screen.width - buttonWidth - margin, margin, buttonWidth, buttonHeight);
    }
}
