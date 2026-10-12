using System;
using Features.Characters;
using Features.Spells;
using UnityEngine;

namespace Features.Enemies {
    /// <summary>What a pattern step does (design doc §9.6).</summary>
    public enum PatternStepKind {
        /// <summary>The usual AI (chase, basic attacks) for Duration seconds.</summary>
        Wait,
        /// <summary>Casts Spell at the player (or on itself with AtSelf), telegraphed during its cast time.</summary>
        CastSpell,
        /// <summary>Telegraphed straight charge towards the player: ChargeDistance at ChargeSpeed, ChargeDamage on contact.</summary>
        Charge,
        /// <summary>Calls Count enemies (Summon) around itself.</summary>
        Summon,
    }

    [Serializable]
    public struct PatternStep {
        public PatternStepKind Kind;
        [Tooltip("Wait: seconds of normal AI. Others: recovery after the step.")]
        [Min(0f)] public float Duration;
        [Tooltip("Weighted random phases: chance weight of this step.")]
        [Min(0f)] public float Weight;
        [Header("CastSpell")]
        public SpellDefinition Spell;
        [Tooltip("Ground spell centered on the boss instead of the player (nova).")]
        public bool AtSelf;
        [Header("Charge")]
        [Min(0f)] public float ChargeDistance;
        [Min(0.1f)] public float ChargeSpeed;
        [Min(0f)] public float ChargeDamage;
        [Tooltip("Seconds the charge line is shown before the dash.")]
        [Min(0f)] public float Telegraph;
        [Header("Summon")]
        public EnemyDefinition Summon;
        public Character SummonPrefab;
        [Min(0)] public int Count;
    }

    public enum PatternOrder { Sequence, WeightedRandom }

    /// <summary>A phase: starts when the boss falls to HealthThreshold (share of max HP).</summary>
    [Serializable]
    public struct PatternPhase {
        [Range(0f, 1f)] public float HealthThreshold;
        public PatternOrder Order;
        public PatternStep[] Steps;
    }

    /// <summary>
    /// A boss pattern (design doc §9.6): phases by HP thresholds (first one at 1), each a list of
    /// steps played in sequence or by weighted draw. Played by PatternRunner.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Pattern Definition", fileName = "Pattern_")]
    public class PatternDefinition : ScriptableObject {
        [SerializeField] private PatternPhase[] phases = Array.Empty<PatternPhase>();
        public PatternPhase[] Phases => phases;
    }
}
