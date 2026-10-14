using System;
using Features.Economy;
using Features.Stats;
using UnityEngine;

namespace Features.Meta {
    /// <summary>What a meta node does at the start of each run, per level bought (design doc §9.7).</summary>
    public enum MetaEffectKind {
        /// <summary>A stat bonus: Stat / Type / Value × level.</summary>
        Stat,
        /// <summary>+Value × level gold at the start of the run.</summary>
        StartingGold,
        /// <summary>+Value × level rerolls on every card of every augment draft.</summary>
        AugmentRerolls,
        /// <summary>+Value × level stage lives.</summary>
        ExtraLives,
        /// <summary>A random augment of rarity (level − 1, capped: Silver, Gold, Prismatic) at the start of the run.</summary>
        StartingAugment,
    }

    [Serializable]
    public struct MetaEffect {
        public MetaEffectKind Kind;
        public StatType Stat;
        public ModifierType Type;
        [Tooltip("Per level.")]
        public float Value;
    }

    /// <summary>
    /// One node of a meta tree (design doc §9.7): levels bought with the persistent currency, each
    /// one stronger (effects × level). Needs its prerequisites at level 1 at least. Read-only data;
    /// the levels owned live in MetaProgress.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Meta/Node", fileName = "Meta_")]
    public class MetaNodeDefinition : ScriptableObject {
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite icon;
        [Tooltip("Cost of each level (its length is the max level).")]
        [SerializeField] private int[] costs = { 10 };
        [SerializeField] private MetaNodeDefinition[] prerequisites = Array.Empty<MetaNodeDefinition>();
        [SerializeField] private MetaEffect[] effects = Array.Empty<MetaEffect>();
        [Tooltip("Place in the tree screen: column (tier) and row.")]
        [SerializeField] private Vector2Int position;

        public string Id => name;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public int MaxLevel => Mathf.Max(1, costs.Length);
        public int Cost(int level) => costs.Length == 0 ? 0 : costs[Mathf.Clamp(level - 1, 0, costs.Length - 1)];
        public MetaNodeDefinition[] Prerequisites => prerequisites;
        public MetaEffect[] Effects => effects;
        public Vector2Int Position => position;

        /// <summary>What one level gives, for the UI.</summary>
        public string EffectText(int level) {
            var parts = new System.Collections.Generic.List<string>();
            foreach (MetaEffect e in effects) {
                float v = e.Value * Mathf.Max(1, level);
                parts.Add(e.Kind switch {
                    MetaEffectKind.Stat => StatLabels.Format(new StatBonus(e.Stat, e.Type, v)),
                    MetaEffectKind.StartingGold => $"+{v:0} or au départ",
                    MetaEffectKind.AugmentRerolls => $"+{v:0} relance(s) par carte d'augment",
                    MetaEffectKind.ExtraLives => $"+{v:0} vie(s) de stage",
                    _ => $"Augment {StatLabels.Name((RarityTier)Mathf.Clamp(level - 1, 0, 2))} au départ",
                });
            }
            return string.Join("\n", parts);
        }
    }
}
