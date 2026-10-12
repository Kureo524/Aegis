using System;
using Features.Options;
using Systems.Inputs;
using UnityEngine;
using UnityEngine.UIElements;

namespace Features.UI.Panels {
    /// <summary>
    /// Options screen content (UI Toolkit, built in code), shared by the pause menu and the main
    /// menu. Edits <see cref="UserOptions"/> directly: every change is saved at once.
    /// </summary>
    public sealed class OptionsView : VisualElement {
        private static readonly (CastMode mode, string label, string hint)[] CastModes = {
            (CastMode.Normal, "Normal", "R / F affichent la portée, le clic gauche lance."),
            (CastMode.QuickWithIndicator, "Rapide avec indicateur",
             "Maintiens R / F pour voir la portée, relâche pour lancer vers le curseur."),
            (CastMode.Quick, "Rapide", "R / F lancent tout de suite vers le curseur, sans rien afficher."),
        };

        public event Action Back;

        private readonly UserOptions _options;
        private readonly Button[] _castButtons = new Button[CastModes.Length];
        private readonly Label _castHint;

        public OptionsView(UserOptions options) {
            _options = options;
            style.alignItems = Align.Stretch;

            Add(MenuStyles.Title("Options"));
            Add(MenuStyles.Section("Lancement des sorts"));
            for (int i = 0; i < CastModes.Length; i++) {
                CastMode mode = CastModes[i].mode;
                _castButtons[i] = MenuStyles.Button(CastModes[i].label, () => {
                    _options.CastMode = mode;
                    Refresh();
                });
                Add(_castButtons[i]);
            }
            _castHint = MenuStyles.Hint("");
            Add(_castHint);

            Button back = MenuStyles.Button("Retour", () => Back?.Invoke());
            back.style.marginTop = 18;
            Add(back);
            Refresh();
        }

        /// <summary>Shows the saved values (the other menu may have changed them).</summary>
        public void Refresh() {
            for (int i = 0; i < CastModes.Length; i++) {
                bool selected = CastModes[i].mode == _options.CastMode;
                MenuStyles.Select(_castButtons[i], selected);
                if (selected) _castHint.text = CastModes[i].hint;
            }
        }
    }

    /// <summary>Inline styles of the code-built menus (pause, options).</summary>
    public static class MenuStyles {
        private static readonly Color Panel = new(0.08f, 0.09f, 0.13f, 0.96f);
        private static readonly Color ButtonColor = new(0.20f, 0.24f, 0.34f);
        private static readonly Color Accent = new(0.95f, 0.80f, 0.40f);

        /// <summary>Full-screen dimmed layer that blocks clicks to what is behind, with a centered box.</summary>
        public static VisualElement Overlay(out VisualElement box) {
            var overlay = new VisualElement();
            UiStyles.Fill(overlay);
            overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;

            box = new VisualElement();
            box.style.width = 420;
            box.style.backgroundColor = Panel;
            box.style.paddingLeft = box.style.paddingRight = 26;
            box.style.paddingTop = box.style.paddingBottom = 22;
            UiStyles.Border(box, 2, new Color(1f, 1f, 1f, 0.15f), 10);
            overlay.Add(box);
            return overlay;
        }

        public static Label Title(string text) {
            Label title = UiStyles.Text(text, 26, Accent);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginBottom = 16;
            return title;
        }

        public static Label Hint(string text) {
            Label hint = UiStyles.Text(text, 14, new Color(0.80f, 0.83f, 0.90f), bold: false);
            hint.style.unityTextOutlineWidth = 0f;
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginTop = 2;
            hint.style.marginLeft = 4;
            return hint;
        }

        public static Button Button(string text, Action onClick) {
            var button = new Button(onClick) { text = text };
            button.style.height = 40;
            button.style.marginTop = button.style.marginBottom = 5;
            button.style.marginLeft = button.style.marginRight = 0;
            button.style.fontSize = 17;
            button.style.color = Color.white;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.backgroundColor = ButtonColor;
            UiStyles.Border(button, 1, new Color(1f, 1f, 1f, 0.2f), 6);
            return button;
        }

        public static Label Section(string text) {
            Label section = UiStyles.Text(text, 16, Color.white);
            section.style.marginBottom = 4;
            return section;
        }

        /// <summary>Highlights the chosen entry of a list of choices.</summary>
        public static void Select(Button button, bool selected) {
            button.style.backgroundColor = selected ? new Color(0.45f, 0.36f, 0.12f) : ButtonColor;
            UiStyles.Border(button, selected ? 2 : 1, selected ? Accent : new Color(1f, 1f, 1f, 0.2f), 6);
        }
    }
}
