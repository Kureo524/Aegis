using System.Collections.Generic;
using Features.Augments;
using Features.Economy;
using Features.Events;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI {
    /// <summary>The player's augment slots, above the items (design doc §9.4). Fed by AugmentsChangedEvent. Shares the GameHud UIDocument.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class AugmentsView : MonoBehaviour {
        private readonly DisposableBag _subscriptions = new();
        private VisualElement _slots;
        private IReadOnlyList<AugmentDefinition> _augments = new List<AugmentDefinition>();
        private int _slotCount;

        [Inject]
        public void Construct(IEventBus eventBus) {
            _subscriptions.Add(eventBus.Subscribe<AugmentsChangedEvent>(e => {
                _augments = e.Augments;
                _slotCount = e.Slots;
                Refresh();
            }));
        }

        private void Start() {
            _slots = new VisualElement { pickingMode = PickingMode.Ignore };
            _slots.style.position = Position.Absolute;
            _slots.style.right = 16;
            _slots.style.bottom = 78;
            _slots.style.flexDirection = FlexDirection.Row;
            GetComponent<UIDocument>().rootVisualElement.Add(_slots);
            Refresh();
        }

        private void OnDestroy() => _subscriptions.Dispose();

        private void Refresh() {
            if (_slots == null) return;
            _slots.Clear();
            for (int i = 0; i < _slotCount; i++) {
                AugmentDefinition a = i < _augments.Count ? _augments[i] : null;
                var slot = new VisualElement { pickingMode = PickingMode.Ignore };
                slot.style.width = slot.style.height = 30;
                slot.style.marginLeft = 4;
                slot.style.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 0.9f);
                UiStyles.Border(slot, 1, a != null ? StatLabels.Color(a.Rarity) : new Color(1f, 1f, 1f, 0.15f), 15);
                if (a != null && a.Icon != null) {
                    slot.style.backgroundImage = new StyleBackground(a.Icon);
                    slot.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                }
                _slots.Add(slot);
            }
        }
    }
}
