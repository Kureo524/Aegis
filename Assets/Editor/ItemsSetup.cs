#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Features.Combat;
using Features.Economy;
using Features.Enemies;
using Features.Passives;
using Features.Stats;
using UnityEditor;
using UnityEngine;
using P = EditorTools.PassiveSetup.P;

namespace EditorTools {
    /// <summary>
    /// The items (design doc §9.3), called by <see cref="ProjectSetup"/>: legendary items (marksman,
    /// mage, fighter, tank), boots (one pair at a time) and prismatic items with adaptive force.
    /// Each item = stats + its own passive, built from the generic passive bricks. Created once;
    /// the shop catalog's item list is set to exactly these items (older test items are dropped
    /// from it), the elixirs get their names and Grokk carries two of the items.
    /// </summary>
    public static class ItemsSetup {
        private const string Dir = "Assets/Data/Economy/Items";
        private const string IconsDir = "Assets/GraphicAssets/Sprites/Items";
        private const string Boots = ItemDefinition.BootsGroup, Spellblade = "Arme runique", Hydra = "Fendoir", Lifeline = "Dernier rempart";

        private static StatBonus B(StatType s, float v, ModifierType t = ModifierType.Flat) => new(s, t, v);
        private static StatBonus AS(float v) => B(StatType.AttackSpeed, v, ModifierType.PercentAdd);
        private static StatBonus MS(float v) => B(StatType.MoveSpeed, v, ModifierType.PercentAdd);
        private static readonly Color Gold = new(1f, 0.82f, 0.35f), Arcane = new(0.6f, 0.75f, 1f), Storm = new(0.75f, 0.9f, 1f), Fire = new(1f, 0.55f, 0.25f);

        // ---- Passive helpers (the generic bricks of design doc §8.1) ----
        private static PassiveTrigger OnHit(float damage, StatType stat, float ratio, DamageType type, int everyN = 1, float cooldown = 0f) =>
            new() { On = PassiveEvent.BasicAttackHit, EveryN = everyN, Chance = 1f, Cooldown = cooldown, Effect = PassiveEffectKind.BonusDamage,
                    Damage = damage, ScalingStat = stat, DamageRatio = ratio, DamageType = type };
        private static PassiveTrigger Bolts(PassiveEvent on, int count, float radius, float damage, StatType stat, float ratio, DamageType type,
                                            Color color, int everyN = 1, float cooldown = 0f) =>
            new() { On = on, EveryN = everyN, Chance = 1f, Cooldown = cooldown, Effect = PassiveEffectKind.Bolts, Count = count, Radius = radius,
                    Damage = damage, ScalingStat = stat, DamageRatio = ratio, DamageType = type, BoltColor = color };
        // One more permanent stack of the passive (growing items)
        private static PassiveTrigger Grow(PassiveEvent on, float cooldown = 0f, bool eliteOnly = false) =>
            new() { On = on, EveryN = 1, Chance = 1f, Cooldown = cooldown, EliteOnly = eliteOnly, Effect = PassiveEffectKind.AddStack };
        private static PassiveTrigger Status(PassiveEvent on, StatusApplication status, int everyN = 1, float cooldown = 0f) =>
            new() { On = on, EveryN = everyN, Chance = 1f, Cooldown = cooldown, Effect = PassiveEffectKind.ApplyStatus, Status = status };
        private static PassiveTrigger Timed(PassiveEvent on, float duration, float cooldown, params StatBonus[] stats) =>
            new() { On = on, EveryN = 1, Chance = 1f, Cooldown = cooldown, Effect = PassiveEffectKind.TimedStats, Duration = duration, Stats = stats };
        private static PassiveTrigger LowHealthShield(float threshold, float amount, StatType stat, float ratio, float duration, float cooldown) =>
            new() { On = PassiveEvent.DamageTaken, EveryN = 1, Chance = 1f, HealthBelow = threshold, Cooldown = cooldown, Effect = PassiveEffectKind.Shield,
                    Amount = amount, AmountStat = stat, AmountRatio = ratio, Duration = duration };
        // Spellblade: a spell charges the next basic attack (10 s), 1.5 s cooldown
        private static PassiveTrigger[] SpellbladeTriggers(float damage, StatType stat, float ratio, DamageType type) {
            var hit = OnHit(damage, stat, ratio, type, cooldown: 1.5f);
            hit.ConsumesCharge = true;
            return new[] { new PassiveTrigger { On = PassiveEvent.SpellCast, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.Charge, Duration = 10f }, hit };
        }
        private static StatusApplication Slow(float power, float duration) => new() { Kind = StatusKind.Slow, Power = power, Duration = duration };
        private static StatusApplication Stun(float duration) => new() { Kind = StatusKind.Stun, Duration = duration };
        private static StatusApplication Burn(float dps, float targetMaxHp, float duration, int stacks = 1) =>
            new() { Kind = StatusKind.Burn, Power = dps, TargetMaxHealthRatio = targetMaxHp, Duration = duration, MaxStacks = stacks };

        private sealed class Item {
            public string Id, Name, Group = "";
            public ItemTier Tier = ItemTier.Legendary;
            public int Cost;
            public StatBonus[] Stats;
            public P Passive;
        }

        private static P Pas(string id, string name, string description) => new() { Id = "Item_" + id, Name = name, Description = description };

