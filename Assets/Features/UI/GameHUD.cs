using System;
using AYellowpaper.SerializedCollections;
using Features.Events;
using Features.StateMachines;
using Systems.EventBusses;
using Systems.UIManagement;
using UnityEngine;
using UnityEngine.Assertions;
using VContainer;

namespace Features.UI {
    public class GameHUD : MonoBehaviour
    {
        [SerializedDictionary("StateType", "Panel")] 
        [SerializeField] private SerializedDictionary<GameState, OpenClosePanel> panels = new();
        
        [Inject] private readonly IEventBus _eventBus;
        private IDisposable _subscription;

        private void Start() {
            Assert.IsNotNull(_eventBus);
            _subscription = _eventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
        }

        private void OnDestroy() {
            _subscription?.Dispose();
        }

        private async void OnStateChanged(GameStateChangedEvent evt) {
            try {
                if (panels.ContainsKey(evt.From)) {
                    if (!await PanelManager.TryOpenOrClosePanelAsync(panels[evt.From]))
                        Debug.LogWarning($"Panel {panels[evt.From]} closure failed");
                }

                if (!panels.ContainsKey(evt.To)) return;
                
                if (!await PanelManager.TryOpenOrClosePanelAsync(panels[evt.To]))
                    Debug.LogWarning($"Panel {panels[evt.To]} opening failed");
            }
            catch (System.Exception e) {
                Debug.LogException(e);
            }
        }
    }
}
