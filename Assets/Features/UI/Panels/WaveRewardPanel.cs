using Features.Events;
using Features.Run;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI.Panels {
    /// <summary>
    /// End-of-wave screen for the reward steps that have no screen of their own yet (design doc
    /// §9.3): the augment draft arrives with phase 8, so for now it announces it and "Continue" ends
    /// the step (RewardStepDoneEvent). Shops and anvils have their own panels. Own UIDocument.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class WaveRewardPanel : MonoBehaviour {
        private IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();
        private VisualElement _overlay;
        private Label _title, _body;
        private int _wave;
        private bool _bonus;

        public bool IsOpen { get; private set; }

        [Inject]
        public void Construct(IEventBus eventBus) {
            _eventBus = eventBus;
            _subscriptions.Add(eventBus.Subscribe<WaveRewardEvent>(e => {
                _wave = e.Wave;
                _bonus = e.Bonus;
            }));
            _subscriptions.Add(eventBus.Subscribe<RewardStepEvent>(e => Show(e.Kind == RewardKind.Augment)));
            _subscriptions.Add(eventBus.Subscribe<RunStateChangedEvent>(e => { if (e.To != RunState.Reward) Show(false); }));
            _subscriptions.Add(eventBus.Subscribe<RunEndedEvent>(_ => Show(false)));
        }

        private void Start() {
            _overlay = MenuStyles.Overlay(out VisualElement box);
            _title = MenuStyles.Title("");
            box.Add(_title);
            _body = MenuStyles.Hint("Les augments arrivent en phase 8.");
            _body.style.fontSize = 16;
            _body.style.marginBottom = 14;
            box.Add(_body);
            box.Add(MenuStyles.Button("Continuer", Close));
            GetComponent<UIDocument>().rootVisualElement.Add(_overlay);
            Show(IsOpen);
        }

        private void OnDestroy() => _subscriptions.Dispose();

        public void Close() {
            if (!IsOpen) return;
            Show(false);
            _eventBus.Publish(new RewardStepDoneEvent());
        }

        private void Show(bool open) {
            IsOpen = open;
            if (_overlay == null) return;
            _overlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open) _title.text = _bonus ? "Vague bonus terminée !" : $"Vague {_wave} terminée !";
        }
    }
}
