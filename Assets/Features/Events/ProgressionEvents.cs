using Systems.EventBusses;

namespace Features.Events {
    /// <summary>Gives XP to the player (kills later, debug tools now).</summary>
    public readonly struct XpGrantedEvent : IEvent {
        public readonly int Amount;
        public XpGrantedEvent(int amount) => Amount = amount;
    }

    /// <summary>Player XP changed (HUD).</summary>
    public readonly struct XpChangedEvent : IEvent {
        public readonly int Level;
        public readonly int Xp;
        public readonly int XpToNext;   // 0 at max level

        public XpChangedEvent(int level, int xp, int xpToNext) {
            Level = level;
            Xp = xp;
            XpToNext = xpToNext;
        }
    }

    /// <summary>Player reached a new level (design doc §14.3).</summary>
    public readonly struct LevelUpEvent : IEvent {
        public readonly int Level;
        public readonly int SkillPoints;
        public readonly int OverchargePoints;

        public LevelUpEvent(int level, int skillPoints, int overchargePoints) {
            Level = level;
            SkillPoints = skillPoints;
            OverchargePoints = overchargePoints;
        }
    }
}
