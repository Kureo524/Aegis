using Features.Augments;
using Features.Economy;
using Features.Events;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI.Panels {
    /// <summary>
    /// Augment draft screen (design doc §9.4), from AugmentDraftEvent snapshots: 3 augments of the
    /// same rarity, the whole card picks it, "Reroll" right below each card (one per card and per
    /// draft, golden reroll flagged); when every
    /// slot is full, the three benefits (anvil, replacement, slot or gold). Own UIDocument.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class AugmentPanel : MonoBehaviour {
        private IEventBus _eventBus;
        private readonly DisposableBag _subscriptions = new();
        private VisualElement _overlay, _cards, _options;
        private Label _title, _subtitle;
        private AugmentDraftEvent _draft;

        public bool IsOpen => _draft.Phase != AugmentDraftPhase.Closed;

        [Inject]
        public void Construct(IEventBus eventBus) {
            _eventBus = eventBus;
            _subscriptions.Add(eventBus.Subscribe<AugmentDraftEvent>(e => {
                _draft = e;
                Refresh();
            }));
        }

        private void Start() {
            _overlay = MenuStyles.Overlay(out VisualElement box);
            box.style.width = StyleKeyword.Null;
            box.AddToClassList("ae-draft");   // Arena draft: big cards straight on the dimmed scene
            _title = MenuStyles.Title("");
            _title.style.marginBottom = 2;
            box.Add(_title);
            _subtitle = MenuStyles.Hint("");
            _subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            _subtitle.style.marginBottom = 8;
            box.Add(_subtitle);
            _cards = new VisualElement();
            _cards.style.flexDirection = FlexDirection.Row;
            _cards.style.justifyContent = Justify.Center;
            box.Add(_cards);
            _options = new VisualElement();
            box.Add(_options);
            GetComponent<UIDocument>().rootVisualElement.Add(_overlay);
            Refresh();
        }

        private void OnDestroy() => _subscriptions.Dispose();

        private void Refresh() {
            if (_overlay == null) return;
            _overlay.style.display = IsOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (!IsOpen) return;
            _cards.Clear();
            _options.Clear();
            _title.style.color = StatLabels.Color(_draft.Rarity);

            if (_draft.Phase == AugmentDraftPhase.Choose) {
                _title.text = $"Augment {StatLabels.Name(_draft.Rarity)}";
                _subtitle.text = "Clique sur une carte pour la choisir · chacune peut être relancée une fois (même rareté)"
                                 + (_draft.GoldenReroll ? "   ·   ✦ Relance dorée disponible : une carte, rareté supérieure" : "");
                for (int i = 0; i < _draft.Choices.Count; i++) {
                    int index = i;
                    AugmentDefinition a = _draft.Choices[i];
                    VisualElement card = OfferCard.Build(a.Icon, a.DisplayName, StatLabels.Color(a.Rarity), a.Description,
                                                         () => _eventBus.Publish(new AugmentPickRequestedEvent(index)), true);
                    card.AddToClassList("ae-card--augment");
                    card.AddToClassList("ae-card--" + a.Rarity.ToString().ToLowerInvariant());
                    int left = i < _draft.CardRerolls.Count ? _draft.CardRerolls[i] : 0;
                    Button reroll = OfferCard.SmallButton(left > 1 ? $"Relancer ({left})" : "Relancer",
                                                          () => _eventBus.Publish(new AugmentRerollRequestedEvent(index)), left > 0);
                    Button golden = null;
                    if (_draft.GoldenReroll && a.Rarity != RarityTier.Prismatic) {
                        golden = OfferCard.SmallButton("✦ Relance dorée",
                                                       () => _eventBus.Publish(new AugmentRerollRequestedEvent(index, golden: true)));
                        golden.AddToClassList("ae-button--golden");
                    }
                    _cards.Add(OfferCard.Column(card, reroll, golden));
                }
                return;
            }

            // Every slot is full: one benefit instead
            AugmentDefinition pending = _draft.Pending;
            _title.text = "Emplacements pleins";
            _subtitle.text = $"Tu as choisi « {pending.DisplayName} » ({StatLabels.Name(pending.Rarity)}). Choisis un bénéfice :";
            _cards.Add(OfferCard.Build(pending.Icon, pending.DisplayName, StatLabels.Color(pending.Rarity), pending.Description,
                                       null, true));
            _options.Add(MenuStyles.Button($"Enclume à stats ({StatLabels.Name(pending.Rarity)})",
                                           () => _eventBus.Publish(new AugmentFullSlotsRequestedEvent(FullSlotsOption.Anvil))));
            for (int i = 0; i < _draft.Replaceable.Count; i++) {
                int index = i;
                AugmentDefinition owned = _draft.Replaceable[i];
                _options.Add(MenuStyles.Button($"Remplacer « {owned.DisplayName} » ({StatLabels.Name(owned.Rarity)})",
                                               () => _eventBus.Publish(new AugmentFullSlotsRequestedEvent(FullSlotsOption.Replace, index))));
            }
            if (_draft.Replaceable.Count == 0) _options.Add(MenuStyles.Hint("Aucun augment remplaçable avec cette rareté."));
            _options.Add(MenuStyles.Button(_draft.CanAddSlot ? "+1 emplacement d'augment" : $"{_draft.Gold} or",
                                           () => _eventBus.Publish(new AugmentFullSlotsRequestedEvent(FullSlotsOption.SlotOrGold))));
        }
    }
}
