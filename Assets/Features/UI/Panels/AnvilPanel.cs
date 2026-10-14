using Features.Economy;
using Features.Events;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI.Panels {
    /// <summary>
    /// Anvil screen (design doc §9.3), from AnvilOfferEvent snapshots: the 3 choices of the same
    /// rarity (the whole card picks it) and, for item anvils, "Reroll" under each card with the rerolls left on each choice.
    /// Drawn above the shop (own UIDocument, higher sorting order).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class AnvilPanel : MonoBehaviour {
        private IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();
        private VisualElement _overlay, _choices;
        private Label _title, _subtitle;
        private Button _skip;
        private AnvilOfferEvent _offer;

        public bool IsOpen => _offer.Open;

        [Inject]
        public void Construct(IEventBus eventBus) {
            _eventBus = eventBus;
            _subscriptions.Add(eventBus.Subscribe<AnvilOfferEvent>(e => {
                _offer = e;
                Refresh();
            }));
        }

        private void Start() {
            _overlay = MenuStyles.Overlay(out VisualElement box);
            box.style.width = 3 * (OfferCard.Width + 16) + 60;
            _title = MenuStyles.Title("");
            _title.style.marginBottom = 2;
            box.Add(_title);
            _subtitle = MenuStyles.Hint("");
            _subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            _subtitle.style.marginBottom = 8;
            box.Add(_subtitle);
            _choices = new VisualElement();
            _choices.style.flexDirection = FlexDirection.Row;
            _choices.style.justifyContent = Justify.Center;
            box.Add(_choices);
            _skip = MenuStyles.Button("Passer (inventaire plein)", () => _eventBus.Publish(new AnvilSkipRequestedEvent()));
            _skip.style.marginTop = 10;
            box.Add(_skip);
            GetComponent<UIDocument>().rootVisualElement.Add(_overlay);
            Refresh();
        }

        private void OnDestroy() => _subscriptions.Dispose();

        private void Refresh() {
            if (_overlay == null) return;
            _overlay.style.display = _offer.Open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_offer.Open) return;

            bool items = _offer.Anvil.Kind == AnvilKind.Item;
            _title.text = _offer.Anvil.DisplayName;
            _title.style.color = items ? StatLabels.Color(_offer.Anvil.ItemTier) : StatLabels.Color(_offer.Rarity);
            _subtitle.text = items ? "Clique sur un item pour le prendre." : $"Rareté tirée : {StatLabels.Name(_offer.Rarity)}. Clique sur un bonus permanent pour le prendre.";
            _skip.style.display = _offer.CanSkip ? DisplayStyle.Flex : DisplayStyle.None;

            _choices.Clear();
            for (int i = 0; i < _offer.Choices.Count; i++) {
                int index = i;
                (string name, Color color, Sprite icon, string body) = OfferCard.Describe(_offer.Choices[i]);
                VisualElement card = OfferCard.Build(icon, name, color, body,
                                                     () => _eventBus.Publish(new AnvilChooseRequestedEvent(index)), true);
                Button reroll = null;
                if (items) {
                    int left = _offer.Rerolls[i];
                    reroll = OfferCard.SmallButton($"Relancer ({left})", () => _eventBus.Publish(new AnvilRerollRequestedEvent(index)), left > 0);
                }
                _choices.Add(OfferCard.Column(card, reroll));
            }
        }
    }
}
