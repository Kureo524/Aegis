using System;
using Features.Economy;
using UnityEngine;
using UnityEngine.UIElements;

namespace Features.UI.Panels {
    /// <summary>A card of the shop / anvil screens: icon, name, stat lines, action button and a short note.</summary>
    public static class OfferCard {
        public const float Width = 190f;

        public static VisualElement Build(Sprite icon, string title, Color titleColor, string body,
                                          string button, Action onClick, bool enabled, string note = null) {
            var card = new VisualElement();
            card.AddToClassList("ae-card");
            card.style.borderTopColor = card.style.borderLeftColor = titleColor;   // rarity / tier color

            var image = new VisualElement();
            image.AddToClassList("ae-card__icon");
            if (icon != null) image.style.backgroundImage = new StyleBackground(icon);
            else image.style.display = DisplayStyle.None;
            card.Add(image);

            var name = new Label(title);
            name.AddToClassList("ae-card__name");
            name.style.color = titleColor;
            card.Add(name);

            var lines = new Label(body);
            lines.AddToClassList("ae-card__body");
            card.Add(lines);

            Button action = MenuStyles.Button(button, onClick);
            action.AddToClassList("ae-button--primary");
            action.style.height = 32;
            action.style.fontSize = 13;
            action.style.alignSelf = Align.Stretch;
            action.SetEnabled(enabled);
            card.Add(action);

            if (!string.IsNullOrEmpty(note)) {
                var n = new Label(note);
                n.AddToClassList("ae-card__note");
                card.Add(n);
            }
            return card;
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
