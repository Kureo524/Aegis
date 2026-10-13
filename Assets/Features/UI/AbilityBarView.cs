using System;
using Features.Events;
using Features.Passives;
using Features.Spells;
using UnityEngine;
using UnityEngine.UIElements;

namespace Features.UI {
    /// <summary>
    /// LoL-style spell bar of the player HUD (design doc §7.9): the champion's passive, Q W E R (icon,
    /// key, rank pips, "+" when a skill point can rank it up, greyed while not learned) and D F.
    /// Cooldowns: LoL-style sweep (CooldownSweep) + seconds left, a flash when the spell is ready again. Hover: tooltip with rank, cost, cooldown and description.
    /// Styled by the theme (.ae-slot…), built in code, fed by PlayerHud.
    /// </summary>
    public sealed class AbilityBarView {
        private const float AbilitySize = 56f, SummonerSize = 42f;

        private sealed class Slot {
            public VisualElement Box, Frame, Icon, Locked, Pips;
            public CooldownSweep Sweep;
            public float Total, ReadyFlashUntil;
            public bool WasCooling;
            public Label Cooldown, Key;
            public Button Plus;
            public float ReadyAt;
        }

        private readonly Slot[] _slots = new Slot[AbilitySlots.Count];
        private readonly VisualElement _passive;
        private readonly Label _passiveStacks;
        private SpellDefinition[] _spells = new SpellDefinition[AbilitySlots.Count];
        private int[] _ranks = new int[AbilitySlots.Count];
        private string[] _keys = { "Q", "W", "E", "R", "D", "F" };
        private PassiveDefinition _passiveDefinition;

        public VisualElement Root { get; }
        /// <summary>The "+" of an ability was clicked (slot).</summary>
        public event Action<int> RankUpClicked;

        public AbilityBarView(UiTooltip tooltip) {
            Root = new VisualElement { pickingMode = PickingMode.Ignore };
            Root.style.flexDirection = FlexDirection.Row;
            Root.style.alignItems = Align.FlexEnd;

            // Passive: smaller, left of Q, with its stacks
            var passiveColumn = new VisualElement { pickingMode = PickingMode.Ignore };
            passiveColumn.style.marginRight = 6;
            passiveColumn.style.marginBottom = 9;
            _passive = new VisualElement();
            _passive.AddToClassList("ae-slot");
            _passive.style.width = _passive.style.height = 36;
            _passive.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            _passiveStacks = Text("", 12);
            _passiveStacks.style.position = Position.Absolute;
            _passiveStacks.style.right = 2;
            _passiveStacks.style.bottom = 0;
            _passive.Add(_passiveStacks);
            passiveColumn.Add(_passive);
            tooltip.Attach(_passive, () => _passiveDefinition == null ? default
                : (_passiveDefinition.DisplayName, "Passif", _passiveDefinition.Description));
            Root.Add(passiveColumn);

            for (int i = 0; i < AbilitySlots.Count; i++) {
                bool ability = AbilitySlots.IsAbility(i);
                _slots[i] = BuildSlot(i, ability ? AbilitySize : SummonerSize);
                if (i == AbilitySlots.SummonerD) _slots[i].Box.style.marginLeft = 12;
                int slot = i;
                tooltip.Attach(_slots[i].Frame, () => Describe(slot));
                Root.Add(_slots[i].Box);
            }
        }

        /// <summary>Key labels in slot order (Q W E R D F, as printed on the player's keyboard).</summary>
        public void SetKeys(string[] keys) {
            _keys = keys;
            for (int i = 0; i < _slots.Length && i < keys.Length; i++) _slots[i].Key.text = keys[i];
        }

