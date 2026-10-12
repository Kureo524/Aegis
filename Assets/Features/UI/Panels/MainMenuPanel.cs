using System;
using System.Threading.Tasks;
using Features.Events;
using Features.StateMachines;
using Systems.EventBusses;
using Systems.UIManagement;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI.Panels {
    /// <summary>
    /// Main menu. Shown while the game is in <see cref="GameState.Menu"/>; its buttons only
    /// publish requests on the bus, the flow decides what happens.
    /// </summary>
    public class MainMenuPanel : OpenClosePanel {
        private const float Duration = 0.5f;

        private IEventBus _eventBus;
        private IPanelManager _panelManager;
        private IDisposable _stateSubscription;

        private Button _play, _settings, _quit;
        private VisualElement _visualElement;

        [Inject]
        public void Construct(IEventBus eventBus, IPanelManager panelManager) {
            _eventBus = eventBus;
            _panelManager = panelManager;
            // Subscribed at injection time, i.e. before the scene entry point enters Menu.
            _stateSubscription = _eventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
        }

        protected override void SetupDocument(VisualElement document) {
            _visualElement = document;

            _play     = _visualElement.Q<Button>("PlayButton");
            _settings = _visualElement.Q<Button>("SettingsButton");
            _quit     = _visualElement.Q<Button>("QuitButton");

            _play.clicked     += OnPlay;
            _settings.clicked += OnSettings;
            _quit.clicked     += OnQuit;
        }

        private void OnDisable() {
            if (_play == null) return;
            _play.clicked     -= OnPlay;
            _settings.clicked -= OnSettings;
            _quit.clicked     -= OnQuit;
        }

        private void OnDestroy() => _stateSubscription?.Dispose();

        private void OnStateChanged(GameStateChangedEvent evt) {
            if (evt.To == GameState.Menu) _ = Show(true);
            else if (evt.From == GameState.Menu && evt.To != GameState.Loading) _ = Show(false);
        }

        private async Task Show(bool open) {
            try {
                await _panelManager.TryOpenOrClosePanelAsync(this, toOpen: open);
            }
            catch (Exception e) {
                Debug.LogException(e);
            }
        }

        public override async Awaitable OpenPanel() {
            _visualElement.style.display = DisplayStyle.Flex;
            await Awaitable.NextFrameAsync();              // let the transition kick in
            _visualElement.RemoveFromClassList("menu--revealed");
            await WaitUnscaled(Duration);
            _play.SetEnabled(true);
        }

        public override async Awaitable ClosePanel() {
            _visualElement.AddToClassList("menu--revealed");
            await WaitUnscaled(Duration);
            _visualElement.style.display = DisplayStyle.None;   // no more clicks, no more rendering
        }

        private void OnPlay() {
            _play.SetEnabled(false);   // avoid double clicks
            _eventBus.Publish(new PlayRequestedEvent());
        }

        private void OnSettings() { }   // Settings screen not implemented yet

        private void OnQuit() => _eventBus.Publish(new QuitRequestedEvent());

        private static async Awaitable WaitUnscaled(float seconds) {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end) await Awaitable.NextFrameAsync();
        }
    }
}
