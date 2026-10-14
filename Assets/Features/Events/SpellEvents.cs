using Features.Spells;
using Systems.EventBusses;

namespace Features.Events {
    /// <summary>
    /// The player's 6 spell slots (Q W E R D F, AbilitySlots), their ranks and what a skill point
    /// can buy (design doc §7.8, §7.9). Published at init and on every change, for the HUD.
    /// Summoner slots have rank 1 when they hold a spell.
    /// </summary>
    public readonly struct AbilitiesChangedEvent : IEvent {
        public readonly SpellDefinition[] Spells;
        public readonly int[] Ranks;
        public readonly bool[] CanRankUp;
        public readonly int SkillPoints;

        public AbilitiesChangedEvent(SpellDefinition[] spells, int[] ranks, bool[] canRankUp, int skillPoints) {
            Spells = spells;
            Ranks = ranks;
            CanRankUp = canRankUp;
            SkillPoints = skillPoints;
        }
    }

    /// <summary>The player's champion (HUD portrait, shop recommendations): published when the player is bound to the bus.</summary>
    public readonly struct PlayerChampionEvent : IEvent {
        public readonly string Name;
        public readonly UnityEngine.Sprite Portrait;
        public readonly Passives.PassiveDefinition Passive;
        public readonly System.Collections.Generic.IReadOnlyList<Economy.ItemDefinition> RecommendedItems;

        public PlayerChampionEvent(string name, UnityEngine.Sprite portrait, Passives.PassiveDefinition passive,
                                   System.Collections.Generic.IReadOnlyList<Economy.ItemDefinition> recommendedItems = null) {
            Name = name;
            Portrait = portrait;
            Passive = passive;
            RecommendedItems = recommendedItems ?? System.Array.Empty<Economy.ItemDefinition>();
        }
    }

    /// <summary>One passive of the player that shows on the HUD buff bar (stacks, charged spellblade…).</summary>
    public readonly struct PlayerBuff {
        public readonly string Name;
        public readonly string Description;
        public readonly UnityEngine.Sprite Icon;
        public readonly int Stacks;
        public readonly bool Charged;

        public PlayerBuff(string name, string description, UnityEngine.Sprite icon, int stacks, bool charged) {
            Name = name;
            Description = description;
            Icon = icon;
            Stacks = stacks;
            Charged = charged;
        }
    }

    /// <summary>The player's active stacks and charges (HUD buff bar), published when they change.</summary>
    public readonly struct PlayerBuffsEvent : IEvent {
        public readonly PlayerBuff[] Buffs;
        public PlayerBuffsEvent(PlayerBuff[] buffs) => Buffs = buffs;
    }

    /// <summary>Spend a skill point on an ability (the HUD "+" button; Ctrl + key goes through AbilityKeyEvent).</summary>
    public readonly struct AbilityRankUpRequestedEvent : IEvent {
        public readonly int Slot;
        public AbilityRankUpRequestedEvent(int slot) => Slot = slot;
    }

    /// <summary>Short message for the player ("Not learned"…), shown by the HUD.</summary>
    public readonly struct GameplayFeedbackEvent : IEvent {
        public readonly string Message;
        public GameplayFeedbackEvent(string message) => Message = message;
    }

    /// <summary>Which aiming mode the player is in: the attack-move and spell aims are exclusive (design doc §7.4).</summary>
    public enum AimMode { None, AttackMove, Ability }

    public readonly struct AimModeEvent : IEvent {
        public readonly AimMode Mode;
        /// <summary>Ability: the spell slot being aimed (AbilitySlots).</summary>
        public readonly int Slot;

        public AimModeEvent(AimMode mode, int slot = -1) {
            Mode = mode;
            Slot = slot;
        }
    }

    /// <summary>A player cooldown (re)started: the HUD counts down from Duration (0 = ready).</summary>
    public readonly struct CooldownStartedEvent : IEvent {
        public readonly int Slot;
        public readonly float Duration;

        public CooldownStartedEvent(int slot, float duration) {
            Slot = slot;
            Duration = duration;
        }
    }

    /// <summary>A spell took effect (design doc §14.3): passives, audio, VFX.</summary>
    public readonly struct SpellCastEvent : IEvent {
        public readonly SpellDefinition Spell;
        public readonly bool ByPlayer;

        public SpellCastEvent(SpellDefinition spell, bool byPlayer) {
            Spell = spell;
            ByPlayer = byPlayer;
        }
    }
}
