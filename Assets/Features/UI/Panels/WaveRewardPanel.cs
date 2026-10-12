using System.Collections.Generic;
using Features.Events;
using Features.Run;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI.Panels {
    /// <summary>
    /// End-of-wave screen (design doc §9.1, §9.3), shown while the run is in Reward (game paused):
    /// which reward the WaveRewardSchedule planned, then "Continue" (RewardClosedEvent) starts the
    /// next countdown. Augments (phase 8), shops and anvils (phase 7b) will fill it; until then it
    /// only announces them. Own UIDocument above the HUD.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class WaveRewardPanel : MonoBehaviour {
        private IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();
        private VisualElement _overlay;
        private Label _title, _rewards;
        private WaveRewardEvent? _pending;

        public bool IsOpen { get; private set; }

        [Inject]
        public void Construct(IEventBus eventBus) {
            _eventBus = eventBus;
            _subscriptions.Add(eventBus.Subscribe<WaveRewardEvent>(e => {
                _pending = e;
                Show(true);
            }));
            _subscriptions.Add(eventBus.Subscribe<RunStateChangedEvent>(e => { if (e.To != RunState.Reward) Show(false); }));
            _subscriptions.Add(eventBus.Subscribe<RunEndedEvent>(_ => Show(false)));
        }

        private void Start() {
            _overlay = MenuStyles.Overlay(out VisualElement box);
            _title = MenuStyles.Title("");
            box.Add(_title);
            _rewards = MenuStyles.Hint("");
            _rewards.style.fontSize = 16;
            _rewards.style.marginBottom = 14;
            box.Add(_rewards);
            box.Add(MenuStyles.Button("Continuer", Close));
            GetComponent<UIDocument>().rootVisualElement.Add(_overlay);
            Show(IsOpen);
        }

        private void OnDestroy() => _subscriptions.Dispose();

        public void Close() {
            if (!IsOpen) return;
            Show(false);
            _eventBus.Publish(new RewardClosedEvent());
        }

        private void Show(bool open) {
            IsOpen = open;
            if (_overlay == null) return;
            _overlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open || !_pending.HasValue) return;
            WaveRewardEvent e = _pending.Value;
            _title.text = e.Bonus ? $"Vague bonus terminée !" : $"Vague {e.Wave} terminée !";
            _rewards.text = Describe(e.Rewards);
        }

        private static string Describe(IReadOnlyList<RewardKind> rewards) {
            if (rewards == null || rewards.Count == 0) return "";
            var lines = new List<string>();
            foreach (RewardKind kind in rewards) {
                lines.Add(kind switch {
                    RewardKind.Augment => "• Augment (arrive en phase 8)",
                    RewardKind.FreePrismaticAnvil => "• Enclume prismatique gratuite (arrive en phase 7b)",
                    RewardKind.LightShop => "• Boutique légère (arrive en phase 7b)",
                    RewardKind.FullShop => "• Boutique complète (arrive en phase 7b)",
                    _ => $"• {kind}",
                });
            }
            return string.Join("\n", lines);
        }
    }
}
