using System;
using Features.Economy;
using UnityEngine;
using UnityEngine.UIElements;

namespace Features.UI.Panels {
    /// <summary>A card of the shop / anvil screens: icon, name, stat lines, action button and a short note.</summary>
    public static class OfferCard {
        public const float Width = 168f;

        public static VisualElement Build(Sprite icon, string title, Color titleColor, string body,
                                          string button, Action onClick, bool enabled, string note = null) {
            var card = new VisualElement();
            card.style.width = Width;
            card.style.marginLeft = card.style.marginRight = 5;
            card.style.marginTop = card.style.marginBottom = 5;
            card.style.paddingLeft = card.style.paddingRight = 8;
            card.style.paddingTop = card.style.paddingBottom = 8;
            card.style.backgroundColor = new Color(0.13f, 0.15f, 0.21f, 1f);
            card.style.alignItems = Align.Center;
            UiStyles.Border(card, 1, new Color(titleColor.r, titleColor.g, titleColor.b, 0.6f), 8);

            var image = new VisualElement();
            image.style.width = image.style.height = 44;
            image.style.marginBottom = 4;
            if (icon != null) image.style.backgroundImage = new StyleBackground(icon);
            image.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            card.Add(image);

            Label name = UiStyles.Text(title, 14, titleColor);
            name.style.whiteSpace = WhiteSpace.Normal;
            name.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(name);

            Label lines = UiStyles.Text(body, 12, new Color(0.85f, 0.88f, 0.95f), bold: false);
            lines.style.unityTextOutlineWidth = 0f;
            lines.style.whiteSpace = WhiteSpace.Normal;
            lines.style.unityTextAlign = TextAnchor.MiddleCenter;
            lines.style.marginTop = 4;
            lines.style.flexGrow = 1;
            card.Add(lines);

            Button action = MenuStyles.Button(button, onClick);
            action.style.height = 30;
            action.style.fontSize = 13;
            action.style.alignSelf = Align.Stretch;
            action.style.marginBottom = 0;
            action.SetEnabled(enabled);
            card.Add(action);

            if (!string.IsNullOrEmpty(note)) {
                Label n = UiStyles.Text(note, 11, new Color(1f, 0.55f, 0.45f), bold: false);
                n.style.unityTextOutlineWidth = 0f;
                n.style.marginTop = 3;
                card.Add(n);
            }
            return card;
        }

        /// <summary>Name, color, icon and stat lines of any offer or anvil choice.</summary>
        public static (string name, Color color, Sprite icon, string body) Describe(ScriptableObject offer, RarityTier rarity = RarityTier.Silver) {
            switch (offer) {
                case ItemDefinition item:
                    return (item.DisplayName, StatLabels.Color(item.Tier), item.Icon, StatLabels.Format(item.Stats));
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
