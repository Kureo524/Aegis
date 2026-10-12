using Features.Spells;
using Systems.EventBusses;

namespace Features.Events {
    /// <summary>The player's 3 elements (HUD icons, colors). Published at init.</summary>
    public readonly struct PlayerElementsEvent : IEvent {
        public readonly ElementDefinition Element0, Element1, Element2;

        public PlayerElementsEvent(ElementDefinition e0, ElementDefinition e1, ElementDefinition e2) {
            Element0 = e0;
            Element1 = e1;
            Element2 = e2;
        }

        public ElementDefinition this[int slot] => slot switch { 0 => Element0, 1 => Element1, _ => Element2 };
    }

    /// <summary>Stacked elements, the spell they resolve to and its mana cost (design doc §7.9).</summary>
    public readonly struct ElementSelectionChangedEvent : IEvent {
        public readonly int Stacks0, Stacks1, Stacks2;
        public readonly int Total;
        public readonly SpellDefinition Spell;   // null when empty or no spell for this recipe
        public readonly float ManaCost;

        public ElementSelectionChangedEvent(int stacks0, int stacks1, int stacks2, int total, SpellDefinition spell, float manaCost) {
            Stacks0 = stacks0;
            Stacks1 = stacks1;
            Stacks2 = stacks2;
            Total = total;
            Spell = spell;
            ManaCost = manaCost;
        }

        public int Stacks(int slot) => slot switch { 0 => Stacks0, 1 => Stacks1, _ => Stacks2 };
    }

    /// <summary>Element levels and what can be bought with skill points (design doc §7.8).</summary>
    public readonly struct ElementLevelsChangedEvent : IEvent {
        public readonly int Level0, Level1, Level2;
        public readonly bool CanLevel0, CanLevel1, CanLevel2;
        public readonly int SkillPoints;
        public readonly int NextUnlockLevel;   // character level for the next unlock, 0 = none

        public ElementLevelsChangedEvent(int level0, int level1, int level2, bool canLevel0, bool canLevel1, bool canLevel2,
                                         int skillPoints, int nextUnlockLevel) {
            Level0 = level0;
            Level1 = level1;
            Level2 = level2;
            CanLevel0 = canLevel0;
            CanLevel1 = canLevel1;
            CanLevel2 = canLevel2;
            SkillPoints = skillPoints;
            NextUnlockLevel = nextUnlockLevel;
        }

        public int Level(int slot) => slot switch { 0 => Level0, 1 => Level1, _ => Level2 };
        public bool CanLevel(int slot) => slot switch { 0 => CanLevel0, 1 => CanLevel1, _ => CanLevel2 };
    }

    /// <summary>Spend a skill point on an element (Ctrl + A/Z/E, or the HUD "+" button).</summary>
    public readonly struct ElementLevelUpRequestedEvent : IEvent {
        public readonly int Slot;
        public ElementLevelUpRequestedEvent(int slot) => Slot = slot;
    }

    /// <summary>Short message for the player ("Element locked"…), shown by the HUD.</summary>
    public readonly struct GameplayFeedbackEvent : IEvent {
        public readonly string Message;
        public GameplayFeedbackEvent(string message) => Message = message;
    }
}
