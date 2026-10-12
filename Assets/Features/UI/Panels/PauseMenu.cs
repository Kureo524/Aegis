using Features.Events;
using Features.Options;
using Features.StateMachines;
using Systems.EventBusses;
using Systems.Inputs.Events;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI.Panels {
    /// <summary>
    /// In-game pause menu (UI Toolkit, built in code): shown while the game is in
    /// <see cref="GameState.Pause"/>. Resume / Options / Main menu / Quit only publish requests,
    /// GameFlowController changes the state. Escape: back from the options, otherwise resume.
    /// Game scene, own UIDocument drawn above the HUD.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PauseMenu : MonoBehaviour {
        private IEventBus _eventBus;
        private UserOptions _options;
        private readonly DisposableBag _subscriptions = new();
        private VisualElement _overlay, _main;
        private OptionsView _optionsView;
        private bool _paused;

        public bool IsOpen => _paused;
        public bool OptionsOpen => _optionsView != null && _optionsView.style.display == DisplayStyle.Flex;

        [Inject]
        public void Construct(IEventBus eventBus, UserOptions options) {
            _eventBus = eventBus;
            _options = options;
            _subscriptions.Add(eventBus.Subscribe<GameStateChangedEvent>(e => SetPaused(e.To == GameState.Pause)));
            _subscriptions.Add(eventBus.Subscribe<UiCancelEvent>(_ => OnBack()));
        }

        private void Start() {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _overlay = MenuStyles.Overlay(out VisualElement box);

            _main = new VisualElement();
            _main.Add(MenuStyles.Title("Pause"));
            _main.Add(MenuStyles.Button("Reprendre", () => _eventBus.Publish(new ResumeRequestedEvent())));
            _main.Add(MenuStyles.Button("Options", () => ShowOptions(true)));
            _main.Add(MenuStyles.Button("Menu principal", () => _eventBus.Publish(new MainMenuRequestedEvent())));
            _main.Add(MenuStyles.Button("Quitter le jeu", () => _eventBus.Publish(new QuitRequestedEvent())));
            box.Add(_main);

            _optionsView = new OptionsView(_options);
            _optionsView.Back += () => ShowOptions(false);
            box.Add(_optionsView);

            root.Add(_overlay);
            ShowOptions(false);
            _overlay.style.display = _paused ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnDestroy() => _subscriptions.Dispose();

        private void SetPaused(bool paused) {
            _paused = paused;
            if (_overlay == null) return;
            ShowOptions(false);
            _overlay.style.display = paused ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void ShowOptions(bool show) {
            if (_optionsView == null) return;
            if (show) _optionsView.Refresh();
            _optionsView.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            _main.style.display = show ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnBack() {
            if (!_paused) return;
            if (OptionsOpen) ShowOptions(false);
            else _eventBus.Publish(new ResumeRequestedEvent());
        }
    }
}
