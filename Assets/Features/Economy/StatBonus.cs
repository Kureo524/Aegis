using System;
using System.Collections.Generic;
using Features.Stats;
using UnityEngine;

namespace Features.Economy {
    /// <summary>Rarity of stat anvil choices and augments (design doc §9.3, §9.4).</summary>
    public enum RarityTier { Silver, Gold, Prismatic }

    /// <summary>Item tiers: legendary in the full shop, prismatic only through anvils (§9.3).</summary>
    public enum ItemTier { Legendary, Prismatic }

    /// <summary>One stat bonus of an item, an elixir or an anvil choice (StatModifierData of the doc).</summary>
    [Serializable]
    public struct StatBonus {
        public StatType Stat;
        public ModifierType Type;
        public float Value;

        public StatBonus(StatType stat, ModifierType type, float value) {
            Stat = stat;
            Type = type;
            Value = value;
        }

        public StatModifier ToModifier(object source) => new(Value, Type, source);
    }

    /// <summary>French labels of the stats and bonuses, for the shop and the anvils.</summary>
    public static class StatLabels {
        private static readonly Dictionary<StatType, string> Names = new() {
            { StatType.MaxHealth, "PV" }, { StatType.HealthRegen, "régén. PV" },
            { StatType.MaxMana, "mana" }, { StatType.ManaRegen, "régén. mana" },
            { StatType.PhysicalAttack, "attaque physique" }, { StatType.MagicAttack, "puissance magique" },
            { StatType.AttackSpeed, "vitesse d'attaque" }, { StatType.AttackRange, "portée" },
            { StatType.CritChance, "chances de critique" }, { StatType.CritDamage, "dégâts critiques" },
            { StatType.ArmorPenFlat, "pénétration d'armure" }, { StatType.ArmorPenPercent, "pénétration d'armure" },
            { StatType.MagicPenFlat, "pénétration magique" }, { StatType.MagicPenPercent, "pénétration magique" },
            { StatType.Armor, "armure" }, { StatType.MagicResist, "résistance magique" },
            { StatType.Tenacity, "ténacité" }, { StatType.SlowResist, "résistance aux ralentissements" },
            { StatType.MoveSpeed, "vitesse de déplacement" }, { StatType.Omnivamp, "omnivampirisme" },
            { StatType.Lifesteal, "vol de vie" }, { StatType.HealShieldPower, "puissance des soins" },
            { StatType.AbilityHaste, "accélération" }, { StatType.ManaCostReduction, "coût en mana" },
        };

        // Stats stored as a 0..1 share and shown as a percentage
        private static readonly HashSet<StatType> Shares = new() {
            StatType.CritChance, StatType.ArmorPenPercent, StatType.MagicPenPercent, StatType.Tenacity,
            StatType.SlowResist, StatType.Omnivamp, StatType.Lifesteal, StatType.ManaCostReduction,
        };

        public static string Name(StatType stat) => Names.TryGetValue(stat, out string n) ? n : stat.ToString();

        public static string Format(StatBonus bonus) {
            string name = Name(bonus.Stat);
            if (bonus.Type != ModifierType.Flat || Shares.Contains(bonus.Stat)) {
                float percent = bonus.Value * 100f;
                string sign = bonus.Stat == StatType.ManaCostReduction ? "-" : (percent >= 0f ? "+" : "");
                return $"{sign}{Mathf.Abs(percent):0.#} % {name}";
            }
            return $"{(bonus.Value >= 0f ? "+" : "")}{bonus.Value:0.#} {name}";
        }

        public static string Format(IReadOnlyList<StatBonus> bonuses) {
            var lines = new string[bonuses.Count];
            for (int i = 0; i < bonuses.Count; i++) lines[i] = Format(bonuses[i]);
            return string.Join("\n", lines);
        }

        public static string Name(RarityTier rarity) => rarity switch {
            RarityTier.Silver => "Argent",
            RarityTier.Gold => "Or",
            _ => "Prismatique",
        };

        public static Color Color(RarityTier rarity) => rarity switch {
            RarityTier.Silver => new Color(0.78f, 0.82f, 0.88f),
            RarityTier.Gold => new Color(1f, 0.8f, 0.3f),
            _ => new Color(0.85f, 0.55f, 1f),
        };

        public static Color Color(ItemTier tier) => tier == ItemTier.Legendary ? new Color(1f, 0.6f, 0.25f) : new Color(0.85f, 0.55f, 1f);
    }
}
