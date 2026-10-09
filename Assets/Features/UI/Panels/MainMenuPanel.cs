using System;
using Features.Events;
using Systems.EventBusses;
using Systems.UIManagement;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI.Panels {
    public class MainMenuPanel : OpenClosePanel {

        [Inject] private readonly IEventBus _eventBus;
        
        private const float Duration = 0.5f;

        private Button _play, _settings, _quit;
        private VisualElement _visualElement;

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
        
        private void OnPlay() {
            _play.SetEnabled(false);   // évite le double clic
            _eventBus.Publish(new PlayRequestedEvent());
        }

        public override async Awaitable OpenPanel() {
            _visualElement.style.display = DisplayStyle.Flex;
            await Awaitable.NextFrameAsync();              // laisse la transition se déclencher
            _visualElement.RemoveFromClassList("menu--revealed");
            await WaitUnscaled(Duration);
            _play.SetEnabled(true);
        }

        public override async Awaitable ClosePanel() {
            _visualElement.AddToClassList("menu--revealed");
            await WaitUnscaled(Duration);
            _visualElement.style.display = DisplayStyle.None;       // plus de clics, plus de rendu
        }

        private static async Awaitable WaitUnscaled(float seconds) {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end) await Awaitable.NextFrameAsync();
        }

        private void OnSettings() { Debug.Log("Settings"); }
        private void OnQuit()     { Debug.Log("Quit"); _eventBus.Publish(new QuitRequestedEvent()); }
    }
}