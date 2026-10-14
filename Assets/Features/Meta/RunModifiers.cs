using System.Collections.Generic;
using Features.Characters;
using Features.Economy;
using Features.Stats;
using UnityEngine;

namespace Features.Meta {
    /// <summary>
    /// What the meta tree of the run's champion gives this run (design doc §9.7, RunModifiers): read
    /// once from MetaProgress when the game scope starts. RunDirector reads the extra lives, the
    /// augment controller the rerolls; MetaApplier applies the rest to the spawned player.
    /// Game scope.
    /// </summary>
    public sealed class RunModifiers {
        private readonly List<(MetaNodeDefinition node, MetaEffect effect, int level)> _effects = new();

        public int ExtraLives { get; }
        public int StartingGold { get; }
        public int AugmentRerolls { get; }
        /// <summary>Rarity of the free starting augment, null without one.</summary>
        public RarityTier? StartingAugment { get; }

        public RunModifiers(MetaProgress progress, CharacterDefinition champion) {
            MetaTreeDefinition tree = progress?.Settings != null ? progress.Settings.TreeOf(champion) : null;
            if (tree == null) return;
            float lives = 0f, gold = 0f, rerolls = 0f;
            int augmentLevel = 0;
            foreach (MetaNodeDefinition node in tree.Nodes) {
                int level = progress.Level(node);
                if (node == null || level <= 0) continue;
                foreach (MetaEffect e in node.Effects) {
                    switch (e.Kind) {
                        case MetaEffectKind.ExtraLives: lives += e.Value * level; break;
                        case MetaEffectKind.StartingGold: gold += e.Value * level; break;
                        case MetaEffectKind.AugmentRerolls: rerolls += e.Value * level; break;
                        case MetaEffectKind.StartingAugment: augmentLevel = Mathf.Max(augmentLevel, level); break;
                        default: _effects.Add((node, e, level)); break;
                    }
                }
            }
            ExtraLives = Mathf.RoundToInt(lives);
            StartingGold = Mathf.RoundToInt(gold);
            AugmentRerolls = Mathf.RoundToInt(rerolls);
            if (augmentLevel > 0) StartingAugment = (RarityTier)Mathf.Clamp(augmentLevel - 1, 0, 2);
        }

        /// <summary>The stat bonuses (Source = the node), on the player's stats.</summary>
        public void ApplyStats(StatController stats) {
            foreach ((MetaNodeDefinition node, MetaEffect e, int level) in _effects)
                if (e.Kind == MetaEffectKind.Stat) stats.Get(e.Stat)?.AddModifier(new StatModifier(e.Value * level, e.Type, node));
        }
    }
}
