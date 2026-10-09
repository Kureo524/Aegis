using System;
using Features.Events;
using Features.StateMachines;
using Features.UI.Panels;
using Systems.EventBusses;
using Systems.Inputs;
using Systems.UIManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Features.Flow {
    public class GameFlowController : IStartable, IDisposable {
        private readonly GameFlowStateMachine _machine;
        private readonly MainMenuPanel _menu;
        private readonly InputReader _inputReader;
        private readonly IEventBus _eventBus;
        private IDisposable _playSubscription;
        private const string GameScene = "Game";

        [Inject]
        public GameFlowController(GameFlowStateMachine machine, MainMenuPanel menu, InputReader inputReader, IEventBus eventBus) {
            _machine = machine;
            _menu = menu;
            _inputReader = inputReader;
            _eventBus = eventBus;
        }

        public void Start() {
            _machine.Changed += OnStateChanged;
            _playSubscription = _eventBus.Subscribe<PlayRequestedEvent>(_ => OnPlayRequested());
            
            _inputReader.Initialize(_eventBus, EActionTypes.UI);

            _ = PanelManager.TryOpenOrClosePanelAsync(_menu, toOpen: true);
            _machine.TryChange(new GameFlowStateMachine.State(GameState.Menu));
        }

        public void Dispose() {
            _machine.Changed -= OnStateChanged;
            _playSubscription?.Dispose();
        }

        private void OnStateChanged(GameFlowStateMachine.State from, GameFlowStateMachine.State to) {
            _eventBus.Publish(new GameStateChangedEvent(from?.Id ?? default, to.Id));
            HandleSceneTransition(from?.Id ?? default, to.Id);
        }

        private void OnPlayRequested() =>
            _machine.TryChange(new GameFlowStateMachine.State(GameState.Game));

        private async void HandleSceneTransition(GameState from, GameState to) {
            try {
                if (from == GameState.Menu && to == GameState.Game) {
                    _inputReader.DisableInput(EActionTypes.UI);
                    await SceneManager.LoadSceneAsync(GameScene, LoadSceneMode.Additive);
                    await PanelManager.TryOpenOrClosePanelAsync(_menu, toOpen: false);
                    _inputReader.EnableInput(EActionTypes.Game);
                }
                else if (from == GameState.Game && to == GameState.Menu) {
                    _inputReader.DisableInput(EActionTypes.Game);
                    await PanelManager.TryOpenOrClosePanelAsync(_menu, toOpen: true);
                    await SceneManager.UnloadSceneAsync(GameScene);
                    _inputReader.EnableInput(EActionTypes.UI);
                }
            }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}