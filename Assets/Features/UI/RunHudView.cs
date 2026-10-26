using Features.Events;
using Features.Run;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI {
    /// <summary>
    /// Top-center run HUD (design doc §9.8), fed by RunStatusEvent only: wave number (bonus waves
    /// flagged), timer or countdown, kill quota, stage lives, and the score (ScoreChangedEvent; hidden
    /// when the game scope has no score system). Overtime shows when the next life is lost.
    /// During the break before a wave: "Start the wave" (NextWaveRequestedEvent) and the shop button.
    /// Shares the GameHud UIDocument.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class RunHudView : MonoBehaviour {
        [SerializeField] private Color quotaDoneColor = new(0.45f, 0.95f, 0.45f);
        [SerializeField] private Color dangerColor = new(1f, 0.35f, 0.3f);

        private readonly DisposableBag _subscriptions = new();
        private Label _wave, _timer, _quota, _lives, _score, _banner;
        private VisualElement _breakRow;
        private IEventBus _eventBus;
        private RunStatusEvent? _status;
        private float _bannerUntil;
        private int? _points;

        [Inject]
        public void Construct(IEventBus eventBus) {
            _eventBus = eventBus;
            _subscriptions.Add(eventBus.Subscribe<RunStatusEvent>(e => {
                _status = e;
                Refresh();
            }));
            _subscriptions.Add(eventBus.Subscribe<ScoreChangedEvent>(e => {
                _points = e.Score;
                Refresh();
            }));
            _subscriptions.Add(eventBus.Subscribe<LifeLostEvent>(e => Banner(e.LivesLeft > 0 ? $"Vie perdue ! ({e.LivesLeft} restante{(e.LivesLeft > 1 ? "s" : "")})" : "Plus de vie…")));
            _subscriptions.Add(eventBus.Subscribe<RunStateChangedEvent>(e => {
                if (e.To == RunState.Combat && _status.HasValue) Banner($"Vague {_status.Value.Wave}");
                if (e.To == RunState.Overtime) Banner("Temps écoulé : atteins le quota !");
            }));
        }

        private void Start() {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.style.position = Position.Absolute;
            column.style.top = 12;
            column.style.left = column.style.right = 0;
            column.style.alignItems = Align.Center;

            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.AddToClassList("ae-panel");
            row.AddToClassList("ae-runhud");

            _wave = Cell(row, 18, new Color(0.72f, 0.88f, 1f));
            _timer = Cell(row, 22, Color.white);
            _timer.style.minWidth = 70;
            _timer.style.unityTextAlign = TextAnchor.MiddleCenter;
            _quota = Cell(row, 16, Color.white);
            _lives = Cell(row, 18, new Color(1f, 0.4f, 0.45f));
            _score = Cell(row, 16, new Color(1f, 0.85f, 0.45f));
            _score.style.display = DisplayStyle.None;

            _banner = UiStyles.Text("", 26, Color.white);
            _banner.AddToClassList("ae-banner");
            _banner.style.fontSize = StyleKeyword.Null;   // the theme's
            _banner.style.color = StyleKeyword.Null;

            _breakRow = new VisualElement();
            _breakRow.AddToClassList("ae-runhud__break");
            Button start = Panels.MenuStyles.Button("Lancer la vague ▶", () => _eventBus.Publish(new NextWaveRequestedEvent()));
            start.AddToClassList("ae-button--primary");
            start.AddToClassList("ae-runhud__start");
            Button shop = Panels.MenuStyles.Button("Boutique (P)", () => _eventBus.Publish(new ShopToggleRequestedEvent()));
            shop.AddToClassList("ae-runhud__shop");
            _breakRow.Add(start);
            _breakRow.Add(shop);

            column.Add(row);
            column.Add(_breakRow);
            column.Add(_banner);
            root.Add(column);
            Refresh();
        }

        private static Label Cell(VisualElement row, float size, Color color) {
            Label label = UiStyles.Text("", size, color);
            label.style.marginLeft = label.style.marginRight = 10;
            row.Add(label);
            return label;
        }

        private void Update() {
            if (_banner != null && _banner.ClassListContains("ae-banner--show") && Time.unscaledTime >= _bannerUntil)
                _banner.RemoveFromClassList("ae-banner--show");   // fades out (theme transition)
        }

        private void OnDestroy() => _subscriptions.Dispose();

        private void Banner(string text) {
            _bannerUntil = Time.unscaledTime + 2.5f;
            if (_banner == null) return;
            _banner.text = text;
            _banner.AddToClassList("ae-banner--show");
        }

        private void Refresh() {
            if (_score != null && _points.HasValue) {
                _score.style.display = DisplayStyle.Flex;
                _score.text = $"Score {_points.Value}";
            }
            if (_wave == null || !_status.HasValue) return;
            RunStatusEvent s = _status.Value;
            _wave.text = s.IsBonusWave
                ? $"Bonus {s.Wave - s.RegularWaves}/{s.TotalWaves - s.RegularWaves}"
                : $"Vague {s.Wave}/{s.RegularWaves}";

            int seconds = Mathf.CeilToInt(s.TimeLeft);
            _timer.style.display = s.State == RunState.Reward ? DisplayStyle.None : DisplayStyle.Flex;
            switch (s.State) {
                case RunState.PreWave:
                    _timer.text = $"Pause {seconds} s";
                    _timer.style.color = Color.white;
                    break;
                case RunState.Overtime:
                    _timer.text = $"-1 vie {seconds}";
                    _timer.style.color = dangerColor;
                    break;
                default:
                    _timer.text = $"{seconds / 60}:{seconds % 60:00}";
                    _timer.style.color = Color.white;
                    break;
            }

            bool fighting = s.State is RunState.Combat or RunState.Overtime;
            _quota.style.display = fighting ? DisplayStyle.Flex : DisplayStyle.None;
            _quota.text = s.QuotaReached ? $"Quota atteint ({s.Kills})" : $"Kills {s.Kills}/{s.Quota}";
            _quota.style.color = s.QuotaReached ? quotaDoneColor : Color.white;
            _lives.text = new string('♥', s.Lives);
            _breakRow.style.display = s.State == RunState.PreWave ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
