using System;
using Features.Economy;
using UnityEngine;
using UnityEngine.UIElements;

namespace Features.UI.Panels {
    /// <summary>
    /// A card of the anvil / augment screens: icon, name, stat lines and a short note. The whole
    /// card is the button that picks it; actions on the card (rerolls) go right below it, in the
    /// column made by <see cref="Column"/>.
    /// </summary>
    public static class OfferCard {
        public const float Width = 190f;

        public static VisualElement Build(Sprite icon, string title, Color titleColor, string body,
                                          Action onClick, bool enabled, string note = null) {
            var card = new VisualElement();
            card.AddToClassList("ae-card");
            card.EnableInClassList("ae-card--clickable", enabled && onClick != null);
            card.EnableInClassList("ae-card--disabled", !enabled);
            card.style.borderTopColor = card.style.borderLeftColor = titleColor;   // rarity / tier color
            if (enabled && onClick != null)
                card.RegisterCallback<ClickEvent>(e => {
                    if (e.button == 0) onClick();
                });

            var image = new VisualElement { pickingMode = PickingMode.Ignore };
            image.AddToClassList("ae-card__icon");
            if (icon != null) image.style.backgroundImage = new StyleBackground(icon);
            else image.style.display = DisplayStyle.None;
            card.Add(image);

            var name = new Label(title) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("ae-card__name");
            name.style.color = titleColor;
            card.Add(name);

            var lines = new Label(body) { pickingMode = PickingMode.Ignore };
            lines.AddToClassList("ae-card__body");
            card.Add(lines);

            if (!string.IsNullOrEmpty(note)) {
                var n = new Label(note) { pickingMode = PickingMode.Ignore };
                n.AddToClassList("ae-card__note");
                card.Add(n);
            }
            return card;
        }

        /// <summary>The card and, under it, its own buttons (rerolls).</summary>
        public static VisualElement Column(VisualElement card, params Button[] below) {
            var column = new VisualElement();
            column.AddToClassList("ae-card-column");
            column.Add(card);
            foreach (Button button in below) {
                if (button == null) continue;
                button.AddToClassList("ae-card-column__button");
                button.style.marginLeft = button.style.marginRight = 0;
                column.Add(button);
            }
            return column;
        }

        /// <summary>A small button for the column under a card.</summary>
        public static Button SmallButton(string text, Action onClick, bool enabled = true) {
            Button button = MenuStyles.Button(text, onClick);
            button.style.height = 30;
            button.style.fontSize = 13;
            button.SetEnabled(enabled);
            return button;
        }

        /// <summary>Name, color, icon and stat lines of any offer or anvil choice.</summary>
        public static (string name, Color color, Sprite icon, string body) Describe(ScriptableObject offer, RarityTier rarity = RarityTier.Silver) {
            switch (offer) {
                case ItemDefinition item:
                    string passive = item.Passive != null ? $"\n\n{item.Passive.DisplayName} : {item.Passive.Description}" : "";
                    return (item.DisplayName, StatLabels.Color(item.Tier), item.Icon, StatLabels.Format(item.Stats) + passive);
                case ElixirDefinition elixir:
                    return (elixir.DisplayName, new Color(0.45f, 0.9f, 0.7f), elixir.Icon,
                            StatLabels.Format(elixir.Stats) + "\njusqu'à la fin de la prochaine vague");
                case AnvilDefinition anvil:
                    string what = anvil.Kind == AnvilKind.Stat
                        ? "3 bonus de stats au choix\n(Argent, Or ou Prismatique)"
                        : $"3 items {(anvil.ItemTier == ItemTier.Legendary ? "légendaires" : "prismatiques")} au choix\n{anvil.RerollsPerChoice} relance par choix";
                    return (anvil.DisplayName, anvil.Kind == AnvilKind.Stat ? new Color(0.8f, 0.85f, 0.95f) : StatLabels.Color(anvil.ItemTier),
                            anvil.Icon, what);
                case StatChoiceDefinition choice:
                    return ($"Bonus {StatLabels.Name(choice.Rarity)}", StatLabels.Color(choice.Rarity), null, StatLabels.Format(choice.Stats));
                default:
                    return (offer != null ? offer.name : "?", Color.white, null, "");
            }
        }

        public static int Cost(ScriptableObject offer) => offer switch {
            ItemDefinition i => i.Cost,
            ElixirDefinition e => e.Cost,
            AnvilDefinition a => a.Cost,
            _ => 0,
        };
    }
}
