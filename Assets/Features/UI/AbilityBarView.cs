using System;
using Features.Events;
using Features.Spells;
using UnityEngine;
using UnityEngine.UIElements;

namespace Features.UI {
    /// <summary>
    /// LoL-style spell bar of the player HUD (design doc §7.9): Q W E R (big slots: icon, key, rank
    /// pips, "+" when a skill point can rank it up, greyed while not learned) then D F (summoner
    /// spells). Cooldowns: dark veil + seconds left. Built in code, fed by PlayerHud.
    /// </summary>
    public sealed class AbilityBarView {
        private const float AbilitySize = 52f, SummonerSize = 40f;

        private sealed class Slot {
            public VisualElement Box, Icon, Shade, Locked, Pips;
            public Label Cooldown, Key;
            public Button Plus;
            public float ReadyAt;
        }

        private readonly Slot[] _slots = new Slot[AbilitySlots.Count];

        public VisualElement Root { get; }
        /// <summary>The "+" of an ability was clicked (slot).</summary>
        public event Action<int> RankUpClicked;

        public AbilityBarView() {
            Root = new VisualElement { pickingMode = PickingMode.Ignore };
            Root.style.flexDirection = FlexDirection.Row;
            Root.style.alignItems = Align.FlexEnd;
            for (int i = 0; i < AbilitySlots.Count; i++) {
                bool ability = AbilitySlots.IsAbility(i);
                _slots[i] = BuildSlot(i, ability ? AbilitySize : SummonerSize);
                if (i == AbilitySlots.SummonerD) _slots[i].Box.style.marginLeft = 14;
                Root.Add(_slots[i].Box);
            }
        }

        /// <summary>Key labels in slot order (Q W E R D F, as printed on the player's keyboard).</summary>
        public void SetKeys(string[] keys) {
            for (int i = 0; i < _slots.Length && i < keys.Length; i++) _slots[i].Key.text = keys[i];
        }

        public void SetAbilities(AbilitiesChangedEvent e) {
            for (int i = 0; i < _slots.Length; i++) {
                Slot slot = _slots[i];
                SpellDefinition spell = e.Spells[i];
                slot.Icon.style.backgroundImage = spell != null && spell.Icon != null ? new StyleBackground(spell.Icon) : StyleKeyword.None;
                slot.Box.style.opacity = spell != null ? 1f : 0.45f;
                bool ability = AbilitySlots.IsAbility(i);
                slot.Locked.style.display = ability && spell != null && e.Ranks[i] <= 0 ? DisplayStyle.Flex : DisplayStyle.None;
                if (!ability) continue;
                slot.Plus.style.visibility = e.CanRankUp[i] ? Visibility.Visible : Visibility.Hidden;
                BuildPips(slot, spell != null ? spell.MaxRank : 0, e.Ranks[i]);
            }
        }

        public void StartCooldown(int slot, float duration) {
            if (slot >= 0 && slot < _slots.Length) _slots[slot].ReadyAt = Time.time + duration;
        }

        /// <summary>Every frame: veils and seconds left.</summary>
        public void Tick() {
            foreach (Slot slot in _slots) {
                float left = slot.ReadyAt - Time.time;
                bool cooling = left > 0f;
                slot.Shade.style.display = cooling ? DisplayStyle.Flex : DisplayStyle.None;
                slot.Cooldown.text = !cooling ? "" : left < 1f ? left.ToString("0.0") : Mathf.CeilToInt(left).ToString();
            }
        }

        private Slot BuildSlot(int index, float size) {
            var slot = new Slot();
            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.style.alignItems = Align.Center;
            column.style.marginLeft = column.style.marginRight = 3;
            slot.Box = column;

            bool ability = AbilitySlots.IsAbility(index);
            slot.Plus = new Button(() => RankUpClicked?.Invoke(index)) { text = "+" };
            slot.Plus.style.width = slot.Plus.style.height = 18;
            slot.Plus.style.fontSize = 14;
            slot.Plus.style.unityFontStyleAndWeight = FontStyle.Bold;
            slot.Plus.style.color = new Color(0.1f, 0.08f, 0.02f);
            slot.Plus.style.backgroundColor = new Color(1f, 0.82f, 0.3f);
            slot.Plus.style.marginBottom = 3;
            slot.Plus.style.paddingLeft = slot.Plus.style.paddingRight = slot.Plus.style.paddingTop = slot.Plus.style.paddingBottom = 0;
            UiStyles.Border(slot.Plus, 1, new Color(0.3f, 0.2f, 0.05f), 9);
            slot.Plus.style.visibility = Visibility.Hidden;
            if (ability) column.Add(slot.Plus);

            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.style.width = frame.style.height = size;
            frame.style.backgroundColor = new Color(0.07f, 0.07f, 0.11f, 0.92f);
            UiStyles.Border(frame, 2, ability ? new Color(0.78f, 0.66f, 0.38f) : new Color(0.55f, 0.55f, 0.62f), 6);
            frame.style.overflow = Overflow.Hidden;
            column.Add(frame);

            slot.Icon = new VisualElement { pickingMode = PickingMode.Ignore };
            UiStyles.Fill(slot.Icon);
            slot.Icon.style.left = slot.Icon.style.right = slot.Icon.style.top = slot.Icon.style.bottom = 3;
            slot.Icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            frame.Add(slot.Icon);

            slot.Locked = new VisualElement { pickingMode = PickingMode.Ignore };
            UiStyles.Fill(slot.Locked);
            slot.Locked.style.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 0.7f);
            frame.Add(slot.Locked);

            slot.Shade = new VisualElement { pickingMode = PickingMode.Ignore };
            UiStyles.Fill(slot.Shade);
            slot.Shade.style.backgroundColor = new Color(0f, 0f, 0f, 0.62f);
            slot.Shade.style.display = DisplayStyle.None;
            frame.Add(slot.Shade);

            slot.Cooldown = UiStyles.Text("", ability ? 18 : 14, Color.white);
            UiStyles.Fill(slot.Cooldown);
            slot.Cooldown.style.unityTextAlign = TextAnchor.MiddleCenter;
            frame.Add(slot.Cooldown);

            slot.Key = UiStyles.Text("", 11, Color.white);
            slot.Key.style.position = Position.Absolute;
            slot.Key.style.left = 3;
            slot.Key.style.bottom = 1;
            frame.Add(slot.Key);

            slot.Pips = new VisualElement { pickingMode = PickingMode.Ignore };
            slot.Pips.style.flexDirection = FlexDirection.Row;
            slot.Pips.style.height = 6;
            slot.Pips.style.marginTop = 3;
            column.Add(slot.Pips);
            return slot;
        }

        private static void BuildPips(Slot slot, int max, int rank) {
            slot.Pips.Clear();
            for (int r = 1; r <= max; r++) {
                var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                pip.style.width = 7;
                pip.style.height = 5;
                pip.style.marginLeft = pip.style.marginRight = 1;
                pip.style.backgroundColor = r <= rank ? new Color(1f, 0.82f, 0.3f) : new Color(0.2f, 0.2f, 0.26f, 0.9f);
                UiStyles.Border(pip, 1, new Color(0f, 0f, 0f, 0.8f), 1);
                slot.Pips.Add(pip);
            }
        }
    }
}
