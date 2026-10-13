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
    /// <summary>
    /// Building blocks of the menus and screens (pause, options, end of run, shop, anvils,
    /// augments), styled by the USS theme classes (Assets/UI/AegisTheme.uss): no colors here.
    /// </summary>
    public static class MenuStyles {
        /// <summary>Full-screen dimmed layer that blocks clicks to what is behind, with a centered box.</summary>
        public static VisualElement Overlay(out VisualElement box) {
            var overlay = new VisualElement();
            overlay.AddToClassList("ae-root");
            overlay.AddToClassList("ae-overlay");
            UiStyles.Fill(overlay);
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;

            box = new VisualElement();
            box.AddToClassList("ae-panel");
            box.AddToClassList("ae-window");
            box.style.width = 420;
            overlay.Add(box);
            return overlay;
        }

        public static Label Title(string text) {
            var title = new Label(text);
            title.AddToClassList("ae-title");
            title.style.marginBottom = 14;
            return title;
        }

        public static Label Hint(string text) {
            var hint = new Label(text);
            hint.AddToClassList("ae-label");
            hint.AddToClassList("ae-dim");
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginTop = 2;
            return hint;
        }

        public static Button Button(string text, Action onClick) {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("ae-button");
            button.style.height = 38;
            return button;
        }

        public static Label Section(string text) {
            var section = new Label(text);
            section.AddToClassList("ae-subtitle");
            section.style.marginBottom = 4;
            return section;
        }

        /// <summary>Highlights the chosen entry of a list of choices.</summary>
        public static void Select(Button button, bool selected) => button.EnableInClassList("ae-button--primary", selected);
    }
}
