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
    /// The items, copied from League of Legends (design doc §9.3), called by <see cref="ProjectSetup"/>:
    /// legendary items (marksman, mage, fighter, tank), tier 2 boots (one pair at a time) and Arena
    /// style prismatic items with adaptive force. Each item = its LoL stats + its own passive, built
    /// from the generic passive bricks (simplified where LoL needs an active or another champion).
    /// Prices are LoL's / 10, move speed 45 → +0.7. Created once; the shop catalog's item list is
    /// set to exactly these items (older test items are dropped from it), the elixirs take LoL's names
    /// and Grokk carries two of them.
    /// </summary>
    public static class LolItemsSetup {
        private const string Dir = "Assets/Data/Economy/Items";
        private const string IconsDir = "Assets/GraphicAssets/Sprites/Items";
        private const string Boots = "Bottes", Spellblade = "Lame enchantée", Hydra = "Hydre", Lifeline = "Ligne de vie";

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
            new() { Id = "InfinityEdge", Name = "Lame d'infini", Cost = 345, Stats = new[] { B(StatType.PhysicalAttack, 65), B(StatType.CritChance, 0.25f) },
                Passive = Pas("InfinityEdge", "Perfection", "+40 % dégâts des coups critiques.").With(stats: new[] { B(StatType.CritDamage, 0.4f) }) },
            new() { Id = "KrakenSlayer", Name = "Tueur de krakens", Cost = 310, Stats = new[] { B(StatType.PhysicalAttack, 45), AS(0.4f), MS(0.04f) },
                Passive = Pas("KrakenSlayer", "Abattre", "Une attaque sur trois inflige 140 (+40 % AD) dégâts physiques en plus.")
                    .With(OnHit(140f, StatType.PhysicalAttack, 0.4f, DamageType.Physical, everyN: 3)) },
            new() { Id = "RunaansHurricane", Name = "Ouragan de Runaan", Cost = 265, Stats = new[] { AS(0.4f), B(StatType.CritChance, 0.25f), MS(0.04f) },
                Passive = Pas("RunaansHurricane", "Fureur du vent", "Les attaques tirent 2 traits sur des ennemis proches : 55 % AD dégâts physiques.")
                    .With(Bolts(PassiveEvent.BasicAttackHit, 2, 5f, 0f, StatType.PhysicalAttack, 0.55f, DamageType.Physical, Storm)) },
            new() { Id = "StatikkShiv", Name = "Shiv statique", Cost = 290, Stats = new[] { B(StatType.PhysicalAttack, 45), AS(0.3f), MS(0.04f) },
                Passive = Pas("StatikkShiv", "Électrocharge", "Une attaque sur trois foudroie 5 ennemis proches : 60 (+15 % AP) dégâts magiques.")
                    .With(Bolts(PassiveEvent.BasicAttackHit, 5, 6f, 60f, StatType.MagicAttack, 0.15f, DamageType.Magic, Storm, everyN: 3)) },
            new() { Id = "PhantomDancer", Name = "Danseur fantôme", Cost = 265, Stats = new[] { AS(0.6f), B(StatType.CritChance, 0.25f), MS(0.08f) },
                Passive = Pas("PhantomDancer", "Valse spectrale", "Les attaques donnent +10 % vitesse de déplacement pendant 2 s.")
                    .With(Timed(PassiveEvent.BasicAttackHit, 2f, 0f, MS(0.1f))) },
            new() { Id = "Bloodthirster", Name = "Soif-de-sang", Cost = 340, Stats = new[] { B(StatType.PhysicalAttack, 80), B(StatType.Lifesteal, 0.15f) },
                Passive = Pas("Bloodthirster", "Ichor", "Touché sous 35 % de PV : bouclier de 165 (+10 % PV max) pendant 4 s. Recharge 30 s.")
                    .With(LowHealthShield(0.35f, 165f, StatType.MaxHealth, 0.1f, 4f, 30f)), Group = Lifeline },
            new() { Id = "LordDominiksRegards", Name = "Salutations de Dominik", Cost = 300, Stats = new[] { B(StatType.PhysicalAttack, 35), B(StatType.CritChance, 0.25f), B(StatType.ArmorPenPercent, 0.35f) },
                Passive = Pas("LordDominiksRegards", "Tueur de géants", "Les attaques infligent 2 % des PV max de la cible en dégâts physiques.")
                    .With(new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.BonusDamage,
                                               TargetMaxHealthRatio = 0.02f, DamageType = DamageType.Physical }) },
            new() { Id = "NavoriFlickerblade", Name = "Lame éclipsante de Navori", Cost = 260, Stats = new[] { AS(0.4f), B(StatType.CritChance, 0.25f), MS(0.04f) },
                Passive = Pas("NavoriFlickerblade", "Transcendance", "Les attaques réduisent les délais de récupération de 0,5 s.")
                    .With(new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.ReduceCooldowns, Amount = 0.5f }) },
            new() { Id = "EssenceReaver", Name = "Faucheur d'essence", Cost = 290, Group = Spellblade, Stats = new[] { B(StatType.PhysicalAttack, 60), B(StatType.AbilityHaste, 15), B(StatType.CritChance, 0.25f) },
                Passive = Pas("EssenceReaver", "Lame enchantée", "Après un sort, la prochaine attaque inflige 100 % AD dégâts physiques en plus (recharge 1,5 s).")
                    .With(SpellbladeTriggers(0f, StatType.PhysicalAttack, 1f, DamageType.Physical)) },
            new() { Id = "BladeOfTheRuinedKing", Name = "Lame du roi déchu", Cost = 320, Stats = new[] { B(StatType.PhysicalAttack, 40), AS(0.25f), B(StatType.Lifesteal, 0.1f) },
                Passive = Pas("BladeOfTheRuinedKing", "Lame de brume", "Les attaques infligent 6 % des PV actuels de la cible en dégâts physiques.")
                    .With(new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.BonusDamage,
                                               TargetCurrentHealthRatio = 0.06f, DamageType = DamageType.Physical }) },
            new() { Id = "WitsEnd", Name = "Fin de l'esprit", Cost = 280, Stats = new[] { AS(0.5f), B(StatType.MagicResist, 45), B(StatType.Tenacity, 0.2f) },
                Passive = Pas("WitsEnd", "Rixe", "Les attaques infligent 45 dégâts magiques en plus.")
                    .With(OnHit(45f, StatType.MagicAttack, 0f, DamageType.Magic)) },
            new() { Id = "GuinsoosRageblade", Name = "Lame enragée de Guinsoo", Cost = 300, Stats = new[] { B(StatType.PhysicalAttack, 30), B(StatType.MagicAttack, 30), AS(0.25f) },
                Passive = Pas("GuinsoosRageblade", "Ire", "Les attaques infligent 30 dégâts magiques et donnent +8 % vitesse d'attaque (4 fois, 3 s).")
                    .With(OnHit(30f, StatType.MagicAttack, 0f, DamageType.Magic))
                    .WithStacks(StackSource.PerBasicAttack, ResetPolicy.AfterDuration, 4, 3f, AS(0.08f)) },
            new() { Id = "RapidFirecannon", Name = "Canon ultrarapide", Cost = 265, Stats = new[] { AS(0.35f), B(StatType.CritChance, 0.25f), MS(0.04f) },
                Passive = Pas("RapidFirecannon", "Tir de précision", "Une attaque sur trois inflige 120 dégâts magiques en plus.")
                    .With(OnHit(120f, StatType.MagicAttack, 0f, DamageType.Magic, everyN: 3)) },
            new() { Id = "Hubris", Name = "Hubris", Cost = 295, Stats = new[] { B(StatType.PhysicalAttack, 60), B(StatType.AbilityHaste, 10) },
                Passive = Pas("Hubris", "Éminence", "Chaque kill donne +1 AD pour la partie (30 max).")
                    .WithStacks(StackSource.PerKill, ResetPolicy.Never, 30, 0f, B(StatType.PhysicalAttack, 1)) },
            new() { Id = "YoumuusGhostblade", Name = "Lame spectrale de Youmuu", Cost = 280, Stats = new[] { B(StatType.PhysicalAttack, 55), B(StatType.ArmorPenFlat, 18), B(StatType.AbilityHaste, 15) },
                Passive = Pas("YoumuusGhostblade", "Hantise", "Chaque kill donne +20 % vitesse de déplacement pendant 2 s.")
                    .With(Timed(PassiveEvent.Kill, 2f, 0f, MS(0.2f))) },
            new() { Id = "SeryldasGrudge", Name = "Rancune de Serylda", Cost = 320, Stats = new[] { B(StatType.PhysicalAttack, 45), B(StatType.ArmorPenPercent, 0.3f), B(StatType.AbilityHaste, 15) },
                Passive = Pas("SeryldasGrudge", "Froid mordant", "Les sorts ralentissent de 30 % pendant 1 s.")
                    .With(Status(PassiveEvent.SpellHit, Slow(0.3f, 1f))) },

            // ---- Mage ----
            new() { Id = "RabadonsDeathcap", Name = "Coiffe de Rabadon", Cost = 360, Stats = new[] { B(StatType.MagicAttack, 130) },
                Passive = Pas("RabadonsDeathcap", "Magnum opus", "+30 % puissance magique.").With(stats: new[] { B(StatType.MagicAttack, 0.3f, ModifierType.PercentMult) }) },
            new() { Id = "LudensCompanion", Name = "Compagnon de Luden", Cost = 290, Stats = new[] { B(StatType.MagicAttack, 100), B(StatType.MaxMana, 600), B(StatType.AbilityHaste, 10) },
                Passive = Pas("LudensCompanion", "Feu", "Un sort qui touche tire 3 traits sur des ennemis proches : 60 (+10 % AP) dégâts magiques. Recharge 3 s.")
                    .With(Bolts(PassiveEvent.SpellHit, 3, 6f, 60f, StatType.MagicAttack, 0.1f, DamageType.Magic, Arcane, cooldown: 3f)) },
            new() { Id = "LiandrysTorment", Name = "Tourment de Liandry", Cost = 300, Stats = new[] { B(StatType.MagicAttack, 90), B(StatType.MaxHealth, 300) },
                Passive = Pas("LiandrysTorment", "Supplice", "Les sorts brûlent : 1 % des PV max de la cible par seconde pendant 3 s.")
                    .With(Status(PassiveEvent.SpellHit, Burn(5f, 0.01f, 3f))) },
            new() { Id = "VoidStaff", Name = "Bâton du vide", Cost = 300, Stats = new[] { B(StatType.MagicAttack, 95) },
                Passive = Pas("VoidStaff", "Dissolution", "+40 % pénétration magique.").With(stats: new[] { B(StatType.MagicPenPercent, 0.4f) }) },
            new() { Id = "LichBane", Name = "Fléau de liche", Cost = 290, Group = Spellblade, Stats = new[] { B(StatType.MagicAttack, 100), MS(0.04f), B(StatType.AbilityHaste, 10) },
                Passive = Pas("LichBane", "Lame enchantée", "Après un sort, la prochaine attaque inflige 50 (+60 % AP) dégâts magiques en plus (recharge 1,5 s).")
                    .With(SpellbladeTriggers(50f, StatType.MagicAttack, 0.6f, DamageType.Magic)) },
            new() { Id = "RylaisCrystalScepter", Name = "Sceptre de cristal de Rylai", Cost = 260, Stats = new[] { B(StatType.MagicAttack, 65), B(StatType.MaxHealth, 400) },
                Passive = Pas("RylaisCrystalScepter", "Givre éternel", "Les sorts ralentissent de 30 % pendant 1 s.")
                    .With(Status(PassiveEvent.SpellHit, Slow(0.3f, 1f))) },
            new() { Id = "NashorsTooth", Name = "Dent de Nashor", Cost = 290, Stats = new[] { AS(0.5f), B(StatType.MagicAttack, 80) },
                Passive = Pas("NashorsTooth", "Morsure de l'Icathia", "Les attaques infligent 15 (+20 % AP) dégâts magiques en plus.")
                    .With(OnHit(15f, StatType.MagicAttack, 0.2f, DamageType.Magic)) },
            new() { Id = "ArchangelsStaff", Name = "Bâton de l'archange", Cost = 290, Stats = new[] { B(StatType.MagicAttack, 70), B(StatType.MaxMana, 600), B(StatType.AbilityHaste, 25) },
                Passive = Pas("ArchangelsStaff", "Admiration", "Gagne de la puissance magique égale à 1 % du mana bonus.")
                    .WithConversion(StatType.MaxMana, StatType.MagicAttack, 0.01f) },
            new() { Id = "Shadowflame", Name = "Flamme d'ombre", Cost = 320, Stats = new[] { B(StatType.MagicAttack, 110), B(StatType.MagicPenFlat, 15) },
                Passive = Pas("Shadowflame", "Cendrebrûlure", "Les sorts brûlent : 25 dégâts magiques par seconde pendant 2 s.")
                    .With(Status(PassiveEvent.SpellHit, Burn(25f, 0f, 2f))) },
            new() { Id = "CosmicDrive", Name = "Moteur cosmique", Cost = 300, Stats = new[] { B(StatType.MagicAttack, 70), B(StatType.MaxHealth, 350), B(StatType.AbilityHaste, 25), MS(0.04f) },
                Passive = Pas("CosmicDrive", "Agilité", "Un sort qui touche donne +20 % vitesse de déplacement pendant 2 s.")
                    .With(Timed(PassiveEvent.SpellHit, 2f, 0f, MS(0.2f))) },
            new() { Id = "BlackfireTorch", Name = "Torche de feu noir", Cost = 280, Stats = new[] { B(StatType.MagicAttack, 80), B(StatType.MaxMana, 600), B(StatType.AbilityHaste, 20) },
                Passive = Pas("BlackfireTorch", "Flamme noire", "Les sorts brûlent : 15 dégâts magiques par seconde pendant 3 s, cumulable 3 fois.")
                    .With(Status(PassiveEvent.SpellHit, Burn(15f, 0f, 3f, 3))) },
            new() { Id = "Riftmaker", Name = "Faiseur de failles", Cost = 310, Stats = new[] { B(StatType.MagicAttack, 70), B(StatType.MaxHealth, 350), B(StatType.AbilityHaste, 15), B(StatType.Omnivamp, 0.08f) },
                Passive = Pas("Riftmaker", "Corruption du Néant", "Un sort qui touche donne +6 puissance magique (5 fois, 4 s).")
                    .WithStacks(StackSource.PerSpellHit, ResetPolicy.AfterDuration, 5, 4f, B(StatType.MagicAttack, 6)) },

            // ---- Fighter ----
            new() { Id = "TrinityForce", Name = "Force de la trinité", Cost = 333, Group = Spellblade, Stats = new[] { B(StatType.PhysicalAttack, 36), AS(0.3f), B(StatType.AbilityHaste, 15), B(StatType.MaxHealth, 333) },
                Passive = Pas("TrinityForce", "Lame enchantée", "Après un sort, la prochaine attaque inflige 200 % AD dégâts physiques en plus (recharge 1,5 s).")
                    .With(SpellbladeTriggers(0f, StatType.PhysicalAttack, 2f, DamageType.Physical)) },
            new() { Id = "BlackCleaver", Name = "Couperet noir", Cost = 300, Stats = new[] { B(StatType.PhysicalAttack, 40), B(StatType.MaxHealth, 400), B(StatType.AbilityHaste, 20) },
                Passive = Pas("BlackCleaver", "Fureur", "Les attaques donnent +20 % vitesse de déplacement pendant 2 s.")
                    .With(Timed(PassiveEvent.BasicAttackHit, 2f, 0f, MS(0.2f))) },
            new() { Id = "SunderedSky", Name = "Ciel fracturé", Cost = 310, Stats = new[] { B(StatType.PhysicalAttack, 40), B(StatType.MaxHealth, 400), B(StatType.AbilityHaste, 10) },
                Passive = Pas("SunderedSky", "Frappe de lumière", "Toutes les 6 s, une attaque inflige 75 % AD en plus et rend 6 % des PV max.")
                    .With(OnHit(0f, StatType.PhysicalAttack, 0.75f, DamageType.Physical, cooldown: 6f),
                          new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 1, Chance = 1f, Cooldown = 6f, Effect = PassiveEffectKind.Heal,
                                               AmountStat = StatType.MaxHealth, AmountRatio = 0.06f }) },
            new() { Id = "TitanicHydra", Name = "Hydre titanesque", Cost = 330, Group = Hydra, Stats = new[] { B(StatType.PhysicalAttack, 40), B(StatType.MaxHealth, 600) },
                Passive = Pas("TitanicHydra", "Fendoir", "Les attaques infligent 5 (+1,5 % PV max) dégâts physiques aux ennemis autour de la cible.")
                    .With(new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.Splash, Radius = 2.5f,
                                               Damage = 5f, ScalingStat = StatType.MaxHealth, DamageRatio = 0.015f, DamageType = DamageType.Physical }) },
            new() { Id = "RavenousHydra", Name = "Hydre vorace", Cost = 330, Group = Hydra, Stats = new[] { B(StatType.PhysicalAttack, 65), B(StatType.AbilityHaste, 15), B(StatType.Omnivamp, 0.1f) },
                Passive = Pas("RavenousHydra", "Fendoir", "Les attaques infligent 40 % AD dégâts physiques aux ennemis autour de la cible.")
                    .With(new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.Splash, Radius = 2.5f,
                                               ScalingStat = StatType.PhysicalAttack, DamageRatio = 0.4f, DamageType = DamageType.Physical }) },
            new() { Id = "SteraksGage", Name = "Gage de Sterak", Cost = 320, Group = Lifeline, Stats = new[] { B(StatType.MaxHealth, 400), B(StatType.PhysicalAttack, 0.45f, ModifierType.PercentAdd) },
                Passive = Pas("SteraksGage", "Ligne de vie", "Touché sous 30 % de PV : bouclier de 25 % des PV max pendant 4 s. Recharge 90 s.")
                    .With(LowHealthShield(0.3f, 0f, StatType.MaxHealth, 0.25f, 4f, 90f)) },
            new() { Id = "DeathsDance", Name = "Danse de la mort", Cost = 330, Stats = new[] { B(StatType.PhysicalAttack, 60), B(StatType.Armor, 50), B(StatType.AbilityHaste, 15) },
                Passive = Pas("DeathsDance", "Défiance", "Chaque kill rend 10 % des PV max.")
                    .With(new PassiveTrigger { On = PassiveEvent.Kill, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.Heal, AmountStat = StatType.MaxHealth, AmountRatio = 0.1f }) },
            new() { Id = "MawOfMalmortius", Name = "Gueule de Malmortius", Cost = 310, Group = Lifeline, Stats = new[] { B(StatType.PhysicalAttack, 60), B(StatType.MagicResist, 40), B(StatType.AbilityHaste, 15) },
                Passive = Pas("MawOfMalmortius", "Ligne de vie", "Touché sous 30 % de PV : bouclier de 200 (+15 % PV max) pendant 4 s. Recharge 90 s.")
                    .With(LowHealthShield(0.3f, 200f, StatType.MaxHealth, 0.15f, 4f, 90f)) },
            new() { Id = "Eclipse", Name = "Éclipse", Cost = 290, Stats = new[] { B(StatType.PhysicalAttack, 60), B(StatType.AbilityHaste, 15) },
                Passive = Pas("Eclipse", "Lune ascendante", "Toutes les 6 s, toucher avec un sort inflige 6 % des PV max de la cible et donne un bouclier de 80.")
                    .With(new PassiveTrigger { On = PassiveEvent.SpellHit, EveryN = 1, Chance = 1f, Cooldown = 6f, Effect = PassiveEffectKind.BonusDamage,
                                               TargetMaxHealthRatio = 0.06f, DamageType = DamageType.Physical },
                          new PassiveTrigger { On = PassiveEvent.SpellHit, EveryN = 1, Chance = 1f, Cooldown = 6f, Effect = PassiveEffectKind.Shield, Amount = 80f, Duration = 2f }) },
            new() { Id = "SpearOfShojin", Name = "Lance de Shojin", Cost = 310, Stats = new[] { B(StatType.PhysicalAttack, 45), B(StatType.MaxHealth, 450), B(StatType.AbilityHaste, 20) },
                Passive = Pas("SpearOfShojin", "Focus du dragon", "Un sort qui touche donne +4 AD (4 fois, 6 s).")
                    .WithStacks(StackSource.PerSpellHit, ResetPolicy.AfterDuration, 4, 6f, B(StatType.PhysicalAttack, 4)) },

            // ---- Tank ----
            new() { Id = "SunfireAegis", Name = "Égide du feu solaire", Cost = 270, Stats = new[] { B(StatType.MaxHealth, 350), B(StatType.Armor, 50) },
                Passive = Pas("SunfireAegis", "Immolation", "Brûle les ennemis à moins de 3 m : 20 (+1 % PV max) dégâts magiques par seconde.")
                    .WithAura(3f, 1f, 20f, StatType.MaxHealth, 0.01f, DamageType.Magic) },
            new() { Id = "Thornmail", Name = "Cotte épineuse", Cost = 245, Stats = new[] { B(StatType.MaxHealth, 150), B(StatType.Armor, 75) },
                Passive = Pas("Thornmail", "Épines", "Quand un ennemi te touche, il subit 10 (+15 % armure) dégâts magiques.")
                    .With(new PassiveTrigger { On = PassiveEvent.DamageTaken, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.BonusDamage,
                                               Damage = 10f, ScalingStat = StatType.Armor, DamageRatio = 0.15f, DamageType = DamageType.Magic }) },
            new() { Id = "RanduinsOmen", Name = "Présage de Randuin", Cost = 270, Stats = new[] { B(StatType.MaxHealth, 350), B(StatType.Armor, 75) },
                Passive = Pas("RanduinsOmen", "Robustesse", "Quand un ennemi te touche, il est ralenti de 25 % pendant 1 s.")
                    .With(Status(PassiveEvent.DamageTaken, Slow(0.25f, 1f))) },
            new() { Id = "WarmogsArmor", Name = "Armure de Warmog", Cost = 310, Stats = new[] { B(StatType.MaxHealth, 1000), B(StatType.HealthRegen, 2) },
                Passive = Pas("WarmogsArmor", "Cœur de Warmog", "Régénère 1 % des PV max par seconde.").WithConversion(StatType.MaxHealth, StatType.HealthRegen, 0.01f, bonusOnly: false) },
            new() { Id = "Heartsteel", Name = "Cœur d'acier", Cost = 300, Stats = new[] { B(StatType.MaxHealth, 900), B(StatType.HealthRegen, 2) },
                Passive = Pas("Heartsteel", "Coup colossal", "Toutes les 8 s, une attaque inflige 70 (+6 % PV max) dégâts physiques en plus.")
                    .With(OnHit(70f, StatType.MaxHealth, 0.06f, DamageType.Physical, cooldown: 8f)) },
            new() { Id = "FrozenHeart", Name = "Cœur gelé", Cost = 250, Stats = new[] { B(StatType.Armor, 65), B(StatType.MaxMana, 400), B(StatType.AbilityHaste, 20) },
                Passive = Pas("FrozenHeart", "Toucher glacial", "Quand un ennemi te touche, il est ralenti de 20 % pendant 1,5 s.")
                    .With(Status(PassiveEvent.DamageTaken, Slow(0.2f, 1.5f))) },
            new() { Id = "ForceOfNature", Name = "Force de la nature", Cost = 280, Stats = new[] { B(StatType.MaxHealth, 400), B(StatType.MagicResist, 55), MS(0.05f) },
                Passive = Pas("ForceOfNature", "Absorption", "Être touché donne +20 résistance magique et +5 % vitesse pendant 3 s.")
                    .With(Timed(PassiveEvent.DamageTaken, 3f, 1f, B(StatType.MagicResist, 20), MS(0.05f))) },
            new() { Id = "SpiritVisage", Name = "Visage spirituel", Cost = 290, Stats = new[] { B(StatType.MaxHealth, 400), B(StatType.MagicResist, 50), B(StatType.AbilityHaste, 10), B(StatType.HealthRegen, 1.5f) },
                Passive = Pas("SpiritVisage", "Cœur sans limite", "+25 % soins et boucliers reçus.").With(stats: new[] { B(StatType.HealShieldPower, 0.25f) }) },
            new() { Id = "JakSho", Name = "Jak'Sho le Protéen", Cost = 320, Stats = new[] { B(StatType.MaxHealth, 350), B(StatType.Armor, 45), B(StatType.MagicResist, 45) },
                Passive = Pas("JakSho", "Résilience du Néant", "+4 armure et +4 résistance magique par ennemi à moins de 5 m (6 max).")
                    .WithNearby(5f, 6, B(StatType.Armor, 4), B(StatType.MagicResist, 4)) },
            new() { Id = "UnendingDespair", Name = "Désespoir éternel", Cost = 280, Stats = new[] { B(StatType.MaxHealth, 400), B(StatType.Armor, 55), B(StatType.AbilityHaste, 10) },
                Passive = Pas("UnendingDespair", "Angoisse", "Toutes les 2 s, inflige 25 dégâts magiques aux ennemis à moins de 3,5 m et rend 6 PV par ennemi.")
                    .WithAura(3.5f, 2f, 25f, StatType.MaxHealth, 0f, DamageType.Magic, healPerEnemy: 6f) },
            new() { Id = "DeadMansPlate", Name = "Plaque du mort", Cost = 290, Stats = new[] { B(StatType.MaxHealth, 300), B(StatType.Armor, 45), MS(0.05f) },
                Passive = Pas("DeadMansPlate", "Écrasement", "Une attaque sur quatre inflige 40 (+100 % AD de base) dégâts physiques et ralentit de 50 % pendant 1 s.")
                    .With(OnHit(40f, StatType.PhysicalAttack, 1f, DamageType.Physical, everyN: 4), Status(PassiveEvent.BasicAttackHit, Slow(0.5f, 1f), everyN: 4)) },

            // ---- Boots (one pair) ----
            new() { Id = "BerserkersGreaves", Name = "Jambières du berserker", Cost = 110, Group = Boots, Stats = new[] { AS(0.35f), B(StatType.MoveSpeed, 0.7f) },
                Passive = Pas("BerserkersGreaves", "Rythme", "+5 % vitesse d'attaque en plus.").With(stats: new[] { AS(0.05f) }) },
            new() { Id = "SorcerersShoes", Name = "Chaussures du sorcier", Cost = 110, Group = Boots, Stats = new[] { B(StatType.MagicPenFlat, 12), B(StatType.MoveSpeed, 0.7f) },
                Passive = Pas("SorcerersShoes", "Focus", "+3 pénétration magique en plus.").With(stats: new[] { B(StatType.MagicPenFlat, 3) }) },
            new() { Id = "PlatedSteelcaps", Name = "Coques en acier renforcé", Cost = 120, Group = Boots, Stats = new[] { B(StatType.Armor, 25), B(StatType.MoveSpeed, 0.7f) },
                Passive = Pas("PlatedSteelcaps", "Blindage", "+2 PV max par point d'armure bonus.").WithConversion(StatType.Armor, StatType.MaxHealth, 2f) },
            new() { Id = "MercurysTreads", Name = "Sandales de Mercure", Cost = 110, Group = Boots, Stats = new[] { B(StatType.MagicResist, 25), B(StatType.MoveSpeed, 0.7f), B(StatType.Tenacity, 0.3f) },
                Passive = Pas("MercurysTreads", "Ténacité", "+30 % ténacité (comprise).").With(stats: new StatBonus[0]) },
            new() { Id = "IonianBoots", Name = "Bottes de lucidité ioniennes", Cost = 90, Group = Boots, Stats = new[] { B(StatType.AbilityHaste, 15), B(StatType.MoveSpeed, 0.7f) },
                Passive = Pas("IonianBoots", "Lucidité", "Les kills réduisent les délais de récupération de 1 s.")
                    .With(new PassiveTrigger { On = PassiveEvent.Kill, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.ReduceCooldowns, Amount = 1f }) },
            new() { Id = "BootsOfSwiftness", Name = "Bottes de célérité", Cost = 100, Group = Boots, Stats = new[] { B(StatType.MoveSpeed, 0.85f), B(StatType.SlowResist, 0.25f) },
                Passive = Pas("BootsOfSwiftness", "Célérité", "-25 % durée et force des ralentissements (comprise).").With(stats: new StatBonus[0]) },

            // ---- Prismatic (anvils only, adaptive force) ----
            new() { Id = "DemonKingsCrown", Name = "Couronne du roi démon", Tier = ItemTier.Prismatic, Stats = new[] { B(StatType.AdaptiveForce, 40), B(StatType.MaxHealth, 300), B(StatType.Armor, 30), B(StatType.MagicResist, 30) },
                Passive = Pas("DemonKingsCrown", "Règne infernal", "Brûle les ennemis à moins de 3,5 m : 30 (+2 % PV max) dégâts magiques par seconde.")
                    .WithAura(3.5f, 1f, 30f, StatType.MaxHealth, 0.02f, DamageType.Magic) },
            new() { Id = "AtmasReckoning", Name = "Jugement d'Atma", Tier = ItemTier.Prismatic, Stats = new[] { B(StatType.AdaptiveForce, 30), B(StatType.MaxHealth, 500), B(StatType.CritChance, 0.2f) },
                Passive = Pas("AtmasReckoning", "Fardeau", "Gagne de l'AD égal à 1,5 % des PV bonus.").WithConversion(StatType.MaxHealth, StatType.PhysicalAttack, 0.015f) },
            new() { Id = "Reverberation", Name = "Réverbération", Tier = ItemTier.Prismatic, Stats = new[] { B(StatType.AdaptiveForce, 60), B(StatType.MagicResist, 30), B(StatType.AbilityHaste, 20) },
                Passive = Pas("Reverberation", "Écho", "Un sort qui touche tire 3 traits sur des ennemis proches : 80 (+20 % AP) dégâts magiques. Recharge 2 s.")
                    .With(Bolts(PassiveEvent.SpellHit, 3, 7f, 80f, StatType.MagicAttack, 0.2f, DamageType.Magic, Arcane, cooldown: 2f)) },
            new() { Id = "ZhonyasParadox", Name = "Paradoxe de Zhonya", Tier = ItemTier.Prismatic, Stats = new[] { B(StatType.AdaptiveForce, 50), B(StatType.Armor, 50), B(StatType.AbilityHaste, 15) },
                Passive = Pas("ZhonyasParadox", "Stase", "Touché sous 30 % de PV : bouclier de 300 (+30 % force adaptative) pendant 2,5 s. Recharge 60 s.")
                    .With(LowHealthShield(0.3f, 300f, StatType.AdaptiveForce, 0.3f, 2.5f, 60f)) },
            new() { Id = "Flesheater", Name = "Mangeur de chair", Tier = ItemTier.Prismatic, Stats = new[] { B(StatType.AdaptiveForce, 40), B(StatType.MaxHealth, 300) },
                Passive = Pas("Flesheater", "Festin", "Chaque kill donne +1 force adaptative pour la partie (60 max).")
                    .WithStacks(StackSource.PerKill, ResetPolicy.Never, 60, 0f, B(StatType.AdaptiveForce, 1)) },
            new() { Id = "LightningRod", Name = "Paratonnerre", Tier = ItemTier.Prismatic, Stats = new[] { B(StatType.AdaptiveForce, 30), AS(0.4f), MS(0.05f) },
                Passive = Pas("LightningRod", "Surtension", "Une attaque sur trois foudroie 4 ennemis proches : 90 (+30 % force adaptative) dégâts magiques.")
                    .With(Bolts(PassiveEvent.BasicAttackHit, 4, 7f, 90f, StatType.AdaptiveForce, 0.3f, DamageType.Magic, Storm, everyN: 3)) },
            new() { Id = "BlossomingDawn", Name = "Lame de l'aube éclose", Tier = ItemTier.Prismatic, Stats = new[] { B(StatType.AdaptiveForce, 50), AS(0.25f), B(StatType.HealShieldPower, 0.2f) },
                Passive = Pas("BlossomingDawn", "Floraison", "Les attaques rendent 20 (+2 % PV max) PV. Recharge 1 s.")
                    .With(new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 1, Chance = 1f, Cooldown = 1f, Effect = PassiveEffectKind.Heal,
                                               Amount = 20f, AmountStat = StatType.MaxHealth, AmountRatio = 0.02f }) },
            new() { Id = "TwinMask", Name = "Masque jumeau", Tier = ItemTier.Prismatic, Stats = new[] { B(StatType.AdaptiveForce, 60), B(StatType.MaxHealth, 400), B(StatType.AbilityHaste, 20) },
                Passive = Pas("TwinMask", "Malédiction", "Les sorts brûlent : 2 % des PV max de la cible par seconde pendant 3 s.")
                    .With(Status(PassiveEvent.SpellHit, Burn(10f, 0.02f, 3f))) },
            new() { Id = "Hamstringer", Name = "Coupe-jarret", Tier = ItemTier.Prismatic, Stats = new[] { B(StatType.AdaptiveForce, 45), AS(0.3f), MS(0.05f) },
                Passive = Pas("Hamstringer", "Entrave", "Les attaques ralentissent de 30 % pendant 1 s ; une sur cinq étourdit 0,6 s.")
                    .With(Status(PassiveEvent.BasicAttackHit, Slow(0.3f, 1f)), Status(PassiveEvent.BasicAttackHit, Stun(0.6f), everyN: 5)) },
            new() { Id = "MoltenStone", Name = "Bouclier de pierre en fusion", Tier = ItemTier.Prismatic, Stats = new[] { B(StatType.AdaptiveForce, 20), B(StatType.MaxHealth, 600), B(StatType.Armor, 40), B(StatType.MagicResist, 40) },
                Passive = Pas("MoltenStone", "Magma", "Quand un ennemi te touche, il subit 30 (+3 % PV max) dégâts magiques.")
                    .With(new PassiveTrigger { On = PassiveEvent.DamageTaken, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.BonusDamage,
                                               Damage = 30f, ScalingStat = StatType.MaxHealth, DamageRatio = 0.03f, DamageType = DamageType.Magic }) },
        };

        private static readonly (string asset, string name, StatBonus[] stats)[] Elixirs = {
            ("Elixir_Strength", "Élixir de colère", new[] { B(StatType.PhysicalAttack, 30), B(StatType.Lifesteal, 0.1f) }),
            ("Elixir_Sorcery", "Élixir de sorcellerie", new[] { B(StatType.MagicAttack, 50), B(StatType.ManaRegen, 3) }),
            ("Elixir_Iron", "Élixir de fer", new[] { B(StatType.MaxHealth, 300), B(StatType.Tenacity, 0.25f) }),
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
            foreach (Item i in Items) items.Add(ItemOnce(i));

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

        private static bool SameItems(ShopCatalog catalog, List<Object> items) {
            if (catalog.Items.Count != items.Count) return false;
            for (int k = 0; k < items.Count; k++) if (catalog.Items[k] != items[k]) return false;
            return true;
        }

        // LoL names (once: an elixir renamed by hand is kept)
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

        // Grokk carries LoL items too: Sunfire Aegis (burns whoever stands next to him) and Black Cleaver
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