        private static readonly Item[] Items = {
            // ---- Marksman ----
            new() { Id = "SunsteelBlade", Name = "Lame d'aciersoleil", Cost = 345, Stats = new[] { B(StatType.PhysicalAttack, 65), B(StatType.CritChance, 0.25f) },
                Passive = Pas("SunsteelBlade", "Tranchant parfait", "+40 % dégâts des coups critiques.").With(stats: new[] { B(StatType.CritDamage, 0.4f) }) },
            new() { Id = "TideBreaker", Name = "Brise-marée", Cost = 310, Stats = new[] { B(StatType.PhysicalAttack, 45), AS(0.4f), MS(0.04f) },
                Passive = Pas("TideBreaker", "Troisième vague", "Une attaque sur trois inflige 140 (+40 % AD) dégâts physiques en plus.")
                    .With(OnHit(140f, StatType.PhysicalAttack, 0.4f, DamageType.Physical, everyN: 3)) },
            new() { Id = "GaleBow", Name = "Arc des bourrasques", Cost = 265, Stats = new[] { AS(0.4f), B(StatType.CritChance, 0.25f), MS(0.04f) },
                Passive = Pas("GaleBow", "Volée jumelle", "Les attaques tirent 2 traits sur des ennemis proches : 55 % AD dégâts physiques.")
                    .With(Bolts(PassiveEvent.BasicAttackHit, 2, 5f, 0f, StatType.PhysicalAttack, 0.55f, DamageType.Physical, Storm)) },
            new() { Id = "StormNeedle", Name = "Aiguille d'orage", Cost = 290, Stats = new[] { B(StatType.PhysicalAttack, 45), AS(0.3f), MS(0.04f) },
                Passive = Pas("StormNeedle", "Décharge", "Une attaque sur trois foudroie 5 ennemis proches : 60 (+15 % AP) dégâts magiques.")
                    .With(Bolts(PassiveEvent.BasicAttackHit, 5, 6f, 60f, StatType.MagicAttack, 0.15f, DamageType.Magic, Storm, everyN: 3)) },
            new() { Id = "MistStep", Name = "Pas-de-brume", Cost = 265, Stats = new[] { AS(0.6f), B(StatType.CritChance, 0.25f), MS(0.08f) },
                Passive = Pas("MistStep", "Esquive fluide", "Les attaques donnent +10 % vitesse de déplacement pendant 2 s.")
                    .With(Timed(PassiveEvent.BasicAttackHit, 2f, 0f, MS(0.1f))) },
            new() { Id = "CrimsonChalice", Name = "Calice écarlate", Cost = 340, Stats = new[] { B(StatType.PhysicalAttack, 80), B(StatType.Lifesteal, 0.15f) },
                Passive = Pas("CrimsonChalice", "Sang figé", "Touché sous 35 % de PV : bouclier de 165 (+10 % PV max) pendant 4 s. Recharge 30 s.")
                    .With(LowHealthShield(0.35f, 165f, StatType.MaxHealth, 0.1f, 4f, 30f)), Group = Lifeline },
            new() { Id = "GiantsBane", Name = "Fléau des colosses", Cost = 300, Stats = new[] { B(StatType.PhysicalAttack, 35), B(StatType.CritChance, 0.25f), B(StatType.ArmorPenPercent, 0.35f) },
                Passive = Pas("GiantsBane", "Abattre les géants", "Les attaques infligent 2 % des PV max de la cible en dégâts physiques.")
                    .With(new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.BonusDamage,
                                               TargetMaxHealthRatio = 0.02f, DamageType = DamageType.Physical }) },
            new() { Id = "QuicksilverEdge", Name = "Fil de vif-argent", Cost = 260, Stats = new[] { AS(0.4f), B(StatType.CritChance, 0.25f), MS(0.04f) },
                Passive = Pas("QuicksilverEdge", "Cadence", "Les attaques réduisent les délais de récupération de 0,5 s.")
                    .With(new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.ReduceCooldowns, Amount = 0.5f }) },
            new() { Id = "RuneSickle", Name = "Faucille runique", Cost = 290, Group = Spellblade, Stats = new[] { B(StatType.PhysicalAttack, 60), B(StatType.AbilityHaste, 15), B(StatType.CritChance, 0.25f) },
                Passive = Pas("RuneSickle", "Arme éveillée", "Après un sort, la prochaine attaque inflige 100 % AD dégâts physiques en plus (recharge 1,5 s).")
                    .With(SpellbladeTriggers(0f, StatType.PhysicalAttack, 1f, DamageType.Physical)) },
            new() { Id = "GraveFang", Name = "Croc du tombeau", Cost = 320, Stats = new[] { B(StatType.PhysicalAttack, 40), AS(0.25f), B(StatType.Lifesteal, 0.1f) },
                Passive = Pas("GraveFang", "Saignée", "Les attaques infligent 6 % des PV actuels de la cible en dégâts physiques.")
                    .With(new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.BonusDamage,
                                               TargetCurrentHealthRatio = 0.06f, DamageType = DamageType.Physical }) },
            new() { Id = "HexEdge", Name = "Lame maudite", Cost = 280, Stats = new[] { AS(0.5f), B(StatType.MagicResist, 45), B(StatType.Tenacity, 0.2f) },
                Passive = Pas("HexEdge", "Morsure arcanique", "Les attaques infligent 45 dégâts magiques en plus.")
                    .With(OnHit(45f, StatType.MagicAttack, 0f, DamageType.Magic)) },
            new() { Id = "WrathBlade", Name = "Lame de colère", Cost = 300, Stats = new[] { B(StatType.PhysicalAttack, 30), B(StatType.MagicAttack, 30), AS(0.25f) },
                Passive = Pas("WrathBlade", "Emballement", "Les attaques infligent 30 dégâts magiques et donnent +8 % vitesse d'attaque (4 fois, 3 s).")
                    .With(OnHit(30f, StatType.MagicAttack, 0f, DamageType.Magic))
                    .WithStacks(StackSource.PerBasicAttack, ResetPolicy.AfterDuration, 4, 3f, AS(0.08f)) },
            new() { Id = "RepeatingCrossbow", Name = "Arbalète à répétition", Cost = 265, Stats = new[] { AS(0.35f), B(StatType.CritChance, 0.25f), MS(0.04f) },
                Passive = Pas("RepeatingCrossbow", "Tir appuyé", "Une attaque sur trois inflige 120 dégâts magiques en plus.")
                    .With(OnHit(120f, StatType.MagicAttack, 0f, DamageType.Magic, everyN: 3)) },
            new() { Id = "TrophyCrown", Name = "Couronne de trophées", Cost = 295, Stats = new[] { B(StatType.PhysicalAttack, 60), B(StatType.AbilityHaste, 10) },
                Passive = Pas("TrophyCrown", "Gloire", "Chaque kill donne +1 AD pour la partie (30 max).")
                    .WithStacks(StackSource.PerKill, ResetPolicy.Never, 30, 0f, B(StatType.PhysicalAttack, 1)) },
            new() { Id = "WraithKnife", Name = "Coutelas du revenant", Cost = 280, Stats = new[] { B(StatType.PhysicalAttack, 55), B(StatType.ArmorPenFlat, 18), B(StatType.AbilityHaste, 15) },
                Passive = Pas("WraithKnife", "Traque", "Chaque kill donne +20 % vitesse de déplacement pendant 2 s.")
                    .With(Timed(PassiveEvent.Kill, 2f, 0f, MS(0.2f))) },
            new() { Id = "FrostbiteAxe", Name = "Hache mord-givre", Cost = 320, Stats = new[] { B(StatType.PhysicalAttack, 45), B(StatType.ArmorPenPercent, 0.3f), B(StatType.AbilityHaste, 15) },
                Passive = Pas("FrostbiteAxe", "Morsure du froid", "Les sorts ralentissent de 30 % pendant 1 s.")
                    .With(Status(PassiveEvent.SpellHit, Slow(0.3f, 1f))) },

            // ---- Mage ----
            new() { Id = "ArchmageHood", Name = "Capuche de l'archimage", Cost = 360, Stats = new[] { B(StatType.MagicAttack, 130) },
                Passive = Pas("ArchmageHood", "Grand œuvre", "+30 % puissance magique.").With(stats: new[] { B(StatType.MagicAttack, 0.3f, ModifierType.PercentMult) }) },
            new() { Id = "EmberChoir", Name = "Chœur de braises", Cost = 290, Stats = new[] { B(StatType.MagicAttack, 100), B(StatType.MaxMana, 600), B(StatType.AbilityHaste, 10) },
                Passive = Pas("EmberChoir", "Étincelles", "Un sort qui touche tire 3 traits sur des ennemis proches : 60 (+10 % AP) dégâts magiques. Recharge 3 s.")
                    .With(Bolts(PassiveEvent.SpellHit, 3, 6f, 60f, StatType.MagicAttack, 0.1f, DamageType.Magic, Arcane, cooldown: 3f)) },
            new() { Id = "WitchfireMask", Name = "Masque feu-follet", Cost = 300, Stats = new[] { B(StatType.MagicAttack, 90), B(StatType.MaxHealth, 300) },
                Passive = Pas("WitchfireMask", "Tourment", "Les sorts brûlent : 1 % des PV max de la cible par seconde pendant 3 s.")
                    .With(Status(PassiveEvent.SpellHit, Burn(5f, 0.01f, 3f))) },
            new() { Id = "SplitterRod", Name = "Sceptre fendeur", Cost = 300, Stats = new[] { B(StatType.MagicAttack, 95) },
                Passive = Pas("SplitterRod", "Fissure", "+40 % pénétration magique.").With(stats: new[] { B(StatType.MagicPenPercent, 0.4f) }) },
            new() { Id = "BoneLantern", Name = "Lanterne d'os", Cost = 290, Group = Spellblade, Stats = new[] { B(StatType.MagicAttack, 100), MS(0.04f), B(StatType.AbilityHaste, 10) },
                Passive = Pas("BoneLantern", "Arme éveillée", "Après un sort, la prochaine attaque inflige 50 (+60 % AP) dégâts magiques en plus (recharge 1,5 s).")
                    .With(SpellbladeTriggers(50f, StatType.MagicAttack, 0.6f, DamageType.Magic)) },
            new() { Id = "RimeScepter", Name = "Sceptre de givre", Cost = 260, Stats = new[] { B(StatType.MagicAttack, 65), B(StatType.MaxHealth, 400) },
                Passive = Pas("RimeScepter", "Givre pénétrant", "Les sorts ralentissent de 30 % pendant 1 s.")
                    .With(Status(PassiveEvent.SpellHit, Slow(0.3f, 1f))) },
            new() { Id = "SerpentFang", Name = "Croc du serpent", Cost = 290, Stats = new[] { AS(0.5f), B(StatType.MagicAttack, 80) },
                Passive = Pas("SerpentFang", "Venin", "Les attaques infligent 15 (+20 % AP) dégâts magiques en plus.")
                    .With(OnHit(15f, StatType.MagicAttack, 0.2f, DamageType.Magic)) },
            new() { Id = "SageStaff", Name = "Bâton du sage", Cost = 290, Stats = new[] { B(StatType.MagicAttack, 70), B(StatType.MaxMana, 600), B(StatType.AbilityHaste, 25) },
                Passive = Pas("SageStaff", "Savoir", "Gagne de la puissance magique égale à 1 % du mana bonus.")
                    .WithConversion(StatType.MaxMana, StatType.MagicAttack, 0.01f) },
            new() { Id = "GloomTorch", Name = "Torche lugubre", Cost = 320, Stats = new[] { B(StatType.MagicAttack, 110), B(StatType.MagicPenFlat, 15) },
                Passive = Pas("GloomTorch", "Flamme sombre", "Les sorts brûlent : 25 dégâts magiques par seconde pendant 2 s.")
                    .With(Status(PassiveEvent.SpellHit, Burn(25f, 0f, 2f))) },
            new() { Id = "CometHeart", Name = "Cœur de comète", Cost = 300, Stats = new[] { B(StatType.MagicAttack, 70), B(StatType.MaxHealth, 350), B(StatType.AbilityHaste, 25), MS(0.04f) },
                Passive = Pas("CometHeart", "Élan", "Un sort qui touche donne +20 % vitesse de déplacement pendant 2 s.")
                    .With(Timed(PassiveEvent.SpellHit, 2f, 0f, MS(0.2f))) },
            new() { Id = "CinderLantern", Name = "Lanterne de cendres", Cost = 280, Stats = new[] { B(StatType.MagicAttack, 80), B(StatType.MaxMana, 600), B(StatType.AbilityHaste, 20) },
                Passive = Pas("CinderLantern", "Feu couvant", "Les sorts brûlent : 15 dégâts magiques par seconde pendant 3 s, cumulable 3 fois.")
                    .With(Status(PassiveEvent.SpellHit, Burn(15f, 0f, 3f, 3))) },
            new() { Id = "AbyssalLens", Name = "Lentille abyssale", Cost = 310, Stats = new[] { B(StatType.MagicAttack, 70), B(StatType.MaxHealth, 350), B(StatType.AbilityHaste, 15), B(StatType.Omnivamp, 0.08f) },
                Passive = Pas("AbyssalLens", "Résonance", "Un sort qui touche donne +6 puissance magique (5 fois, 4 s).")
                    .WithStacks(StackSource.PerSpellHit, ResetPolicy.AfterDuration, 5, 4f, B(StatType.MagicAttack, 6)) },

            // ---- Fighter ----
            new() { Id = "TriadSigil", Name = "Sceau de la triade", Cost = 333, Group = Spellblade, Stats = new[] { B(StatType.PhysicalAttack, 36), AS(0.3f), B(StatType.AbilityHaste, 15), B(StatType.MaxHealth, 333) },
                Passive = Pas("TriadSigil", "Arme éveillée", "Après un sort, la prochaine attaque inflige 200 % AD dégâts physiques en plus (recharge 1,5 s).")
                    .With(SpellbladeTriggers(0f, StatType.PhysicalAttack, 2f, DamageType.Physical)) },
            new() { Id = "IronCleaver", Name = "Fendoir de fer", Cost = 300, Stats = new[] { B(StatType.PhysicalAttack, 40), B(StatType.MaxHealth, 400), B(StatType.AbilityHaste, 20) },
                Passive = Pas("IronCleaver", "Élan sanglant", "Les attaques donnent +20 % vitesse de déplacement pendant 2 s.")
                    .With(Timed(PassiveEvent.BasicAttackHit, 2f, 0f, MS(0.2f))) },
            new() { Id = "DawnHammer", Name = "Marteau de l'aube", Cost = 310, Stats = new[] { B(StatType.PhysicalAttack, 40), B(StatType.MaxHealth, 400), B(StatType.AbilityHaste, 10) },
                Passive = Pas("DawnHammer", "Frappe radieuse", "Toutes les 6 s, une attaque inflige 75 % AD en plus et rend 6 % des PV max.")
                    .With(OnHit(0f, StatType.PhysicalAttack, 0.75f, DamageType.Physical, cooldown: 6f),
                          new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 1, Chance = 1f, Cooldown = 6f, Effect = PassiveEffectKind.Heal,
                                               AmountStat = StatType.MaxHealth, AmountRatio = 0.06f }) },
            new() { Id = "ThunderAxe", Name = "Hache-tonnerre", Cost = 330, Group = Hydra, Stats = new[] { B(StatType.PhysicalAttack, 40), B(StatType.MaxHealth, 600) },
                Passive = Pas("ThunderAxe", "Onde de choc", "Les attaques infligent 5 (+1,5 % PV max) dégâts physiques aux ennemis autour de la cible.")
                    .With(new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.Splash, Radius = 2.5f,
                                               Damage = 5f, ScalingStat = StatType.MaxHealth, DamageRatio = 0.015f, DamageType = DamageType.Physical }) },
            new() { Id = "HungryAxe", Name = "Hache affamée", Cost = 330, Group = Hydra, Stats = new[] { B(StatType.PhysicalAttack, 65), B(StatType.AbilityHaste, 15), B(StatType.Omnivamp, 0.1f) },
                Passive = Pas("HungryAxe", "Fauchage", "Les attaques infligent 40 % AD dégâts physiques aux ennemis autour de la cible.")
                    .With(new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.Splash, Radius = 2.5f,
                                               ScalingStat = StatType.PhysicalAttack, DamageRatio = 0.4f, DamageType = DamageType.Physical }) },
            new() { Id = "OathGauntlet", Name = "Gantelet du serment", Cost = 320, Group = Lifeline, Stats = new[] { B(StatType.MaxHealth, 400), B(StatType.PhysicalAttack, 0.45f, ModifierType.PercentAdd) },
                Passive = Pas("OathGauntlet", "Sursis", "Touché sous 30 % de PV : bouclier de 25 % des PV max pendant 4 s. Recharge 90 s.")
                    .With(LowHealthShield(0.3f, 0f, StatType.MaxHealth, 0.25f, 4f, 90f)) },
            new() { Id = "WarDrum", Name = "Tambour de guerre", Cost = 330, Stats = new[] { B(StatType.PhysicalAttack, 60), B(StatType.Armor, 50), B(StatType.AbilityHaste, 15) },
                Passive = Pas("WarDrum", "Rage du vainqueur", "Chaque kill rend 10 % des PV max.")
                    .With(new PassiveTrigger { On = PassiveEvent.Kill, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.Heal, AmountStat = StatType.MaxHealth, AmountRatio = 0.1f }) },
            new() { Id = "WardingHelm", Name = "Heaume protecteur", Cost = 310, Group = Lifeline, Stats = new[] { B(StatType.PhysicalAttack, 60), B(StatType.MagicResist, 40), B(StatType.AbilityHaste, 15) },
                Passive = Pas("WardingHelm", "Sursis", "Touché sous 30 % de PV : bouclier de 200 (+15 % PV max) pendant 4 s. Recharge 90 s.")
                    .With(LowHealthShield(0.3f, 200f, StatType.MaxHealth, 0.15f, 4f, 90f)) },
            new() { Id = "MoonblightBlade", Name = "Lame lune-noire", Cost = 290, Stats = new[] { B(StatType.PhysicalAttack, 60), B(StatType.AbilityHaste, 15) },
                Passive = Pas("MoonblightBlade", "Croissant", "Toutes les 6 s, toucher avec un sort inflige 6 % des PV max de la cible et donne un bouclier de 80.")
                    .With(new PassiveTrigger { On = PassiveEvent.SpellHit, EveryN = 1, Chance = 1f, Cooldown = 6f, Effect = PassiveEffectKind.BonusDamage,
                                               TargetMaxHealthRatio = 0.06f, DamageType = DamageType.Physical },
                          new PassiveTrigger { On = PassiveEvent.SpellHit, EveryN = 1, Chance = 1f, Cooldown = 6f, Effect = PassiveEffectKind.Shield, Amount = 80f, Duration = 2f }) },
            new() { Id = "AscendantPike", Name = "Pique ascendante", Cost = 310, Stats = new[] { B(StatType.PhysicalAttack, 45), B(StatType.MaxHealth, 450), B(StatType.AbilityHaste, 20) },
                Passive = Pas("AscendantPike", "Ferveur", "Un sort qui touche donne +4 AD (4 fois, 6 s).")
                    .WithStacks(StackSource.PerSpellHit, ResetPolicy.AfterDuration, 4, 6f, B(StatType.PhysicalAttack, 4)) },

            // ---- Tank ----
            new() { Id = "EmberPlate", Name = "Plastron de braise", Cost = 270, Stats = new[] { B(StatType.MaxHealth, 350), B(StatType.Armor, 50) },
                Passive = Pas("EmberPlate", "Fournaise", "Brûle les ennemis à moins de 3 m : 20 (+1 % PV max) dégâts magiques par seconde.")
                    .WithAura(3f, 1f, 20f, StatType.MaxHealth, 0.01f, DamageType.Magic) },
            new() { Id = "BrambleMail", Name = "Cotte de ronces", Cost = 245, Stats = new[] { B(StatType.MaxHealth, 150), B(StatType.Armor, 75) },
                Passive = Pas("BrambleMail", "Ronces", "Quand un ennemi te touche, il subit 10 (+15 % armure) dégâts magiques.")
                    .With(new PassiveTrigger { On = PassiveEvent.DamageTaken, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.BonusDamage,
                                               Damage = 10f, ScalingStat = StatType.Armor, DamageRatio = 0.15f, DamageType = DamageType.Magic }) },
            new() { Id = "SealedBulwark", Name = "Rempart sigillé", Cost = 270, Stats = new[] { B(StatType.MaxHealth, 350), B(StatType.Armor, 75) },
                Passive = Pas("SealedBulwark", "Inébranlable", "Quand un ennemi te touche, il est ralenti de 25 % pendant 1 s.")
                    .With(Status(PassiveEvent.DamageTaken, Slow(0.25f, 1f))) },
            new() { Id = "TrollhideVest", Name = "Gilet en cuir de troll", Cost = 310, Stats = new[] { B(StatType.MaxHealth, 1000), B(StatType.HealthRegen, 2) },
                Passive = Pas("TrollhideVest", "Régénération", "Régénère 1 % des PV max par seconde.").WithConversion(StatType.MaxHealth, StatType.HealthRegen, 0.01f, bonusOnly: false) },
            new() { Id = "TitanHeart", Name = "Cœur de titan", Cost = 300, Stats = new[] { B(StatType.MaxHealth, 900), B(StatType.HealthRegen, 2) },
                Passive = Pas("TitanHeart", "Coup de masse", "Toutes les 8 s, une attaque inflige 70 (+6 % PV max) dégâts physiques en plus.")
                    .With(OnHit(70f, StatType.MaxHealth, 0.06f, DamageType.Physical, cooldown: 8f)) },
            new() { Id = "GlacierPlate", Name = "Plaque glaciaire", Cost = 250, Stats = new[] { B(StatType.Armor, 65), B(StatType.MaxMana, 400), B(StatType.AbilityHaste, 20) },
                Passive = Pas("GlacierPlate", "Aura gelée", "Quand un ennemi te touche, il est ralenti de 20 % pendant 1,5 s.")
                    .With(Status(PassiveEvent.DamageTaken, Slow(0.2f, 1.5f))) },
            new() { Id = "BarkCloak", Name = "Cape d'écorce", Cost = 280, Stats = new[] { B(StatType.MaxHealth, 400), B(StatType.MagicResist, 55), MS(0.05f) },
                Passive = Pas("BarkCloak", "Écorce vive", "Être touché donne +20 résistance magique et +5 % vitesse pendant 3 s.")
                    .With(Timed(PassiveEvent.DamageTaken, 3f, 1f, B(StatType.MagicResist, 20), MS(0.05f))) },
            new() { Id = "SoulLocket", Name = "Médaillon d'âme", Cost = 290, Stats = new[] { B(StatType.MaxHealth, 400), B(StatType.MagicResist, 50), B(StatType.AbilityHaste, 10), B(StatType.HealthRegen, 1.5f) },
                Passive = Pas("SoulLocket", "Ferveur vitale", "+25 % soins et boucliers reçus.").With(stats: new[] { B(StatType.HealShieldPower, 0.25f) }) },
            new() { Id = "ShiftingScale", Name = "Écaille changeante", Cost = 320, Stats = new[] { B(StatType.MaxHealth, 350), B(StatType.Armor, 45), B(StatType.MagicResist, 45) },
                Passive = Pas("ShiftingScale", "Adaptation", "+4 armure et +4 résistance magique par ennemi à moins de 5 m (6 max).")
                    .WithNearby(5f, 6, B(StatType.Armor, 4), B(StatType.MagicResist, 4)) },
            new() { Id = "MourningShroud", Name = "Linceul du deuil", Cost = 280, Stats = new[] { B(StatType.MaxHealth, 400), B(StatType.Armor, 55), B(StatType.AbilityHaste, 10) },
                Passive = Pas("MourningShroud", "Lamentation", "Toutes les 2 s, inflige 25 dégâts magiques aux ennemis à moins de 3,5 m et rend 6 PV par ennemi.")
                    .WithAura(3.5f, 2f, 25f, StatType.MaxHealth, 0f, DamageType.Magic, healPerEnemy: 6f) },
            new() { Id = "JuggernautPlate", Name = "Plaque du mastodonte", Cost = 290, Stats = new[] { B(StatType.MaxHealth, 300), B(StatType.Armor, 45), MS(0.05f) },
                Passive = Pas("JuggernautPlate", "Charge écrasante", "Une attaque sur quatre inflige 40 (+100 % AD de base) dégâts physiques et ralentit de 50 % pendant 1 s.")
                    .With(OnHit(40f, StatType.PhysicalAttack, 1f, DamageType.Physical, everyN: 4), Status(PassiveEvent.BasicAttackHit, Slow(0.5f, 1f), everyN: 4)) },

            // ---- Growing items: permanent stacks for the run (design doc §9.3) ----
            new() { Id = "ForgeHeart", Name = "Cœur de forge", Cost = 0, Stats = new[] { B(StatType.MaxHealth, 450), B(StatType.HealthRegen, 1.5f) },
                Passive = Pas("ForgeHeart", "Battement de forge", "Toutes les 6 s, une attaque inflige 50 (+6 % PV max) dégâts physiques en plus et donne +10 PV max pour le reste de la partie (sans limite).")
                    .With(OnHit(50f, StatType.MaxHealth, 0.06f, DamageType.Physical, cooldown: 6f), Grow(PassiveEvent.BasicAttackHit, cooldown: 6f))
                    .WithStacks(StackSource.None, ResetPolicy.Never, 0, 0f, B(StatType.MaxHealth, 10)) },
            new() { Id = "ConquerorTrophy", Name = "Trophée du conquérant", Cost = 0, Stats = new[] { B(StatType.PhysicalAttack, 50), B(StatType.AbilityHaste, 10) },
                Passive = Pas("ConquerorTrophy", "Gloire", "Chaque élite ou boss tué donne +4 attaque physique pour le reste de la partie (15 fois max).")
                    .With(Grow(PassiveEvent.Kill, eliteOnly: true))
                    .WithStacks(StackSource.None, ResetPolicy.Never, 15, 0f, B(StatType.PhysicalAttack, 4)) },
            new() { Id = "SoulGrimoire", Name = "Grimoire des âmes", Cost = 0, Stats = new[] { B(StatType.MagicAttack, 75), B(StatType.MaxMana, 250) },
                Passive = Pas("SoulGrimoire", "Moisson d'âmes", "Chaque élite ou boss tué donne +7 puissance magique pour le reste de la partie (15 fois max).")
                    .With(Grow(PassiveEvent.Kill, eliteOnly: true))
                    .WithStacks(StackSource.None, ResetPolicy.Never, 15, 0f, B(StatType.MagicAttack, 7)) },

            // ---- Boots (one pair) ----
            new() { Id = "DuelistGreaves", Name = "Grèves du bretteur", Cost = 110, Group = Boots, Stats = new[] { AS(0.35f), B(StatType.MoveSpeed, 0.7f) },
                Passive = Pas("DuelistGreaves", "Rythme", "+5 % vitesse d'attaque en plus.").With(stats: new[] { AS(0.05f) }) },
            new() { Id = "MageSlippers", Name = "Chaussons du mage", Cost = 110, Group = Boots, Stats = new[] { B(StatType.MagicPenFlat, 12), B(StatType.MoveSpeed, 0.7f) },
                Passive = Pas("MageSlippers", "Focalisation", "+3 pénétration magique en plus.").With(stats: new[] { B(StatType.MagicPenFlat, 3) }) },
            new() { Id = "IronBoots", Name = "Bottes ferrées", Cost = 120, Group = Boots, Stats = new[] { B(StatType.Armor, 25), B(StatType.MoveSpeed, 0.7f) },
                Passive = Pas("IronBoots", "Blindage", "+2 PV max par point d'armure bonus.").WithConversion(StatType.Armor, StatType.MaxHealth, 2f) },
            new() { Id = "NomadBoots", Name = "Bottes du nomade", Cost = 110, Group = Boots, Stats = new[] { B(StatType.MagicResist, 25), B(StatType.MoveSpeed, 0.7f), B(StatType.Tenacity, 0.3f) },
                Passive = Pas("NomadBoots", "Ténacité", "+30 % ténacité (comprise).").With(stats: new StatBonus[0]) },
            new() { Id = "SageSandals", Name = "Sandales du sage", Cost = 90, Group = Boots, Stats = new[] { B(StatType.AbilityHaste, 15), B(StatType.MoveSpeed, 0.7f) },
                Passive = Pas("SageSandals", "Clarté", "Les kills réduisent les délais de récupération de 1 s.")
                    .With(new PassiveTrigger { On = PassiveEvent.Kill, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.ReduceCooldowns, Amount = 1f }) },
            new() { Id = "CourierBoots", Name = "Bottes du coursier", Cost = 100, Group = Boots, Stats = new[] { B(StatType.MoveSpeed, 0.85f), B(StatType.SlowResist, 0.25f) },
                Passive = Pas("CourierBoots", "Célérité", "-25 % durée et force des ralentissements (comprise).").With(stats: new StatBonus[0]) },

            // ---- Prismatic (anvils only, adaptive force) ----
            new() { Id = "EmberCrown", Name = "Couronne des braises", Tier = ItemTier.Prismatic, Stats = new[] { B(StatType.AdaptiveForce, 40), B(StatType.MaxHealth, 300), B(StatType.Armor, 30), B(StatType.MagicResist, 30) },
                Passive = Pas("EmberCrown", "Règne ardent", "Brûle les ennemis à moins de 3,5 m : 30 (+2 % PV max) dégâts magiques par seconde.")
                    .WithAura(3.5f, 1f, 30f, StatType.MaxHealth, 0.02f, DamageType.Magic) },
            new() { Id = "ColossusBlade", Name = "Lame du colosse", Tier = ItemTier.Prismatic, Stats = new[] { B(StatType.AdaptiveForce, 30), B(StatType.MaxHealth, 500), B(StatType.CritChance, 0.2f) },
                Passive = Pas("ColossusBlade", "Poids du géant", "Gagne de l'AD égal à 1,5 % des PV bonus.").WithConversion(StatType.MaxHealth, StatType.PhysicalAttack, 0.015f) },
            new() { Id = "EchoPrism", Name = "Prisme à échos", Tier = ItemTier.Prismatic, Stats = new[] { B(StatType.AdaptiveForce, 60), B(StatType.MagicResist, 30), B(StatType.AbilityHaste, 20) },
                Passive = Pas("EchoPrism", "Écho", "Un sort qui touche tire 3 traits sur des ennemis proches : 80 (+20 % AP) dégâts magiques. Recharge 2 s.")
                    .With(Bolts(PassiveEvent.SpellHit, 3, 7f, 80f, StatType.MagicAttack, 0.2f, DamageType.Magic, Arcane, cooldown: 2f)) },
            new() { Id = "HourglassAmulet", Name = "Amulette-sablier", Tier = ItemTier.Prismatic, Stats = new[] { B(StatType.AdaptiveForce, 50), B(StatType.Armor, 50), B(StatType.AbilityHaste, 15) },
                Passive = Pas("HourglassAmulet", "Stase", "Touché sous 30 % de PV : bouclier de 300 (+30 % force adaptative) pendant 2,5 s. Recharge 60 s.")
                    .With(LowHealthShield(0.3f, 300f, StatType.AdaptiveForce, 0.3f, 2.5f, 60f)) },
            new() { Id = "GluttonFang", Name = "Croc du glouton", Tier = ItemTier.Prismatic, Stats = new[] { B(StatType.AdaptiveForce, 40), B(StatType.MaxHealth, 300) },
                Passive = Pas("GluttonFang", "Festin", "Chaque kill donne +1 force adaptative pour la partie (60 max).")
                    .WithStacks(StackSource.PerKill, ResetPolicy.Never, 60, 0f, B(StatType.AdaptiveForce, 1)) },
            new() { Id = "StormSpire", Name = "Flèche des tempêtes", Tier = ItemTier.Prismatic, Stats = new[] { B(StatType.AdaptiveForce, 30), AS(0.4f), MS(0.05f) },
                Passive = Pas("StormSpire", "Surtension", "Une attaque sur trois foudroie 4 ennemis proches : 90 (+30 % force adaptative) dégâts magiques.")
                    .With(Bolts(PassiveEvent.BasicAttackHit, 4, 7f, 90f, StatType.AdaptiveForce, 0.3f, DamageType.Magic, Storm, everyN: 3)) },
            new() { Id = "BloomBlade", Name = "Lame florissante", Tier = ItemTier.Prismatic, Stats = new[] { B(StatType.AdaptiveForce, 50), AS(0.25f), B(StatType.HealShieldPower, 0.2f) },
                Passive = Pas("BloomBlade", "Floraison", "Les attaques rendent 20 (+2 % PV max) PV. Recharge 1 s.")
                    .With(new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 1, Chance = 1f, Cooldown = 1f, Effect = PassiveEffectKind.Heal,
                                               Amount = 20f, AmountStat = StatType.MaxHealth, AmountRatio = 0.02f }) },
            new() { Id = "MirrorMask", Name = "Masque miroir", Tier = ItemTier.Prismatic, Stats = new[] { B(StatType.AdaptiveForce, 60), B(StatType.MaxHealth, 400), B(StatType.AbilityHaste, 20) },
                Passive = Pas("MirrorMask", "Malédiction", "Les sorts brûlent : 2 % des PV max de la cible par seconde pendant 3 s.")
                    .With(Status(PassiveEvent.SpellHit, Burn(10f, 0.02f, 3f))) },
            new() { Id = "BindingChain", Name = "Chaîne d'entrave", Tier = ItemTier.Prismatic, Stats = new[] { B(StatType.AdaptiveForce, 45), AS(0.3f), MS(0.05f) },
                Passive = Pas("BindingChain", "Entrave", "Les attaques ralentissent de 30 % pendant 1 s ; une sur cinq étourdit 0,6 s.")
                    .With(Status(PassiveEvent.BasicAttackHit, Slow(0.3f, 1f)), Status(PassiveEvent.BasicAttackHit, Stun(0.6f), everyN: 5)) },
            new() { Id = "MagmaShield", Name = "Bouclier de magma", Tier = ItemTier.Prismatic, Stats = new[] { B(StatType.AdaptiveForce, 20), B(StatType.MaxHealth, 600), B(StatType.Armor, 40), B(StatType.MagicResist, 40) },
                Passive = Pas("MagmaShield", "Magma", "Quand un ennemi te touche, il subit 30 (+3 % PV max) dégâts magiques.")
                    .With(new PassiveTrigger { On = PassiveEvent.DamageTaken, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.BonusDamage,
                                               Damage = 30f, ScalingStat = StatType.MaxHealth, DamageRatio = 0.03f, DamageType = DamageType.Magic }) },
        };

        private static readonly (string asset, string name, StatBonus[] stats)[] Elixirs = {
            ("Elixir_Strength", "Tonique de rage", new[] { B(StatType.PhysicalAttack, 30), B(StatType.Lifesteal, 0.1f) }),
            ("Elixir_Sorcery", "Philtre des arcanes", new[] { B(StatType.MagicAttack, 50), B(StatType.ManaRegen, 3) }),
            ("Elixir_Iron", "Potion de rempart", new[] { B(StatType.MaxHealth, 300), B(StatType.Tenacity, 0.25f) }),
        };

        public static IReadOnlyList<string> ItemIds {
            get {
                var ids = new List<string>();
                foreach (Item i in Items) ids.Add(i.Id);
                return ids;
            }
        }

        public static void Run() {
            foreach (string png in Directory.GetFiles(IconsDir, "Item_*.png")) EconomySetup.ConfigureIcon(png.Replace('\\', '/'));
            var items = new List<Object>();
            var prices = new System.Text.StringBuilder("[ItemsSetup] prices:");
            foreach (Item i in Items) {
                ItemDefinition item = ItemOnce(i);
                PriceAndRoles(item, i);
                prices.Append($" {i.Id}={item.Cost}");
                items.Add(item);
            }
            Debug.Log(prices.ToString());

            var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(EconomySetup.CatalogPath);
            if (catalog != null && !SameItems(catalog, items)) {
                var so = new SerializedObject(catalog);
                SerializedProperty list = so.FindProperty("items");
                list.arraySize = items.Count;
                for (int k = 0; k < items.Count; k++) list.GetArrayElementAtIndex(k).objectReferenceValue = items[k];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            RenameElixirs();
            ArmGrokk();
            RoleAnvils(catalog);
        }

        // ---- Legendary anvils by role (design doc §9.3): 3 legendary items of that role, 1 reroll each ----

        private const int RoleAnvilCost = 280;
        private static readonly (ItemRole role, string id, string name)[] AnvilRoles = {
            (ItemRole.Marksman, "Marksman", "Enclume légendaire du tireur"), (ItemRole.Mage, "Mage", "Enclume légendaire du mage"),
            (ItemRole.Fighter, "Fighter", "Enclume légendaire du combattant"), (ItemRole.Assassin, "Assassin", "Enclume légendaire de l'assassin"),
            (ItemRole.Tank, "Tank", "Enclume légendaire du tank"),
        };

        private static void RoleAnvils(ShopCatalog catalog) {
            if (catalog == null) return;
            string dir = $"{EconomySetup.Dir}/Anvils";
            var anvils = new List<Object>();
            var prismatic = AssetDatabase.LoadAssetAtPath<AnvilDefinition>($"{dir}/Anvil_Prismatic.asset");
            if (prismatic != null) anvils.Add(prismatic);
            foreach ((ItemRole role, string id, string name) in AnvilRoles) {
                string path = $"{dir}/Anvil_Legendary_{id}.asset";
                var anvil = AssetDatabase.LoadAssetAtPath<AnvilDefinition>(path);
                if (anvil == null) {
                    anvil = ScriptableObject.CreateInstance<AnvilDefinition>();
                    AssetDatabase.CreateAsset(anvil, path);
                }
                string icon = $"{IconsDir}/Anvil_Legendary_{id}.png";
                EconomySetup.ConfigureIcon(icon);
                var so = new SerializedObject(anvil);
                so.FindProperty("displayName").stringValue = name;
                so.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(icon);
                so.FindProperty("kind").enumValueIndex = (int)AnvilKind.Item;
                so.FindProperty("itemTier").enumValueIndex = (int)ItemTier.Legendary;
                so.FindProperty("cost").intValue = RoleAnvilCost;
                so.FindProperty("rerollsPerChoice").intValue = 1;
                so.FindProperty("role").intValue = (int)role;
                so.ApplyModifiedPropertiesWithoutUndo();
                anvils.Add(anvil);
            }
            var cso = new SerializedObject(catalog);
            SerializedProperty list = cso.FindProperty("itemAnvils");
            list.arraySize = anvils.Count;
            for (int k = 0; k < anvils.Count; k++) list.GetArrayElementAtIndex(k).objectReferenceValue = anvils[k];
            cso.ApplyModifiedPropertiesWithoutUndo();
            // The generic legendary anvil is replaced by these
            if (AssetDatabase.LoadAssetAtPath<AnvilDefinition>($"{dir}/Anvil_Legendary.asset") != null) AssetDatabase.DeleteAsset($"{dir}/Anvil_Legendary.asset");
        }

        private static ItemDefinition ItemOnce(Item i) {
            string path = $"{Dir}/Item_{i.Id}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item != null) return item;
            Directory.CreateDirectory(Dir);
            item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, path);
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsDir}/Item_{i.Id}.png");
            var so = new SerializedObject(item);
            so.FindProperty("displayName").stringValue = i.Name;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("cost").intValue = i.Cost;
            so.FindProperty("tier").enumValueIndex = (int)i.Tier;
            so.FindProperty("uniqueGroup").stringValue = i.Group;
            EconomySetup.WriteStats(so.FindProperty("stats"), i.Stats);
            if (i.Passive != null) so.FindProperty("passive").objectReferenceValue = PassiveSetup.Passive(i.Passive, null, icon);
            so.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        // ---- Prices and roles (design doc §9.3), rewritten on every run ----

        // Gold per unit of each stat (percent stats: per 1.0 = 100 %)
        private static float StatValue(StatType stat) => stat switch {
            StatType.PhysicalAttack => 3.5f, StatType.MagicAttack => 2.2f, StatType.MaxHealth => 0.27f,
            StatType.Armor => 2f, StatType.MagicResist => 1.8f, StatType.AttackSpeed => 250f,
            StatType.CritChance => 400f, StatType.CritDamage => 200f, StatType.AbilityHaste => 2.7f,
            StatType.MoveSpeed => 400f, StatType.Lifesteal => 375f, StatType.Omnivamp => 400f,
            StatType.MaxMana => 0.14f, StatType.ManaRegen => 12f, StatType.HealthRegen => 9f,
            StatType.ArmorPenFlat => 5f, StatType.ArmorPenPercent => 420f, StatType.MagicPenFlat => 3.1f,
            StatType.MagicPenPercent => 470f, StatType.Tenacity => 200f, StatType.HealShieldPower => 250f,
            StatType.AdaptiveForce => 2.5f,
            _ => 0f,
        };

        // Percent stats (attack speed, crit…) are already shares; a percent of a flat stat is
        // valued on a typical late-run amount of it; flat move speed is worth more than a percent
        private static float BonusValue(StatBonus b) {
            if (b.Stat == StatType.MoveSpeed) return b.Type == ModifierType.Flat ? 77f * b.Value : 395f * b.Value;
            if (b.Type == ModifierType.Flat || b.Stat is StatType.AttackSpeed or StatType.CritChance or StatType.CritDamage
                    or StatType.Lifesteal or StatType.Omnivamp or StatType.ArmorPenPercent or StatType.MagicPenPercent
                    or StatType.Tenacity or StatType.HealShieldPower)
                return StatValue(b.Stat) * b.Value;
            float typical = b.Stat switch {
                StatType.PhysicalAttack => 200f, StatType.MagicAttack => 300f, StatType.MaxHealth => 2500f,
                StatType.Armor => 150f, StatType.MagicResist => 120f, StatType.MaxMana => 1500f, _ => 50f,
            };
            return StatValue(b.Stat) * b.Value * typical;
        }

        private const float PassiveValue = 50f;
        private const float GrowingValue = 110f;   // permanent stacks for the run     // a passive that does something (triggers, aura, stacks…)
        private const float StatShare = 1f;         // items are a bit more efficient than raw stats

        /// <summary>Price from the stats (and the passive's own stats), plus the value of its effect.</summary>
        public static int PriceOf(StatBonus[] stats, PassiveDefinition passive, bool boots) {
            float value = 0f;
            foreach (StatBonus b in stats) value += BonusValue(b);
            bool effect = false;
            if (passive != null) {
                foreach (StatBonus b in passive.Stats) value += BonusValue(b);
                effect = passive.Triggers.Length > 0 || passive.Stacks.Source != StackSource.None || passive.NearbyEnemies.Radius > 0f
                         || passive.Aura.Radius > 0f || passive.Conversions.Length > 0;
            }
            float price = value * StatShare + (effect ? PassiveValue : 0f);
            // Growing items (permanent stacks) are worth more the longer they are kept
            if (passive != null && System.Array.Exists(passive.Triggers, t => t.Effect == PassiveEffectKind.AddStack)) price += GrowingValue;
            price = boots ? Mathf.Clamp(price, 90f, 160f) : Mathf.Clamp(price, 260f, 420f);
            return Mathf.RoundToInt(price / 5f) * 5;
        }

        private static readonly Dictionary<string, ItemRole> RoleOverrides = new() {
            { "WraithKnife", ItemRole.Assassin }, { "TrophyCrown", ItemRole.Assassin | ItemRole.Fighter },
            { "FrostbiteAxe", ItemRole.Assassin | ItemRole.Marksman }, { "MoonblightBlade", ItemRole.Assassin | ItemRole.Fighter },
            { "GraveFang", ItemRole.Marksman | ItemRole.Fighter }, { "HexEdge", ItemRole.Marksman | ItemRole.Fighter },
            { "WrathBlade", ItemRole.Marksman | ItemRole.Mage }, { "SerpentFang", ItemRole.Mage | ItemRole.Marksman },
            { "BoneLantern", ItemRole.Mage | ItemRole.Assassin }, { "GloomTorch", ItemRole.Mage | ItemRole.Assassin },
            { "TriadSigil", ItemRole.Fighter | ItemRole.Marksman }, { "IronCleaver", ItemRole.Fighter | ItemRole.Tank },
            { "OathGauntlet", ItemRole.Fighter | ItemRole.Tank }, { "TitanHeart", ItemRole.Tank | ItemRole.Fighter },
            { "SoulLocket", ItemRole.Tank | ItemRole.Fighter },
            { "ForgeHeart", ItemRole.Tank | ItemRole.Fighter }, { "ConquerorTrophy", ItemRole.Fighter | ItemRole.Assassin | ItemRole.Marksman },
            { "SoulGrimoire", ItemRole.Mage },
        };

        // Role of the table section, unless overridden
        private static ItemRole RoleOf(Item i, int index) {
            if (RoleOverrides.TryGetValue(i.Id, out ItemRole role)) return role;
            if (i.Group == Boots) return ItemRole.Marksman | ItemRole.Mage | ItemRole.Fighter | ItemRole.Tank | ItemRole.Assassin;
            bool Has(StatType t) => System.Array.Exists(i.Stats, b => b.Stat == t);
            if (Has(StatType.MagicAttack) && !Has(StatType.PhysicalAttack)) return ItemRole.Mage;
            if (Has(StatType.Armor) || Has(StatType.MagicResist)) return Has(StatType.PhysicalAttack) ? ItemRole.Fighter : ItemRole.Tank;
            if (Has(StatType.CritChance) || Has(StatType.AttackSpeed)) return ItemRole.Marksman;
            if (Has(StatType.MaxHealth)) return Has(StatType.PhysicalAttack) ? ItemRole.Fighter : ItemRole.Tank;
            return Has(StatType.PhysicalAttack) ? ItemRole.Fighter : ItemRole.None;
        }

        private static void PriceAndRoles(ItemDefinition item, Item i) {
            var so = new SerializedObject(item);
            if (i.Tier == ItemTier.Legendary) so.FindProperty("cost").intValue = PriceOf(i.Stats, item.Passive, i.Group == Boots);
            so.FindProperty("roles").intValue = (int)RoleOf(i, 0);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool SameItems(ShopCatalog catalog, List<Object> items) {
            if (catalog.Items.Count != items.Count) return false;
            for (int k = 0; k < items.Count; k++) if (catalog.Items[k] != items[k]) return false;
            return true;
        }

        // Their names (once: an elixir renamed by hand is kept)
        private static void RenameElixirs() {
            foreach ((string asset, string name, StatBonus[] stats) in Elixirs) {
                var elixir = AssetDatabase.LoadAssetAtPath<ElixirDefinition>($"{EconomySetup.Dir}/Elixirs/{asset}.asset");
                if (elixir == null || elixir.DisplayName == name) continue;
                var so = new SerializedObject(elixir);
                so.FindProperty("displayName").stringValue = name;
                EconomySetup.WriteStats(so.FindProperty("stats"), stats);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // Grokk carries items too: the Ember Plate (burns whoever stands next to him) and the Iron Cleaver
        private static void ArmGrokk() {
            var grokk = AssetDatabase.LoadAssetAtPath<BossDefinition>(BossSetup.GrokkPath);
            if (grokk == null) return;
            var so = new SerializedObject(grokk);
            SerializedProperty list = so.FindProperty("items");
            bool lol = list.arraySize > 0;
            for (int k = 0; k < list.arraySize; k++) {
                var item = list.GetArrayElementAtIndex(k).objectReferenceValue as ItemDefinition;
                lol &= item != null && ((List<string>)ItemIds).Contains(item.name.Replace("Item_", ""));
            }
            if (lol) return;
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{Dir}/Item_SunfireAegis.asset");
            list.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{Dir}/Item_BlackCleaver.asset");
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>Fluent helpers to write passives as one expression in the tables.</summary>
    internal static class PassiveBuilder {
        public static PassiveSetup.P With(this PassiveSetup.P p, params PassiveTrigger[] triggers) {
            var all = new List<PassiveTrigger>(p.Triggers);
            all.AddRange(triggers);
            p.Triggers = all.ToArray();
            return p;
        }

        public static PassiveSetup.P With(this PassiveSetup.P p, StatBonus[] stats) {
            p.Stats = stats;
            return p;
        }

        public static PassiveSetup.P WithStacks(this PassiveSetup.P p, StackSource source, ResetPolicy reset, int max, float duration, params StatBonus[] perStack) {
            p.Stacks = new StackSettings { Source = source, Reset = reset, Max = max, Duration = duration, PerStack = perStack };
            return p;
        }

        public static PassiveSetup.P WithAura(this PassiveSetup.P p, float radius, float interval, float damage, StatType stat, float ratio,
                                              DamageType type, float healPerEnemy = 0f) {
            p.Aura = new AuraSettings { Radius = radius, Interval = interval, Damage = damage, ScalingStat = stat, Ratio = ratio, DamageType = type,
                                        HealPerEnemy = healPerEnemy };
            return p;
        }

        public static PassiveSetup.P WithConversion(this PassiveSetup.P p, StatType from, StatType to, float ratio, bool bonusOnly = true) {
            p.Conversions = new[] { new StatConversion { From = from, To = to, Ratio = ratio, BonusOnly = bonusOnly } };
            return p;
        }

        public static PassiveSetup.P WithNearby(this PassiveSetup.P p, float radius, int max, params StatBonus[] perEnemy) {
            p.Nearby = new NearbyEnemiesBonus { Radius = radius, Max = max, PerEnemy = perEnemy };
            return p;
        }
    }
}
#endif
