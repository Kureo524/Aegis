using System.Collections.Generic;
using Features.Augments;
using Features.Economy;
using Systems.EventBusses;

namespace Features.Events {
    public enum AugmentDraftPhase { Closed, Choose, SlotsFull }

    /// <summary>Augment draft snapshot (AugmentController → AugmentPanel).</summary>
    public readonly struct AugmentDraftEvent : IEvent {
        public readonly AugmentDraftPhase Phase;
        public readonly RarityTier Rarity;
        public readonly IReadOnlyList<AugmentDefinition> Choices;
        /// <summary>Rerolls left on each card of this draft.</summary>
        public readonly IReadOnlyList<int> CardRerolls;
        /// <summary>The draft still offers its golden reroll (one card, rarity + 1).</summary>
        public readonly bool GoldenReroll;
        /// <summary>SlotsFull: the augment just chosen, and the owned augments it may replace.</summary>
        public readonly AugmentDefinition Pending;
        public readonly IReadOnlyList<AugmentDefinition> Replaceable;
        public readonly bool CanAddSlot;
        public readonly int Gold;

        public AugmentDraftEvent(AugmentDraftPhase phase, RarityTier rarity, IReadOnlyList<AugmentDefinition> choices, IReadOnlyList<int> cardRerolls,
                                 bool goldenReroll, AugmentDefinition pending, IReadOnlyList<AugmentDefinition> replaceable,
                                 bool canAddSlot, int gold) {
            Phase = phase;
            Rarity = rarity;
            Choices = choices;
            CardRerolls = cardRerolls;
            GoldenReroll = goldenReroll;
            Pending = pending;
            Replaceable = replaceable;
            CanAddSlot = canAddSlot;
            Gold = gold;
        }
    }

    public readonly struct AugmentPickRequestedEvent : IEvent {
        public readonly int Index;
        public AugmentPickRequestedEvent(int index) => Index = index;
    }

    /// <summary>Reroll one card: its own reroll (same rarity), or the draft's golden reroll (rarity + 1).</summary>
    public readonly struct AugmentRerollRequestedEvent : IEvent {
        public readonly int Index;
        public readonly bool Golden;
        public AugmentRerollRequestedEvent(int index, bool golden = false) {
            Index = index;
            Golden = golden;
        }
    }

    public enum FullSlotsOption { Anvil, Replace, SlotOrGold }

    /// <summary>All slots full: the benefit chosen (Replace: index in Replaceable).</summary>
    public readonly struct AugmentFullSlotsRequestedEvent : IEvent {
        public readonly FullSlotsOption Option;
        public readonly int Index;
        public AugmentFullSlotsRequestedEvent(FullSlotsOption option, int index = 0) {
            Option = option;
            Index = index;
        }
    }

    /// <summary>The player's augments and slots (HUD).</summary>
    public readonly struct AugmentsChangedEvent : IEvent {
        public readonly IReadOnlyList<AugmentDefinition> Augments;
        public readonly int Slots;
        public AugmentsChangedEvent(IReadOnlyList<AugmentDefinition> augments, int slots) {
            Augments = augments;
            Slots = slots;
        }
    }
}
