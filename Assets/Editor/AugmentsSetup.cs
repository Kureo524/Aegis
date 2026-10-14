#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Features.Augments;
using Features.Combat;
using Features.Economy;
using Features.Passives;
using Features.Stats;
using UnityEditor;
using UnityEngine;
using P = EditorTools.PassiveSetup.P;

namespace EditorTools {
    /// <summary>
    /// The augments (design doc §9.4), called by <see cref="ProjectSetup"/> after the
    /// older augment setups: effects that change how a run plays (procs, auto-cast spells, novas,
    /// executions, cooldown resets, scaling over the wave) instead of flat stats. Built from the generic
    /// passive bricks, adapted to a solo horde game (provisional values). The catalog is set to
    /// exactly this list; the pure-stat augments of the first version are dropped from it. Assets
    /// are created once.
    /// </summary>
    public static class AugmentsSetup {
        private const string Dir = "Assets/Data/Augments";

        private static StatBonus B(StatType s, float v, ModifierType t = ModifierType.Flat) => new(s, t, v);
        private static StatBonus Pct(StatType s, float v) => B(s, v, ModifierType.PercentAdd);
        private static readonly Color Ice = new(0.6f, 0.85f, 1f, 0.8f), Ember = new(1f, 0.55f, 0.25f, 0.8f), Gold = new(1f, 0.85f, 0.4f, 0.85f);

        private static PassiveTrigger T(PassiveEvent on, PassiveEffectKind effect, float cooldown = 0f, int everyN = 1) =>
            new() { On = on, Effect = effect, EveryN = everyN, Chance = 1f, Cooldown = cooldown };

