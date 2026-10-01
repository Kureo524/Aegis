using AYellowpaper.SerializedCollections;
using Features.StateMachines;
using NUnit.Framework;
using Systems.StateMachine;
using Systems.UIManagement;
using UnityEngine;
using VContainer;

namespace Features.UI {
    public class GameHUD : MonoBehaviour
    {
        [SerializedDictionary("StateType", "Panel")] 
        [SerializeField] private SerializedDictionary<GameState, OpenClosePanel> panels = new();
        
        [Inject] public readonly GameFlowStateMachine StateMachine;

        private void Start() {
            Assert.IsNotNull(StateMachine);
            StateMachine.Changed += TryUpdateUI;
        }

        private void OnDestroy() {
            Assert.IsNotNull(StateMachine);
            StateMachine.Changed -= TryUpdateUI;
        }

        private async void TryUpdateUI(GameStateMachine<GameState>.State fromState, GameStateMachine<GameState>.State toState) {
            try {
                if (panels.ContainsKey(fromState.Id)) {
                    if (await PanelManager.TryOpenOrClosePanelAsync(panels[fromState.Id]))
                        Debug.Log($"Panel {panels[fromState.Id]} closure encountered an error");
                }

                if (!panels.ContainsKey(toState.Id)) return;
                
                if (await PanelManager.TryOpenOrClosePanelAsync(panels[toState.Id]))
                    Debug.Log($"Panel {panels[toState.Id]} opening encountered an error");
            }
            catch (System.Exception e) {
                Debug.LogException(e);
            }
        }
    }
}
