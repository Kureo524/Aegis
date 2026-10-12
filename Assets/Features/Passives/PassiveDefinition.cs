using System;
using Features.Combat;
using Features.Economy;
using Features.Stats;
using UnityEngine;

namespace Features.Passives {
    /// <summary>What a passive reacts to (CharacterEvents, design doc §8.1).</summary>
    public enum PassiveEvent { BasicAttackHit, SpellCast, Kill, DamageTaken }

    /// <summary>What a trigger does.</summary>
    public enum PassiveEffectKind {
        /// <summary>Stats for Duration seconds (TimedModifiers).</summary>
        TimedStats,
        /// <summary>Extra damage on the target of the hit: Damage + DamageRatio × ScalingStat.</summary>
        BonusDamage,
        /// <summary>Drops a loot on the ground where it happened (design doc §9.5).</summary>
        DropLoot,
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
        [Tooltip("SpellCast: only spells of at least this many elements (0 = any).")]
        [Min(0)] public int MinSpellStacks;
        public PassiveEffectKind Effect;
        [Header("TimedStats")]
        public StatBonus[] Stats;
        [Min(0f)] public float Duration;
        [Header("BonusDamage")]
        public float Damage;
        public StatType ScalingStat;
        public float DamageRatio;
        public DamageType DamageType;
        [Header("DropLoot")]
        public LootKind Loot;
    }

    /// <summary>Where stacks come from (design doc §8.1).</summary>
    public enum StackSource { None, PerBasicAttack, PerSpellCast, PerKill }

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

        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public StatBonus[] Stats => stats;
        public Capability[] Capabilities => capabilities;
        public PassiveTrigger[] Triggers => triggers;
        public StackSettings Stacks => stacks;
        public NearbyEnemiesBonus NearbyEnemies => nearbyEnemies;
    }
}