        // (asset id, rarity, passive). Existing assets with the same id are kept as they are.
        private static readonly (string id, RarityTier rarity, P passive)[] Augments = {
            // ---- Silver ----
            ("CoupDeGrace", RarityTier.Silver, new P { Name = "Coup de grâce", Description = "+20 % de dégâts sur les ennemis sous 50 % de PV. Chaque kill réduit tes délais de récupération de 1 s.",
                Triggers = new[] { T(PassiveEvent.Kill, PassiveEffectKind.ReduceCooldowns).Set(amount: 1f) } }.Damage(0.2f, 0.5f)),
            ("SparkVolley", RarityTier.Silver, new P { Name = "Salve d'étincelles", Description = "Une attaque sur quatre tire 4 fusées sur les ennemis proches : 60 % AD dégâts physiques.",
                Triggers = new[] { T(PassiveEvent.BasicAttackHit, PassiveEffectKind.Bolts, everyN: 4).Bolts(4, 7f, 0f, StatType.PhysicalAttack, 0.6f, DamageType.Physical, Gold) } }),
            ("Heavyweight", RarityTier.Silver, new P { Name = "Poids lourd", Description = "Les attaques infligent 4 % de tes PV max en dégâts physiques en plus.",
                Triggers = new[] { T(PassiveEvent.BasicAttackHit, PassiveEffectKind.BonusDamage).Damage(0f, StatType.MaxHealth, 0.04f, DamageType.Physical) } }),
            ("WillOWisps", RarityTier.Silver, new P { Name = "Feux follets", Description = "Toutes les 7 s, 3 flammes frappent les ennemis proches : 35 (+35 % AD) dégâts magiques.",
                Triggers = new[] { T(PassiveEvent.Periodic, PassiveEffectKind.Bolts, cooldown: 7f).Bolts(3, 8f, 35f, StatType.PhysicalAttack, 0.35f, DamageType.Magic, new Color(0.7f, 0.85f, 1f)) } }),
            ("FrostGrip", RarityTier.Silver, new P { Name = "Étreinte du givre", Description = "Toutes les 6,5 s, immobilise les ennemis à moins de 4,5 m pendant 1,25 s.",
                Triggers = new[] { T(PassiveEvent.Periodic, PassiveEffectKind.Nova, cooldown: 6.5f).Nova(4.5f, Ice, new StatusApplication { Kind = StatusKind.Root, Duration = 1.25f }) } }),
            ("UnstableCore", RarityTier.Silver, new P { Name = "Cœur instable", Description = "Toutes les 13 s, une bombe explose autour de toi : 15 % des PV max des ennemis à moins de 3,5 m en dégâts bruts, étourdit 0,75 s.",
                Triggers = new[] { T(PassiveEvent.Periodic, PassiveEffectKind.Nova, cooldown: 13f).Nova(3.5f, Ember, new StatusApplication { Kind = StatusKind.Stun, Duration = 0.75f })
                                   .TargetHp(0.15f, DamageType.True) } }),
            ("FleetStep", RarityTier.Silver, new P { Name = "Pas fugitif", Description = "Après un saut (Bond éclair, Roulade…), +40 % vitesse de déplacement pendant 3 s.",
                Triggers = new[] { T(PassiveEvent.Blink, PassiveEffectKind.TimedStats).Timed(3f, Pct(StatType.MoveSpeed, 0.4f)) } }),
            ("SurvivalInstinct", RarityTier.Silver, new P { Name = "Instinct de survie", Description = "Touché sous 35 % de PV : bouclier de 65 % de tes PV max et +50 % vitesse pendant 3 s. Recharge 75 s.",
                Triggers = new[] {
                    T(PassiveEvent.DamageTaken, PassiveEffectKind.Shield, cooldown: 75f).Below(0.35f).Set(stat: StatType.MaxHealth, ratio: 0.65f, duration: 3f),
                    T(PassiveEvent.DamageTaken, PassiveEffectKind.TimedStats, cooldown: 75f).Below(0.35f).Timed(3f, Pct(StatType.MoveSpeed, 0.5f)),
                } }),
            ("Corrosion", RarityTier.Silver, new P { Name = "Corrosion", Description = "Chaque coup réduit l'armure et la résistance magique de la cible de 2 %, cumulable 20 fois (4 s).",
                Triggers = new[] {
                    T(PassiveEvent.BasicAttackHit, PassiveEffectKind.ApplyStatus).Status(new StatusApplication { Kind = StatusKind.Shred, Power = 0.02f, Duration = 4f, MaxStacks = 20 }),
                    T(PassiveEvent.SpellHit, PassiveEffectKind.ApplyStatus).Status(new StatusApplication { Kind = StatusKind.Shred, Power = 0.02f, Duration = 4f, MaxStacks = 20 }),
                } }),
            ("SoulCollector", RarityTier.Silver, new P { Name = "Collectionneur d'âmes", Description = "Chaque kill donne +0,5 AD et +1 AP pour toute la partie.",
                Stacks = new StackSettings { Source = StackSource.PerKill, Reset = ResetPolicy.Never, PerStack = new[] { B(StatType.PhysicalAttack, 0.5f), B(StatType.MagicAttack, 1f) } } }),
            ("Gambler", RarityTier.Silver, null),   // kept from phase 8: 6 % per kill to drop a stat anvil
            ("HotBlood", RarityTier.Silver, null),     // kept: AD / AP per enemy nearby

            // ---- Gold ----
            ("SpellEdge", RarityTier.Gold, new P { Name = "Sorts tranchants", Description = "Tes sorts appliquent les effets à l'impact de tes attaques (une fois par seconde).",
                Triggers = new[] { T(PassiveEvent.SpellHit, PassiveEffectKind.ProcOnHit, cooldown: 1f) } }),
            ("BurningFist", RarityTier.Gold, new P { Name = "Poing ardent", Description = "Les attaques brûlent : 0,8 % des PV max de la cible par seconde pendant 5 s, cumulable sans limite.",
                Triggers = new[] { T(PassiveEvent.BasicAttackHit, PassiveEffectKind.ApplyStatus).Status(new StatusApplication { Kind = StatusKind.Burn, TargetMaxHealthRatio = 0.008f, Duration = 5f, MaxStacks = 99 }) } }),
            ("ArcaneMissiles", RarityTier.Gold, new P { Name = "Missiles arcaniques", Description = "Un sort qui touche tire 3 missiles sur les ennemis proches : 30 (+1 % des PV max de la cible) dégâts bruts. Recharge 1,5 s.",
                Triggers = new[] { T(PassiveEvent.SpellHit, PassiveEffectKind.Bolts, cooldown: 1.5f).Bolts(3, 7f, 30f, StatType.MagicAttack, 0f, DamageType.True, new Color(0.8f, 0.6f, 1f)).TargetHp(0.01f, DamageType.True) } }),
            ("DeadlyCadence", RarityTier.Gold, new P { Name = "Cadence mortelle", Description = "+25 % chances de critique. Chaque coup critique donne +6 % vitesse d'attaque, cumulable 10 fois (4 s).",
                Stats = new[] { B(StatType.CritChance, 0.25f) },
                Stacks = new StackSettings { Source = StackSource.PerCrit, Reset = ResetPolicy.AfterDuration, Max = 10, Duration = 4f, PerStack = new[] { Pct(StatType.AttackSpeed, 0.06f) } } }),
            ("SecondWind", RarityTier.Gold, new P { Name = "Second souffle", Description = "Toutes les 10 s, tes délais de récupération sont réduits de 4 s.",
                Triggers = new[] { T(PassiveEvent.Periodic, PassiveEffectKind.ReduceCooldowns, cooldown: 10f).Set(amount: 4f) } }),
            ("Clamor", RarityTier.Gold, new P { Name = "Clameur", Description = "Chaque sort lancé rend 20 PV (+2 % des PV max).",
                Triggers = new[] { T(PassiveEvent.SpellCast, PassiveEffectKind.Heal).Set(amount: 20f, stat: StatType.MaxHealth, ratio: 0.02f) } }),
            ("IronWill", RarityTier.Gold, new P { Name = "Volonté de fer", Description = "Touché sous 60 % de PV : rend 30 % des PV max. Recharge 45 s.",
                Triggers = new[] { T(PassiveEvent.DamageTaken, PassiveEffectKind.Heal, cooldown: 45f).Below(0.6f).Set(stat: StatType.MaxHealth, ratio: 0.3f) } }),
            ("HuntersThrill", RarityTier.Gold, new P { Name = "Frisson de la chasse", Description = "Chaque kill donne +50 % vitesse de déplacement et +30 % vitesse d'attaque pendant 3 s.",
                Triggers = new[] { T(PassiveEvent.Kill, PassiveEffectKind.TimedStats).Timed(3f, Pct(StatType.MoveSpeed, 0.5f), Pct(StatType.AttackSpeed, 0.3f)) } }),
            ("ReturningBlade", RarityTier.Gold, new P { Name = "Lame de retour", Description = "Toutes les 6 s, une lame frappe l'ennemi le plus proche : 80 (+80 % AD) dégâts physiques.",
                Triggers = new[] { T(PassiveEvent.Periodic, PassiveEffectKind.Bolts, cooldown: 6f).Bolts(1, 9f, 80f, StatType.PhysicalAttack, 0.8f, DamageType.Physical, new Color(1f, 0.85f, 0.5f)) } }),
            ("WellPracticed", RarityTier.Gold, new P { Name = "Geste répété", Description = "+100 accélération sur ton premier sort (Q)." }.Haste(100f, 0f, 0f, 0f)),
            ("SecondNature", RarityTier.Gold, new P { Name = "Seconde nature", Description = "+100 accélération sur ton deuxième sort (W)." }.Haste(0f, 100f, 0f, 0f)),
            ("ThirdEye", RarityTier.Gold, new P { Name = "Troisième œil", Description = "+100 accélération sur ton troisième sort (E)." }.Haste(0f, 0f, 100f, 0f)),
            ("Fury", RarityTier.Gold, null),
            ("StaticShock", RarityTier.Gold, null),
            ("ArcaneBarrier", RarityTier.Gold, null),

            // ---- Prismatic ----
            ("RunawaySorcery", RarityTier.Prismatic, new P { Name = "Sorcellerie emballée", Description = "Chaque sort lancé donne +12 accélération, sans limite, jusqu'à la fin de la vague.",
                Stacks = new StackSettings { Source = StackSource.PerSpellCast, Reset = ResetPolicy.EndOfWave, PerStack = new[] { B(StatType.AbilityHaste, 12f) } } }),
            ("LavaChannel", RarityTier.Prismatic, new P { Name = "Canal de lave", Description = "Tes sorts brûlent (20 + 15 % AP par seconde pendant 5 s) et chaque touche réduit tes délais de récupération de 0,4 s.",
                Triggers = new[] {
                    T(PassiveEvent.SpellHit, PassiveEffectKind.ApplyStatus).Status(new StatusApplication { Kind = StatusKind.Burn, Power = 20f, ScalingStat = StatType.MagicAttack, Ratio = 0.15f, Duration = 5f, MaxStacks = 3 }),
                    T(PassiveEvent.SpellHit, PassiveEffectKind.ReduceCooldowns).Set(amount: 0.4f),
                } }),
            ("Orbit", RarityTier.Prismatic, new P { Name = "Orbite", Description = "Des étoiles tournent autour de toi : toutes les 0,75 s, 25 (+20 % AD) dégâts magiques aux ennemis à portée d'attaque." }
                .Aura(5.5f, 0.75f, 25f, StatType.PhysicalAttack, 0.2f, DamageType.Magic)),
            ("ArcLightning", RarityTier.Prismatic, new P { Name = "Arc électrique", Description = "Tes dégâts frappent aussi l'ennemi le plus proche de la cible pour 30 % en dégâts bruts.",
                Triggers = new[] { T(PassiveEvent.DamageDealt, PassiveEffectKind.Bolts, cooldown: 0.3f).Bolts(1, 8f, 0f, StatType.PhysicalAttack, 0f, DamageType.True, new Color(0.7f, 0.9f, 1f)).Dealt(0.3f) } }),
            ("Ambidextrous", RarityTier.Prismatic, new P { Name = "Ambidextre", Description = "+25 % vitesse d'attaque. Les attaques tirent un trait de plus sur un autre ennemi : 50 % AD dégâts physiques.",
                Stats = new[] { Pct(StatType.AttackSpeed, 0.25f) },
                Triggers = new[] { T(PassiveEvent.BasicAttackHit, PassiveEffectKind.Bolts).Bolts(1, 6f, 0f, StatType.PhysicalAttack, 0.5f, DamageType.Physical, new Color(0.6f, 0.75f, 1f)) } }),
            ("QuakeStep", RarityTier.Prismatic, new P { Name = "Pas sismique", Description = "Après un blink, la terre explose autour de toi : 140 (+100 % AD) dégâts physiques à moins de 3 m.",
                Triggers = new[] { T(PassiveEvent.Blink, PassiveEffectKind.Nova).Nova(3f, new Color(0.85f, 0.65f, 0.35f, 0.85f), default).Damage(140f, StatType.PhysicalAttack, 1f, DamageType.Physical) } }),
            ("RuneTrail", RarityTier.Prismatic, new P { Name = "Traînée runique", Description = "Un sort qui touche fait exploser la zone autour de la cible : 100 (+30 % AP) dégâts magiques. Recharge 2 s.",
                Triggers = new[] { T(PassiveEvent.SpellHit, PassiveEffectKind.Splash, cooldown: 2f).Damage(100f, StatType.MagicAttack, 0.3f, DamageType.Magic).Area(3f) } }),
            ("AllOrNothing", RarityTier.Prismatic, new P { Name = "Tout ou rien", Description = "−30 % PV max, mais +25 % de dégâts.",
                Stats = new[] { B(StatType.MaxHealth, -0.3f, ModifierType.PercentMult) } }.Damage(0.25f, 0f)),
            ("Apotheosis", RarityTier.Prismatic, new P { Name = "Apothéose", Description = "Lancer ton ultime donne un bouclier de 50 % des PV max, +20 % omnivampirisme et +30 % vitesse pendant 7,5 s. Recharge 20 s.",
                Triggers = new[] {
                    T(PassiveEvent.SpellCast, PassiveEffectKind.Shield, cooldown: 20f).Ult().Set(stat: StatType.MaxHealth, ratio: 0.5f, duration: 7.5f),
                    T(PassiveEvent.SpellCast, PassiveEffectKind.TimedStats, cooldown: 20f).Ult().Timed(7.5f, B(StatType.Omnivamp, 0.2f), Pct(StatType.MoveSpeed, 0.3f)),
                } }),
            ("EternalMoment", RarityTier.Prismatic, new P { Name = "Instant d'éternité", Description = "Lancer ton ultime te rend invulnérable pendant 2 s. Recharge 8 s.",
                Triggers = new[] { T(PassiveEvent.SpellCast, PassiveEffectKind.Shield, cooldown: 8f).Ult().Set(amount: 99999f, duration: 2f) } }),
            ("RunicFist", RarityTier.Prismatic, new P { Name = "Poing runique", Description = "Les attaques réduisent tes délais de récupération de 1,25 s.",
                Triggers = new[] { T(PassiveEvent.BasicAttackHit, PassiveEffectKind.ReduceCooldowns).Set(amount: 1.25f) } }),
            ("Simmer", RarityTier.Prismatic, new P { Name = "Mijotage", Description = "Brûle les ennemis à moins de 5 m : 1 % de tes PV max par seconde." }
                .Aura(5f, 1f, 0f, StatType.MaxHealth, 0.01f, DamageType.Magic)),
            ("GoldsmithGlove", RarityTier.Prismatic, null),
            ("Phoenix", RarityTier.Prismatic, null),
            ("SealCaller", RarityTier.Prismatic, null),
        };

