using System;
using Features.Combat;
using Features.Economy;
using Features.Stats;
using UnityEngine;

namespace Features.Passives {
    /// <summary>What a passive reacts to (CharacterEvents, design doc §8.1). DamageTaken: the target is the attacker.</summary>
    public enum PassiveEvent { BasicAttackHit, SpellCast, Kill, DamageTaken, SpellHit }

    /// <summary>What a trigger does.</summary>
    public enum PassiveEffectKind {
        /// <summary>Stats for Duration seconds (TimedModifiers).</summary>
        TimedStats,
        /// <summary>Extra damage on the target of the hit: Damage + DamageRatio × ScalingStat.</summary>
        BonusDamage,
        /// <summary>Drops a loot on the ground where it happened (design doc §9.5).</summary>
        DropLoot,
        /// <summary>A status on the target of the hit (BasicAttackHit, SpellHit, §5.7).</summary>
        ApplyStatus,
        /// <summary>Heals the owner: Amount + AmountRatio × AmountStat.</summary>
        Heal,
        /// <summary>Shields the owner for Duration seconds: Amount + AmountRatio × AmountStat.</summary>
        Shield,
        /// <summary>Count homing bolts at the nearest other enemies within Radius of the target (Runaan, Luden…): the damage formula.</summary>
        Bolts,
        /// <summary>The damage formula to every other enemy within Radius of the target (Hydra cleave).</summary>
        Splash,
        /// <summary>Takes Amount seconds off the owner's ability cooldowns (Navori).</summary>
        ReduceCooldowns,
        /// <summary>Charges the passive for Duration seconds: a trigger with ConsumesCharge needs it (Spellblade: Sheen, Trinity Force…).</summary>
        Charge,
    }

    /// <summary>Loot that a passive can drop (design doc §9.5).</summary>
    public enum LootKind {
        /// <summary>A free stat anvil, opened at the end of the wave.</summary>
        StatAnvil,
        /// <summary>A single-use summoner spell that replaces F until used.</summary>
        SummonerSpell,
    }

    /// <summary>When it happens, how often, what it does.</summary>
    [Serializable]
    public struct PassiveTrigger {
        public PassiveEvent On;
        [Tooltip("1 = every time, 3 = every 3rd time.")]
        [Min(1)] public int EveryN;
        [Tooltip("Chance per occurrence (0..1).")]
        [Range(0f, 1f)] public float Chance;
        [Tooltip("Only while the owner's HP share is at most this (0 = always).")]
        [Range(0f, 1f)] public float HealthBelow;
        [Tooltip("Seconds before the trigger can go off again (0 = none).")]
        [Min(0f)] public float Cooldown;
        [Tooltip("Only when the passive is charged (Charge effect), and uses the charge up.")]
        public bool ConsumesCharge;
        public PassiveEffectKind Effect;
        [Header("TimedStats")]
        public StatBonus[] Stats;
        [Tooltip("TimedStats, Shield: seconds.")]
        [Min(0f)] public float Duration;
        [Header("BonusDamage / Bolts / Splash")]
        public float Damage;
        public StatType ScalingStat;
        public float DamageRatio;
        [Tooltip("+ this share of the target's max HP.")]
        public float TargetMaxHealthRatio;
        [Tooltip("+ this share of the target's current HP.")]
        public float TargetCurrentHealthRatio;
        public DamageType DamageType;
        [Tooltip("Bolts: how many. Splash / Bolts: Radius around the target.")]
        [Min(0)] public int Count;
        [Min(0f)] public float Radius;
        public Color BoltColor;
        [Header("DropLoot")]
        public LootKind Loot;
        [Header("ApplyStatus")]
        public StatusApplication Status;
        [Header("Heal / Shield")]
        public float Amount;
        public StatType AmountStat;
        public float AmountRatio;
    }

    /// <summary>Where stacks come from (design doc §8.1).</summary>
    public enum StackSource { None, PerBasicAttack, PerSpellCast, PerKill, PerSpellHit }

    /// <summary>When stacks go back to zero.</summary>
    public enum ResetPolicy { Never, EndOfWave, AfterDuration }

    /// <summary>A stack counter and what each stack gives.</summary>
    [Serializable]
    public struct StackSettings {
        public StackSource Source;
        public ResetPolicy Reset;
        [Tooltip("0 = no limit.")]
        [Min(0)] public int Max;
        [Tooltip("AfterDuration: stacks fall off this long after the last one gained.")]
        [Min(0f)] public float Duration;
        public StatBonus[] PerStack;
    }

    /// <summary>Damage to every enemy around the owner, every Interval seconds (Sunfire Aegis).</summary>
    [Serializable]
    public struct AuraSettings {
        [Tooltip("0 = no aura.")]
        [Min(0f)] public float Radius;
        [Min(0.1f)] public float Interval;
        public float Damage;
        public StatType ScalingStat;
        public float Ratio;
        public DamageType DamageType;
        [Tooltip("Heals the owner this much per enemy hit (Unending Despair).")]
        public float HealPerEnemy;
    }

    /// <summary>A share of a stat added to another one, recomputed continuously (Archangel's: bonus mana → AP).</summary>
    [Serializable]
    public struct StatConversion {
        public StatType From;
        public StatType To;
        public float Ratio;
        [Tooltip("Only the bonus part of From (items, buffs), not the base and level growth.")]
        public bool BonusOnly;
    }

    /// <summary>Continuous bonus recomputed from the number of enemies around (design doc §8.1).</summary>
    [Serializable]
    public struct NearbyEnemiesBonus {
        [Tooltip("0 = unused.")]
        [Min(0f)] public float Radius;
        [Min(0)] public int Max;
        public StatBonus[] PerEnemy;
    }

    /// <summary>
    /// A passive (design doc §8.1): the character's own, an item's or an augment's, all built from
    /// the same bricks: static stats, capabilities, triggers, a stack counter and a bonus per
    /// nearby enemy. Read-only data; the runtime is <see cref="Passive"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Passive", fileName = "Passive_")]
    public class PassiveDefinition : ScriptableObject {
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private StatBonus[] stats = Array.Empty<StatBonus>();
        [SerializeField] private Capability[] capabilities = Array.Empty<Capability>();
        [SerializeField] private PassiveTrigger[] triggers = Array.Empty<PassiveTrigger>();
        [SerializeField] private StackSettings stacks;
        [SerializeField] private NearbyEnemiesBonus nearbyEnemies;
        [SerializeField] private AuraSettings aura;
        [SerializeField] private StatConversion[] conversions = Array.Empty<StatConversion>();

        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public StatBonus[] Stats => stats;
        public Capability[] Capabilities => capabilities;
        public PassiveTrigger[] Triggers => triggers;
        public StackSettings Stacks => stacks;
        public NearbyEnemiesBonus NearbyEnemies => nearbyEnemies;
        public AuraSettings Aura => aura;
        public StatConversion[] Conversions => conversions;
    }
}
