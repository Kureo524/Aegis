using System.Collections.Generic;
using Features.Characters;
using Features.Events;
using Features.Spells;
using Features.StateMachines;
using Systems.EventBusses;
using Systems.Inputs.Events;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI.Panels {
    /// <summary>
    /// Champion select screen (design doc §7.10), above the main menu: opened by "Play"
    /// (ChampionSelectRequestedEvent), one card per champion of the roster (portrait, name, epithet,
    /// how they play, passive and Q W E R with their tooltips). A click picks the champion
    /// (ChampionPickRequestedEvent), "Play" or a double click starts the run (PlayRequestedEvent).
    /// Reads the ChampionSelection, changes it only through the bus. Own UIDocument.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ChampionSelectPanel : MonoBehaviour {
        private IEventBus _eventBus;
        private ChampionSelection _selection;
        private readonly DisposableBag _subscriptions = new();
        private UiTooltip _tooltip;
        private VisualElement _overlay, _cards;
        private Button _play;
        private bool _open;
        private readonly Dictionary<CharacterDefinition, VisualElement> _cardOf = new();

        public bool IsOpen => _open;

        [Inject]
        public void Construct(IEventBus eventBus, ChampionSelection selection) {
            _eventBus = eventBus;
            _selection = selection;
            _subscriptions.Add(eventBus.Subscribe<ChampionSelectRequestedEvent>(_ => Show(true)));
            _subscriptions.Add(eventBus.Subscribe<UiCancelEvent>(_ => Show(false)));
            _subscriptions.Add(eventBus.Subscribe<ChampionSelectedEvent>(_ => RefreshSelection()));
            _subscriptions.Add(eventBus.Subscribe<GameStateChangedEvent>(e => {
                if (e.From == GameState.Menu) Show(false);
            }));
        }

        private void Start() {
            _tooltip = new UiTooltip();
            _overlay = MenuStyles.Overlay(out VisualElement box);
            box.style.width = StyleKeyword.Null;
            box.AddToClassList("ae-champselect");
            box.Add(MenuStyles.Title("Choisis ton champion"));
            _cards = new VisualElement();
            _cards.AddToClassList("ae-champselect__cards");
            box.Add(_cards);

            var footer = new VisualElement();
            footer.AddToClassList("ae-champselect__footer");
            Button back = MenuStyles.Button("Retour", () => Show(false));
            back.style.width = 180;
            footer.Add(back);
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            footer.Add(spacer);
            _play = MenuStyles.Button("Jouer ▶", Play);
            _play.AddToClassList("ae-button--primary");
            _play.AddToClassList("ae-champselect__play");
            footer.Add(_play);
            box.Add(footer);

            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            root.Add(_overlay);
            root.Add(_tooltip.Root);
            Show(_open);
        }

        private void OnDestroy() => _subscriptions.Dispose();

        public void Show(bool open) {
            _open = open;
            if (_overlay == null) return;
            _overlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open) {
                _play.SetEnabled(true);
                Build();
            }
            else _tooltip.Hide();
        }

        /// <summary>Picks a champion as a click on its card would (tests).</summary>
        public void Pick(CharacterDefinition champion) => _eventBus.Publish(new ChampionPickRequestedEvent(champion));

        private void Play() {
            _play.SetEnabled(false);   // no double start
            _eventBus.Publish(new PlayRequestedEvent());
        }

        private void Build() {
            _cards.Clear();
            _cardOf.Clear();
            foreach (CharacterDefinition champion in _selection.Champions) {
                if (champion == null) continue;
                VisualElement card = Card(champion);
                _cardOf[champion] = card;
                _cards.Add(card);
            }
            RefreshSelection();
        }

        private void RefreshSelection() {
            foreach ((CharacterDefinition champion, VisualElement card) in _cardOf)
                card.EnableInClassList("ae-champ--selected", champion == _selection.Current);
        }

        private VisualElement Card(CharacterDefinition champion) {
            var card = new VisualElement();
            card.AddToClassList("ae-champ");
            card.RegisterCallback<ClickEvent>(e => {
                Pick(champion);
                if (e.clickCount >= 2) Play();
            });

            var portrait = new VisualElement { pickingMode = PickingMode.Ignore };
            portrait.AddToClassList("ae-champ__portrait");
            if (champion.Portrait != null) portrait.style.backgroundImage = new StyleBackground(champion.Portrait);
            card.Add(portrait);

            var name = new Label(champion.DisplayName) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("ae-champ__name");
            card.Add(name);
            var epithet = new Label(champion.Epithet) { pickingMode = PickingMode.Ignore };
            epithet.AddToClassList("ae-champ__epithet");
            card.Add(epithet);
            var summary = new Label(champion.Summary) { pickingMode = PickingMode.Ignore };
            summary.AddToClassList("ae-champ__summary");
            card.Add(summary);

            var kit = new VisualElement();
            kit.AddToClassList("ae-champ__kit");
            if (champion.Passive != null) kit.Add(KitIcon(champion.Passive.Icon, "Passif", champion.Passive.DisplayName, champion.Passive.Description));
            string[] keys = { "A", "Z", "E", "R" };
            for (int i = 0; i < champion.Abilities.Count && i < keys.Length; i++) {
                SpellDefinition spell = champion.Abilities[i];
                if (spell != null) kit.Add(KitIcon(spell.Icon, keys[i], spell.DisplayName, spell.Description));
            }
            card.Add(kit);
            return card;
        }

        private VisualElement KitIcon(Sprite icon, string key, string title, string text) {
            var cell = new VisualElement();
            cell.AddToClassList("ae-slot");
            cell.AddToClassList("ae-champ__spell");
            if (icon != null) cell.style.backgroundImage = new StyleBackground(icon);
            var label = new Label(key) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("ae-champ__key");
            cell.Add(label);
            _tooltip.Attach(cell, () => (title, key == "Passif" ? "Passif" : $"Touche {key}", text));
            return cell;
        }
    }
}