        public static void Run() {
            foreach (string png in Directory.GetFiles("Assets/GraphicAssets/Sprites/Augments", "Augment_*.png")) EconomySetup.ConfigureIcon(png.Replace('\\', '/'));
            var catalog = AssetDatabase.LoadAssetAtPath<AugmentCatalog>(PassiveSetup.AugmentCatalogPath);
            if (catalog == null) return;
            var list = new List<Object>();
            foreach ((string id, RarityTier rarity, P passive) in Augments) {
                string path = $"{Dir}/Augment_{id}.asset";
                var augment = AssetDatabase.LoadAssetAtPath<AugmentDefinition>(path);
                if (augment == null && passive != null) {
                    passive.Id = id;
                    augment = ScriptableObject.CreateInstance<AugmentDefinition>();
                    AssetDatabase.CreateAsset(augment, path);
                    var so = new SerializedObject(augment);
                    so.FindProperty("rarity").enumValueIndex = (int)rarity;
                    so.FindProperty("passive").objectReferenceValue = PassiveSetup.Passive(passive, $"Augment_{id}");
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                if (augment == null) continue;
                if (augment.Passive != null && augment.Passive.Icon == null) {   // icons drawn after the first run
                    var icon = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/GraphicAssets/Sprites/Augments/Augment_{id}.png");
                    if (icon != null) {
                        var pso = new SerializedObject(augment.Passive);
                        pso.FindProperty("icon").objectReferenceValue = icon;
                        pso.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                if (augment.Rarity != rarity) {   // rarity of the table wins over the asset's
                    var so = new SerializedObject(augment);
                    so.FindProperty("rarity").enumValueIndex = (int)rarity;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                list.Add(augment);
            }
            var cso = new SerializedObject(catalog);
            SerializedProperty augments = cso.FindProperty("augments");
            augments.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++) augments.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            cso.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>Fluent helpers for the augment table.</summary>
    internal static class TriggerBuilder {
        public static PassiveTrigger Set(this PassiveTrigger t, float amount = 0f, StatType stat = StatType.MaxHealth, float ratio = 0f, float duration = 0f) {
            t.Amount = amount;
            t.AmountStat = stat;
            t.AmountRatio = ratio;
            if (duration > 0f) t.Duration = duration;
            return t;
        }
        public static PassiveTrigger Bolts(this PassiveTrigger t, int count, float radius, float damage, StatType stat, float ratio, DamageType type, Color color) {
            t.Count = count;
            t.Radius = radius;
            t.BoltColor = color;
            return t.Damage(damage, stat, ratio, type);
        }
        public static PassiveTrigger Damage(this PassiveTrigger t, float damage, StatType stat, float ratio, DamageType type) {
            t.Damage = damage;
            t.ScalingStat = stat;
            t.DamageRatio = ratio;
            t.DamageType = type;
            return t;
        }
        public static PassiveTrigger TargetHp(this PassiveTrigger t, float ratio, DamageType type) {
            t.TargetMaxHealthRatio = ratio;
            t.DamageType = type;
            return t;
        }
        public static PassiveTrigger Nova(this PassiveTrigger t, float radius, Color color, StatusApplication status) {
            t.Radius = radius;
            t.BoltColor = color;
            t.Status = status;
            return t;
        }
        public static PassiveTrigger Area(this PassiveTrigger t, float radius) {
            t.Radius = radius;
            return t;
        }
        public static PassiveTrigger Status(this PassiveTrigger t, StatusApplication status) {
            t.Status = status;
            return t;
        }
        public static PassiveTrigger Timed(this PassiveTrigger t, float duration, params StatBonus[] stats) {
            t.Duration = duration;
            t.Stats = stats;
            return t;
        }
        public static PassiveTrigger Below(this PassiveTrigger t, float healthShare) {
            t.HealthBelow = healthShare;
            return t;
        }
        public static PassiveTrigger Ult(this PassiveTrigger t) {
            t.UltimateOnly = true;
            return t;
        }
        public static PassiveTrigger Dealt(this PassiveTrigger t, float ratio) {
            t.DamageDealtRatio = ratio;
            return t;
        }

        public static PassiveSetup.P Damage(this PassiveSetup.P p, float outgoing, float targetBelow) {
            p.DamageMods = new DamageModifiers { OutgoingPercent = outgoing, TargetHealthBelow = targetBelow };
            return p;
        }
        public static PassiveSetup.P Haste(this PassiveSetup.P p, params float[] perAbility) {
            p.AbilityHaste = perAbility;
            return p;
        }
        public static PassiveSetup.P Aura(this PassiveSetup.P p, float radius, float interval, float damage, StatType stat, float ratio, DamageType type) {
            p.Aura = new AuraSettings { Radius = radius, Interval = interval, Damage = damage, ScalingStat = stat, Ratio = ratio, DamageType = type };
            return p;
        }
    }
}
#endif
