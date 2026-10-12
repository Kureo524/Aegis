using System;
using Features.Events;
using Features.StateMachines;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Features.Flow {
    /// <summary>
    /// Owns the global flow: turns requests (bus events) into state transitions, publishes
    /// <see cref="GameStateChangedEvent"/>, and loads scenes. Root scope entry point.
    ///
    /// Scene loads are Single-mode: the RootLifetimeScope survives them (DontDestroyOnLoad), so
    /// every scene scope — and all of its run state — is rebuilt from scratch on each load.
    /// The loaded scene's <see cref="SceneEntryPoint"/> then requests its own state (e.g. Game).
    /// </summary>
    public sealed class GameFlowController : IInitializable, IDisposable {
        private readonly GameFlowStateMachine _machine;
        private readonly GameFlowSettings _settings;
        private readonly IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();

        public GameFlowController(GameFlowStateMachine machine, GameFlowSettings settings, IEventBus eventBus) {
            _machine = machine;
            _settings = settings;
            _eventBus = eventBus;
        }

        // IInitializable runs as soon as the root container is built, i.e. before any scene
        // entry point can request a state, so no transition is ever missed.
        public void Initialize() {
            _machine.Changed += OnStateChanged;
            _subscriptions.Add(_eventBus.Subscribe<PlayRequestedEvent>(_ => _machine.TryChange(GameState.Loading)));
            _subscriptions.Add(_eventBus.Subscribe<PauseRequestedEvent>(_ => _machine.TryChange(GameState.Pause)));
            _subscriptions.Add(_eventBus.Subscribe<ResumeRequestedEvent>(_ => _machine.TryChange(GameState.Game)));
            _subscriptions.Add(_eventBus.Subscribe<MainMenuRequestedEvent>(_ => _machine.TryChange(GameState.Menu)));
            _subscriptions.Add(_eventBus.Subscribe<QuitRequestedEvent>(_ => Quit()));
            _subscriptions.Add(_eventBus.Subscribe<RunEndedEvent>(e => _machine.TryChange(e.Won ? GameState.Win : GameState.GameOver)));
        }

        public void Dispose() {
            _machine.Changed -= OnStateChanged;
            _subscriptions.Dispose();
        }

        private void OnStateChanged(GameState? from, GameState to) {
            _eventBus.Publish(new GameStateChangedEvent(from, to));

            // First state of the session: the scene is already the right one.
            if (from == null) return;

            switch (to) {
                case GameState.Loading: LoadScene(_settings.GameScene); break;
                case GameState.Menu:    LoadScene(_settings.MenuScene); break;
            }
        }

        private async void LoadScene(string sceneName) {
            try {
                await SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }
            catch (Exception e) {
                Debug.LogException(e);
            }
        }

        private static void Quit() {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
