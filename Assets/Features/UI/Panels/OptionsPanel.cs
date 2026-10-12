using Features.Events;
using Features.Options;
using Systems.EventBusses;
using Systems.Inputs.Events;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI.Panels {
    /// <summary>
    /// Options screen of the main menu (OptionsRequestedEvent, "Settings" button), drawn above it
    /// in its own UIDocument. "Back" or Escape closes it. In game, the pause menu hosts the same
    /// <see cref="OptionsView"/>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class OptionsPanel : MonoBehaviour {
        private UserOptions _options;
        private readonly DisposableBag _subscriptions = new();
        private VisualElement _overlay;
        private OptionsView _view;
        private bool _open;

        public bool IsOpen => _open;

        [Inject]
        public void Construct(IEventBus eventBus, UserOptions options) {
            _options = options;
            _subscriptions.Add(eventBus.Subscribe<OptionsRequestedEvent>(_ => Show(true)));
            _subscriptions.Add(eventBus.Subscribe<UiCancelEvent>(_ => Show(false)));
        }

        private void Start() {
            _overlay = MenuStyles.Overlay(out VisualElement box);
            _view = new OptionsView(_options);
            _view.Back += () => Show(false);
            box.Add(_view);
            GetComponent<UIDocument>().rootVisualElement.Add(_overlay);
            Show(_open);
        }

        private void OnDestroy() => _subscriptions.Dispose();

        public void Show(bool open) {
            _open = open;
            if (_overlay == null) return;
            if (open) _view.Refresh();
            _overlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
