using Features.Events;
using Features.Run;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI {
    /// <summary>
    /// Boss bar under the run HUD (design doc §9.6): name, title, HP, phase. Shown from
    /// BossSpawnedEvent to the boss's death or the end of the wave. Shares the GameHud UIDocument.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class BossHudView : MonoBehaviour {
        [SerializeField] private Color barColor = new(0.85f, 0.2f, 0.25f);

        private readonly DisposableBag _subscriptions = new();
        private VisualElement _root, _fill;
        private Label _name, _phase;
        private string _bossName = "";
        private bool _visible;
        private float _current, _max = 1f;
        private int _phaseNumber = 1;

        public bool IsVisible => _visible;

        [Inject]
        public void Construct(IEventBus eventBus) {
            _subscriptions.Add(eventBus.Subscribe<BossSpawnedEvent>(e => {
                _bossName = $"{e.Name} — {e.Title}";
                _visible = true;
                Refresh();
            }));
            _subscriptions.Add(eventBus.Subscribe<BossHealthEvent>(e => {
                _current = e.Current;
                _max = Mathf.Max(1f, e.Max);
                _phaseNumber = e.Phase;
                Refresh();
            }));
            _subscriptions.Add(eventBus.Subscribe<BossDefeatedEvent>(_ => {
                _visible = false;
                Refresh();
            }));
            _subscriptions.Add(eventBus.Subscribe<RunStateChangedEvent>(e => {
                if (e.To != RunState.Combat && e.To != RunState.Overtime) {
                    _visible = false;
                    Refresh();
                }
            }));
        }

        private void Start() {
            _root = new VisualElement { pickingMode = PickingMode.Ignore };
            _root.style.position = Position.Absolute;
            _root.style.top = 92;
            _root.style.left = _root.style.right = 0;
            _root.style.alignItems = Align.Center;

            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.style.flexDirection = FlexDirection.Row;
            header.style.width = 520;
            header.style.justifyContent = Justify.SpaceBetween;
            _name = UiStyles.Text("", 16, new Color(1f, 0.85f, 0.5f));
            _phase = UiStyles.Text("", 14, Color.white);
            header.Add(_name);
            header.Add(_phase);

            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.style.width = 520;
            bar.style.height = 16;
            bar.style.marginTop = 3;
            bar.AddToClassList("ae-bar");
            bar.AddToClassList("ae-bossbar");
            _fill = new VisualElement { pickingMode = PickingMode.Ignore };
            _fill.style.height = Length.Percent(100);
            _fill.style.backgroundColor = barColor;
            bar.Add(_fill);

            _root.Add(header);
            _root.Add(bar);
            GetComponent<UIDocument>().rootVisualElement.Add(_root);
            Refresh();
        }

        private void OnDestroy() => _subscriptions.Dispose();

        private void Refresh() {
            if (_root == null) return;
            _root.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
            _name.text = _bossName;
            _phase.text = $"Phase {_phaseNumber}";
            _fill.style.width = Length.Percent(100f * Mathf.Clamp01(_current / _max));
        }
    }
}