        public void SetPassive(PassiveDefinition passive) {
            _passiveDefinition = passive;
            _passive.style.backgroundImage = passive != null && passive.Icon != null ? new StyleBackground(passive.Icon) : StyleKeyword.None;
            _passive.style.display = passive != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Stacks of the champion's passive (0: hidden).</summary>
        public void SetPassiveStacks(int stacks) => _passiveStacks.text = stacks > 0 ? stacks.ToString() : "";

        public void SetAbilities(AbilitiesChangedEvent e) {
            _spells = e.Spells;
            _ranks = e.Ranks;
            for (int i = 0; i < _slots.Length; i++) {
                Slot slot = _slots[i];
                SpellDefinition spell = e.Spells[i];
                slot.Icon.style.backgroundImage = spell != null && spell.Icon != null ? new StyleBackground(spell.Icon) : StyleKeyword.None;
                slot.Frame.EnableInClassList("ae-slot--empty", spell == null);
                bool ability = AbilitySlots.IsAbility(i);
                slot.Locked.style.display = ability && spell != null && e.Ranks[i] <= 0 ? DisplayStyle.Flex : DisplayStyle.None;
                if (!ability) continue;
                slot.Plus.style.visibility = e.CanRankUp[i] ? Visibility.Visible : Visibility.Hidden;
                BuildPips(slot, spell != null ? spell.MaxRank : 0, e.Ranks[i]);
            }
        }

        public void StartCooldown(int slot, float duration) {
            if (slot < 0 || slot >= _slots.Length) return;
            Slot s = _slots[slot];
            // A refund (shorter remaining time) keeps the original length of the sweep
            s.Total = Time.time >= s.ReadyAt ? duration : Mathf.Max(s.Total, duration);
            s.ReadyAt = Time.time + duration;
        }

        /// <summary>Every frame: veils and seconds left.</summary>
        public void Tick() {
            foreach (Slot slot in _slots) {
                float left = slot.ReadyAt - Time.time;
                bool cooling = left > 0f;
                slot.Sweep.Fraction = cooling && slot.Total > 0f ? left / slot.Total : 0f;
                slot.Cooldown.text = !cooling ? "" : left < 1f ? left.ToString("0.0") : Mathf.CeilToInt(left).ToString();
                if (slot.WasCooling && !cooling) slot.ReadyFlashUntil = Time.time + 0.35f;   // ready again: flash
                slot.WasCooling = cooling;
                slot.Frame.EnableInClassList("ae-slot--ready", Time.time < slot.ReadyFlashUntil);
            }
        }

        private (string, string, string) Describe(int slot) {
            SpellDefinition spell = slot < _spells.Length ? _spells[slot] : null;
            if (spell == null) return slot == AbilitySlots.SummonerD ? ("Sort d'invocateur", "D", "Vide : un sort d'invocateur ramassé vient ici, pour un usage.") : default;
            int rank = Mathf.Max(1, _ranks[slot]);
            string key = slot < _keys.Length ? _keys[slot] : "";
            string meta;
            if (AbilitySlots.IsAbility(slot)) {
                string learned = _ranks[slot] > 0 ? $"Rang {_ranks[slot]}/{spell.MaxRank}" : $"Non appris (niveau {spell.UnlockLevel(1)})";
                string cost = spell.ManaCost(rank) > 0f ? $" · {spell.ManaCost(rank):0} mana" : "";
                meta = $"{key} · {learned}{cost} · {spell.Cooldown(rank):0.#} s";
            }
            else meta = $"{key} · Sort d'invocateur · {spell.Cooldown(rank):0.#} s";
            return (spell.DisplayName, meta, spell.Description);
        }

        private Slot BuildSlot(int index, float size) {
            var slot = new Slot();
            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.style.alignItems = Align.Center;
            column.style.marginLeft = column.style.marginRight = 2;
            slot.Box = column;

            bool ability = AbilitySlots.IsAbility(index);
            slot.Plus = new Button(() => RankUpClicked?.Invoke(index)) { text = "+" };
            slot.Plus.AddToClassList("ae-plus");
            slot.Plus.style.visibility = Visibility.Hidden;
            if (ability) column.Add(slot.Plus);

            slot.Frame = new VisualElement();
            slot.Frame.AddToClassList("ae-slot");
            if (ability) slot.Frame.AddToClassList("ae-slot--ability");
            slot.Frame.style.width = slot.Frame.style.height = size;
            column.Add(slot.Frame);

            slot.Icon = Layer(slot.Frame);
            slot.Icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            slot.Locked = Layer(slot.Frame);
            slot.Locked.AddToClassList("ae-slot__locked");
            slot.Sweep = new CooldownSweep();
            UiStyles.Fill(slot.Sweep);
            slot.Frame.Add(slot.Sweep);

            slot.Cooldown = Text("", ability ? 18 : 14);
            UiStyles.Fill(slot.Cooldown);
            slot.Cooldown.style.unityTextAlign = TextAnchor.MiddleCenter;
            slot.Frame.Add(slot.Cooldown);

            slot.Key = Text("", 10);
            slot.Key.AddToClassList("ae-slot__key");
            slot.Frame.Add(slot.Key);

            slot.Pips = new VisualElement { pickingMode = PickingMode.Ignore };
            slot.Pips.style.flexDirection = FlexDirection.Row;
            slot.Pips.style.height = 5;
            slot.Pips.style.marginTop = 3;
            column.Add(slot.Pips);
            return slot;
        }

        private static VisualElement Layer(VisualElement parent) {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            UiStyles.Fill(layer);
            parent.Add(layer);
            return layer;
        }

        private static Label Text(string text, float size) {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("ae-outline");
            label.style.fontSize = size;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private static void BuildPips(Slot slot, int max, int rank) {
            slot.Pips.Clear();
            for (int r = 1; r <= max; r++) {
                var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                pip.AddToClassList("ae-pip");
                pip.EnableInClassList("ae-pip--on", r <= rank);
                slot.Pips.Add(pip);
            }
        }
    }
}
