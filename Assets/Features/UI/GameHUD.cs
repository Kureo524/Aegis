using System;
using System.Threading.Tasks;
using AYellowpaper.SerializedCollections;
using Features.Events;
using Features.StateMachines;
using Systems.EventBusses;
using Systems.UIManagement;
using UnityEngine;
using VContainer;

namespace Features.UI {
    /// <summary>Opens the panel mapped to the new game state and closes the previous one.
    /// The state → panel mapping is data, edited in the Inspector.</summary>
    public class GameHUD : MonoBehaviour {
        [SerializedDictionary("StateType", "Panel")]
        [SerializeField] private SerializedDictionary<GameState, OpenClosePanel> panels = new();

        private IPanelManager _panelManager;
        private IDisposable _subscription;

        [Inject]
        public void Construct(IEventBus eventBus, IPanelManager panelManager) {
            _panelManager = panelManager;
            _subscription = eventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
        }

        private void OnDestroy() => _subscription?.Dispose();

        private void OnStateChanged(GameStateChangedEvent evt) => _ = SwitchPanels(evt);

        private async Task SwitchPanels(GameStateChangedEvent evt) {
            try {
                if (evt.From is { } from && panels.TryGetValue(from, out OpenClosePanel previous)) {
                    if (!await _panelManager.TryOpenOrClosePanelAsync(previous, toOpen: false))
                        Debug.LogWarning($"[GameHUD] Closing {previous.name} failed", this);
                }

                if (panels.TryGetValue(evt.To, out OpenClosePanel next)) {
                    if (!await _panelManager.TryOpenOrClosePanelAsync(next, toOpen: true))
                        Debug.LogWarning($"[GameHUD] Opening {next.name} failed", this);
                }
            }
            catch (Exception e) {
                Debug.LogException(e);
            }
        }
    }
}
