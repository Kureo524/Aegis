using Features.Events;
using Features.StateMachines;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI.Panels {
    /// <summary>
    /// Victory / defeat screen (design doc §9.1), shown in GameState.Win / GameOver: replay the
    /// stage from wave 1 (PlayRequestedEvent → Loading) or back to the main menu. Own UIDocument.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class RunEndPanel : MonoBehaviour {
        private IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();
        private VisualElement _overlay;
        private Label _title, _summary;
        private bool _won;
        private int _waves, _level, _gold;

        public bool IsOpen { get; private set; }

        [Inject]
        public void Construct(IEventBus eventBus) {
            _eventBus = eventBus;
            _subscriptions.Add(eventBus.Subscribe<RunEndedEvent>(e => {
                _won = e.Won;
                _waves = e.WavesCleared;
            }));
            _subscriptions.Add(eventBus.Subscribe<LevelUpEvent>(e => _level = e.Level));
            _subscriptions.Add(eventBus.Subscribe<XpChangedEvent>(e => _level = e.Level));
            _subscriptions.Add(eventBus.Subscribe<GoldChangedEvent>(e => _gold = e.Gold));
            _subscriptions.Add(eventBus.Subscribe<GameStateChangedEvent>(e => Show(e.To is GameState.Win or GameState.GameOver)));
        }

        private void Start() {
            _overlay = MenuStyles.Overlay(out VisualElement box);
            _title = MenuStyles.Title("");
            _title.style.fontSize = 34;
            box.Add(_title);
            _summary = MenuStyles.Hint("");
            _summary.style.fontSize = 16;
            _summary.style.unityTextAlign = TextAnchor.MiddleCenter;
            _summary.style.marginBottom = 14;
            box.Add(_summary);
            box.Add(MenuStyles.Button("Rejouer", () => _eventBus.Publish(new PlayRequestedEvent())));
            box.Add(MenuStyles.Button("Menu principal", () => _eventBus.Publish(new MainMenuRequestedEvent())));
            box.Add(MenuStyles.Button("Quitter le jeu", () => _eventBus.Publish(new QuitRequestedEvent())));
            GetComponent<UIDocument>().rootVisualElement.Add(_overlay);
            Show(IsOpen);
        }

        private void OnDestroy() => _subscriptions.Dispose();

        private void Show(bool open) {
            IsOpen = open;
            if (_overlay == null) return;
            _overlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open) return;
            _title.text = _won ? "Victoire !" : "Défaite";
            _title.style.color = _won ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.4f, 0.35f);
            _summary.text = $"Vagues terminées : {_waves}\nNiveau {_level}  ·  {_gold} or";
        }
    }
}
