using System;
using Features.Events;
using Features.Spells;
using UnityEngine;
using UnityEngine.UIElements;

namespace Features.UI {
    /// <summary>
    /// The 3 element slots of the HUD (design doc §7.9): icon, key, stacks (0–3), element level,
    /// padlock with the level required when locked, "+" when a skill point can be spent.
    /// Plain view built by PlayerHud; raises <see cref="LevelUpClicked"/>.
    /// </summary>
    public sealed class ElementBarView {
        private static readonly string[] Keys = { "A", "Z", "E" };
        private const int MaxStacks = 3;
        private const int MaxLevels = 5;

        private readonly Slot[] _slots = new Slot[ElementSelection.SlotCount];
        public VisualElement Root { get; }
        public event Action<int> LevelUpClicked;

        private sealed class Slot {
            public VisualElement Frame, Icon, Lock;
            public Label LockText;
            public Button Plus;
            public readonly VisualElement[] StackPips = new VisualElement[MaxStacks];
            public readonly VisualElement[] LevelPips = new VisualElement[MaxLevels];
            public Color Color = Color.white;
        }

        public ElementBarView() {
            Root = new VisualElement { pickingMode = PickingMode.Ignore };
            Root.style.flexDirection = FlexDirection.Row;
            Root.style.justifyContent = Justify.Center;
            for (int i = 0; i < _slots.Length; i++) Root.Add(BuildSlot(i));
        }

        public void SetElements(PlayerElementsEvent e) {
            for (int i = 0; i < _slots.Length; i++) {
                ElementDefinition element = e[i];
                if (element == null) continue;
                _slots[i].Color = element.Color;
                _slots[i].Icon.style.backgroundImage = element.Icon != null ? new StyleBackground(element.Icon) : StyleKeyword.None;
                UiStyles.Border(_slots[i].Frame, 2, element.Color * 0.8f, 6);
            }
        }

        public void SetSelection(ElementSelectionChangedEvent e) {
            for (int i = 0; i < _slots.Length; i++) {
                Slot slot = _slots[i];
                int stacks = e.Stacks(i);
                for (int p = 0; p < MaxStacks; p++)
                    slot.StackPips[p].style.backgroundColor = p < stacks ? slot.Color : new Color(0f, 0f, 0f, 0.55f);
                slot.Frame.style.scale = new Scale(Vector3.one * (stacks > 0 ? 1.08f : 1f));
            }
        }

        public void SetLevels(ElementLevelsChangedEvent e) {
            for (int i = 0; i < _slots.Length; i++) {
                Slot slot = _slots[i];
                int level = e.Level(i);
                bool locked = level == 0;
                slot.Lock.style.display = locked ? DisplayStyle.Flex : DisplayStyle.None;
                slot.LockText.text = e.CanLevel(i) || e.NextUnlockLevel == 0 ? "" : $"Niv. {e.NextUnlockLevel}";
                slot.Plus.style.visibility = e.CanLevel(i) ? Visibility.Visible : Visibility.Hidden;
                for (int p = 0; p < MaxLevels; p++)
                    slot.LevelPips[p].style.backgroundColor = p < level ? new Color(1f, 0.85f, 0.35f) : new Color(0f, 0f, 0f, 0.5f);
            }
        }

        private VisualElement BuildSlot(int index) {
            var slot = new Slot();
            _slots[index] = slot;

            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.style.alignItems = Align.Center;
            column.style.marginLeft = column.style.marginRight = 6;

            slot.Plus = new Button(() => LevelUpClicked?.Invoke(index)) { text = "+" };
            slot.Plus.style.width = 22;
            slot.Plus.style.height = 18;
            slot.Plus.style.marginBottom = 2;
            slot.Plus.style.fontSize = 14;
            slot.Plus.style.unityFontStyleAndWeight = FontStyle.Bold;
            slot.Plus.style.color = new Color(0.1f, 0.1f, 0.1f);
            slot.Plus.style.backgroundColor = new Color(1f, 0.85f, 0.35f);
            slot.Plus.style.paddingLeft = slot.Plus.style.paddingRight = slot.Plus.style.paddingTop = slot.Plus.style.paddingBottom = 0;
            UiStyles.Border(slot.Plus, 1, new Color(0.3f, 0.2f, 0f), 4);
            slot.Plus.style.visibility = Visibility.Hidden;
            column.Add(slot.Plus);

            slot.Frame = new VisualElement { pickingMode = PickingMode.Ignore };
            slot.Frame.style.width = slot.Frame.style.height = 52;
            slot.Frame.style.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.9f);
            UiStyles.Border(slot.Frame, 2, new Color(0.4f, 0.4f, 0.45f), 6);

            slot.Icon = new VisualElement { pickingMode = PickingMode.Ignore };
            UiStyles.Fill(slot.Icon);
            slot.Icon.style.left = slot.Icon.style.right = slot.Icon.style.top = slot.Icon.style.bottom = 6;
            slot.Icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            slot.Frame.Add(slot.Icon);

            var key = UiStyles.Text(Keys[index], 12, Color.white);
            key.style.position = Position.Absolute;
            key.style.left = 3;
            key.style.top = 1;
            slot.Frame.Add(key);

            var pips = new VisualElement { pickingMode = PickingMode.Ignore };
            pips.style.position = Position.Absolute;
            pips.style.bottom = 3;
            pips.style.left = pips.style.right = 0;
            pips.style.flexDirection = FlexDirection.Row;
            pips.style.justifyContent = Justify.Center;
            for (int p = 0; p < MaxStacks; p++) {
                var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                pip.style.width = pip.style.height = 8;
                pip.style.marginLeft = pip.style.marginRight = 1;
                UiStyles.Border(pip, 1, new Color(0f, 0f, 0f, 0.9f), 2);
                slot.StackPips[p] = pip;
                pips.Add(pip);
            }
            slot.Frame.Add(pips);

            slot.Lock = new VisualElement { pickingMode = PickingMode.Ignore };
            UiStyles.Fill(slot.Lock);
            slot.Lock.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            slot.Lock.style.justifyContent = Justify.Center;
            slot.Lock.style.alignItems = Align.Center;
            slot.LockText = UiStyles.Text("", 10, new Color(1f, 0.85f, 0.5f));
            slot.Lock.Add(slot.LockText);
            slot.Frame.Add(slot.Lock);
            column.Add(slot.Frame);

            var levels = new VisualElement { pickingMode = PickingMode.Ignore };
            levels.style.flexDirection = FlexDirection.Row;
            levels.style.marginTop = 3;
            for (int p = 0; p < MaxLevels; p++) {
                var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                pip.style.width = 7;
                pip.style.height = 4;
                pip.style.marginLeft = pip.style.marginRight = 1;
                slot.LevelPips[p] = pip;
                levels.Add(pip);
            }
            column.Add(levels);
            return column;
        }
    }
}
