using Features.Events;
using Features.StateMachines;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI.Panels {
    /// <summary>
    /// Victory / defeat screen (design doc §9.1), shown in GameState.Win / GameOver: replay the
    /// stage from wave 1 (PlayRequestedEvent → Loading) or back to the main menu. Shows the final
    /// score and the best one (RunScoredEvent, BestScoreEvent) when the run has a score system. Own UIDocument.
    /// Victory or defeat comes from the game state itself: the state changes while RunEndedEvent is
    /// being handled, before this panel receives it, so the summary is refreshed when it does.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class RunEndPanel : MonoBehaviour {
        private IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();
        private VisualElement _overlay;
        private Label _title, _score, _summary, _reward;
        private string _rewardText = "", _scoreText = "";
        private int? _finalScore;
        private BestScoreEvent? _best;
        private bool _won;
        private int _waves, _level, _gold;

        public bool IsOpen { get; private set; }
        public string Title => _title != null ? _title.text : "";
        /// <summary>The meta currency line ("+N éclats" or how to earn some).</summary>
        public string Reward => _rewardText;
        /// <summary>The score lines ("Score : N" and the record), empty without a score system.</summary>
        public string ScoreText => _scoreText;

        [Inject]
        public void Construct(IEventBus eventBus) {
            _eventBus = eventBus;
            _subscriptions.Add(eventBus.Subscribe<RunEndedEvent>(e => {
                _waves = e.WavesCleared;
                if (IsOpen) Show(true);
            }));
            _subscriptions.Add(eventBus.Subscribe<MetaRewardEvent>(e => {
                _rewardText = e.Amount > 0 ? $"+{e.Amount} {e.Currency}" : $"Termine le stage pour gagner des {e.Currency}";
                if (IsOpen) Show(true);
            }));
            // MetaProgress handles RunScoredEvent first: the best score may come before the score
            _subscriptions.Add(eventBus.Subscribe<RunScoredEvent>(e => {
                _finalScore = e.Score;
                RefreshScore();
            }));
            _subscriptions.Add(eventBus.Subscribe<BestScoreEvent>(e => {
                _best = e;
                RefreshScore();
            }));
            _subscriptions.Add(eventBus.Subscribe<LevelUpEvent>(e => _level = e.Level));
            _subscriptions.Add(eventBus.Subscribe<XpChangedEvent>(e => _level = e.Level));
            _subscriptions.Add(eventBus.Subscribe<GoldChangedEvent>(e => _gold = e.Gold));
            _subscriptions.Add(eventBus.Subscribe<GameStateChangedEvent>(e => {
                _won = e.To == GameState.Win;
                Show(e.To is GameState.Win or GameState.GameOver);
            }));
        }

        private void Start() {
            _overlay = MenuStyles.Overlay(out VisualElement box);
            _title = MenuStyles.Title("");
            _title.style.fontSize = 34;
            box.Add(_title);
            _score = MenuStyles.Hint("");
            _score.style.fontSize = 22;
            _score.style.color = new Color(1f, 0.85f, 0.45f);
            _score.style.unityTextAlign = TextAnchor.MiddleCenter;
            _score.style.marginBottom = 8;
            box.Add(_score);
            _summary = MenuStyles.Hint("");
            _summary.style.fontSize = 16;
            _summary.style.unityTextAlign = TextAnchor.MiddleCenter;
            _summary.style.marginBottom = 14;
            box.Add(_summary);
            _reward = new Label();
            _reward.AddToClassList("ae-reward");
            box.Add(_reward);
            box.Add(MenuStyles.Button("Rejouer", () => _eventBus.Publish(new PlayRequestedEvent())));
            box.Add(MenuStyles.Button("Menu principal", () => _eventBus.Publish(new MainMenuRequestedEvent())));
            box.Add(MenuStyles.Button("Quitter le jeu", () => _eventBus.Publish(new QuitRequestedEvent())));
            GetComponent<UIDocument>().rootVisualElement.Add(_overlay);
            Show(IsOpen);
        }

        private void OnDestroy() => _subscriptions.Dispose();

        private void RefreshScore() {
            if (!_finalScore.HasValue) return;
            _scoreText = $"Score : {_finalScore.Value}";
            if (_best.HasValue) _scoreText += _best.Value.NewRecord ? "\nNouveau record !" : $"\nRecord : {_best.Value.Best}";
            if (IsOpen) Show(true);
        }

        private void Show(bool open) {
            IsOpen = open;
            if (_overlay == null) return;
            _overlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open) return;
            _title.text = _won ? "Victoire !" : "Défaite";
            _title.style.color = _won ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 0.4f, 0.5f);
            _score.text = _scoreText;
            _score.style.display = _scoreText.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _summary.text = $"Vagues terminées : {_waves}\nNiveau {_level}  ·  {_gold} or";
            _reward.text = _rewardText;
        }
    }
}
