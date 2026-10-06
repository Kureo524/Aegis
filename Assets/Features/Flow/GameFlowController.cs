using System;
using Features.StateMachines;
using Features.UI.Panels;
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
        private const string GameScene = "Game";

        [Inject]
        public GameFlowController(GameFlowStateMachine machine, MainMenuPanel menu, InputReader inputReader) {
            _machine = machine;
            _menu = menu;
            _inputReader = inputReader;
        }

        public void Start() {
            _machine.Changed += OnStateChanged;
            _menu.PlayClicked += OnPlayClicked;
            
            _inputReader.Initialize();

            _ = PanelManager.TryOpenOrClosePanelAsync(_menu, toOpen: true);
            _machine.TryChange(new GameFlowStateMachine.State(GameState.Menu));
        }

        public void Dispose() {
            _machine.Changed -= OnStateChanged;
            _menu.PlayClicked -= OnPlayClicked;
        }

        private void OnPlayClicked() =>
            _machine.TryChange(new GameFlowStateMachine.State(GameState.Game));

        private async void OnStateChanged(GameFlowStateMachine.State from, GameFlowStateMachine.State to) {
            try {
                if (from?.Id == GameState.Menu && to.Id == GameState.Game) {
                    await SceneManager.LoadSceneAsync(GameScene, LoadSceneMode.Additive);
                    await PanelManager.TryOpenOrClosePanelAsync(_menu, toOpen: false);
                }
                else if (from?.Id == GameState.Game && to.Id == GameState.Menu) {
                    await PanelManager.TryOpenOrClosePanelAsync(_menu, toOpen: true);
                    await SceneManager.UnloadSceneAsync(GameScene);
                }
            }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}