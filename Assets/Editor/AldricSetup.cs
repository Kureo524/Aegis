#if UNITY_EDITOR
using System.IO;
using Features.Characters;
using Features.Combat;
using Features.Economy;
using Features.Passives;
using Features.Progression;
using Features.Spells;
using Features.Stats;
using UnityEditor;
using UnityEngine;

namespace EditorTools {
    /// <summary>
    /// The second champion, Aldric, le Gardien de l'aurore (design doc §7.10), called by
    /// <see cref="ProjectSetup"/>. An old frost knight who fights in melee, takes the hits and freezes
    /// the horde around him:
    /// - Passive, Givre mordant: every third basic attack cleaves around its target and slows it.
    /// - Q Taille boréale: a wide sweep in front of him that shreds armor.
    /// - W Rempart de givre: a shield, armor and magic resist, and a burst of frost around him (slow).
    /// - E Bond polaire: leaps to the cursor and roots the enemies where he lands.
    /// - R Couronne d'aurore: after a short wind-up, the aurora bursts around him: heavy damage, stun,
    ///   then attack speed.
    /// His assets live in Assets/Data/Characters/Aldric; abilities, passive and stats are rewritten on
    /// every run (the code is the reference).
    /// </summary>
    public static class AldricSetup {
        public const string Name = "Aldric";
        private const string Dir = "Assets/Data/Characters/Aldric";
        public const string DefinitionPath = Dir + "/Char_Aldric.asset";
        private const string SpellsDir = "Assets/Data/Spells/Aldric";
        private const string IconsDir = "Assets/GraphicAssets/Sprites/Spells";

        private static readonly string[] Recommended = {
            "IronBoots", "TriadSigil", "IronCleaver", "TitanHeart", "DawnHammer", "OathGauntlet", "ThunderAxe", "GlacierPlate",
        };

        public static CharacterDefinition Run(SpellDefinition flash) {
            foreach (string png in Directory.GetFiles(IconsDir, "Aldric_*.png")) EconomySetup.ConfigureIcon(png.Replace('\\', '/'));
            SpellDefinition q = BorealCut(), w = FrostRampart(), e = PolarLeap(), r = AuroraCrown();

            Directory.CreateDirectory(Dir);
            var stats = Asset<StatBlock>($"{Dir}/StatBlock_Aldric.asset");
            var growth = Asset<StatGrowth>($"{Dir}/StatGrowth_Aldric.asset");
            var attack = Asset<BasicAttackDefinition>($"{Dir}/BasicAttack_Aldric.asset");
            var aso = new SerializedObject(attack);
            aso.FindProperty("windupPercent").floatValue = 0.3f;   // a heavy swing, no projectile: melee
            aso.FindProperty("projectilePrefab").objectReferenceValue = null;
            aso.FindProperty("damageRatio").floatValue = 1f;
            aso.ApplyModifiedPropertiesWithoutUndo();
            var gso = new SerializedObject(growth);
            gso.FindProperty("mode").enumValueIndex = (int)GrowthMode.Curved;
            gso.ApplyModifiedPropertiesWithoutUndo();

            var hero = Asset<CharacterDefinition>(DefinitionPath);
            var so = new SerializedObject(hero);
            so.FindProperty("displayName").stringValue = Name;
            so.FindProperty("epithet").stringValue = "le Gardien de l'aurore";
            so.FindProperty("summary").stringValue = "Vieux chevalier du givre au corps à corps : encaisse derrière son rempart, bondit au milieu de la horde, l'enracine et la gèle, puis fait éclater l'aurore autour de lui. Solide, mais il doit aller au contact.";
            so.FindProperty("faction").enumValueIndex = (int)Faction.Player;
            so.FindProperty("baseStats").objectReferenceValue = stats;
            so.FindProperty("growthPerLevel").objectReferenceValue = growth;
            so.FindProperty("basicAttack").objectReferenceValue = attack;
            SerializedProperty abilities = so.FindProperty("abilities");
            abilities.arraySize = 4;
            abilities.GetArrayElementAtIndex(0).objectReferenceValue = q;
            abilities.GetArrayElementAtIndex(1).objectReferenceValue = w;
            abilities.GetArrayElementAtIndex(2).objectReferenceValue = e;
            abilities.GetArrayElementAtIndex(3).objectReferenceValue = r;
            so.FindProperty("summonerF").objectReferenceValue = flash;
            so.FindProperty("passive").objectReferenceValue = Passive();
            // A bit wider than Vesna: a knight in armor
            so.FindProperty("bodySize").vector2Value = new Vector2(0.9f, so.FindProperty("bodySize").vector2Value.y);
            HeroSetup.SetRecommended(so, Recommended);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Melee bruiser: sturdy, little mana, hits hard and often
            HeroSetup.WriteStats(stats, new (StatType, float)[] {
                (StatType.MaxHealth, 640f), (StatType.HealthRegen, 1.2f),
                (StatType.MaxMana, 300f), (StatType.ManaRegen, 2.6f),
                (StatType.PhysicalAttack, 64f), (StatType.MagicAttack, 0f),
                (StatType.AttackSpeed, 0.7f), (StatType.AttackRange, 1.8f),
                (StatType.CritDamage, 1.75f), (StatType.Armor, 30f), (StatType.MagicResist, 30f),
                (StatType.MoveSpeed, 5.2f), (StatType.HealShieldPower, 1f),
            });
            HeroSetup.WriteGrowth(growth, new (StatType, float, ModifierType)[] {
                (StatType.MaxHealth, 105f, ModifierType.Flat), (StatType.HealthRegen, 0.12f, ModifierType.Flat),
                (StatType.MaxMana, 45f, ModifierType.Flat), (StatType.ManaRegen, 0.12f, ModifierType.Flat),
                (StatType.PhysicalAttack, 3.2f, ModifierType.Flat), (StatType.AttackSpeed, 0.022f, ModifierType.PercentAdd),
                (StatType.Armor, 4.8f, ModifierType.Flat), (StatType.MagicResist, 1.8f, ModifierType.Flat),
            });
            EditorUtility.SetDirty(hero);
            return hero;
        }

        private static T Asset<T>(string path) where T : ScriptableObject {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static SpellDefinition Ability(string id, string name, string description, Color color, int[] ranks,
                                               System.Action<SerializedObject> configure) =>
            HeroSetup.Ability(SpellsDir, id, name, description, color, ranks, configure);

        // Q: wide sweep in front of him, physical, shreds armor
        private static SpellDefinition BorealCut() => Ability("Aldric_Q", "Taille boréale",
            "Balaie l'avant d'Aldric de sa lame de givre : 45/75/105/135/165 (+110 % AD) dégâts physiques aux ennemis touchés, qui perdent 20 % d'armure et de résistance magique pendant 3 s.",
            new Color(0.45f, 0.85f, 1f), HeroSetup.BasicRanks, so => {
                so.FindProperty("targeting").enumValueIndex = (int)TargetingType.Cone;
                so.FindProperty("range").floatValue = 3.6f;
                so.FindProperty("coneAngle").floatValue = 120f;
                so.FindProperty("radius").floatValue = 0.5f;
                so.FindProperty("castTime").floatValue = 0.25f;
                so.FindProperty("kind").enumValueIndex = (int)SpellKind.Physical;
                SpellAssets.SetFloats(so.FindProperty("baseDamage"), 45f, 75f, 105f, 135f, 165f);
                SpellAssets.SetFloats(so.FindProperty("cooldowns"), 6f, 5.5f, 5f, 4.5f, 4f);
                SpellAssets.SetFloats(so.FindProperty("manaCosts"), 20f, 22f, 24f, 26f, 28f);
                so.FindProperty("adRatio").floatValue = 1.1f;
                HeroSetup.Statuses(so, new StatusApplication { Kind = StatusKind.Shred, Power = 0.2f, Duration = 3f, MaxStacks = 1 });
            });

        // W: shield + resistances, and a frost burst around him that slows
        private static SpellDefinition FrostRampart() => Ability("Aldric_W", "Rempart de givre",
            "Dresse un rempart de givre : bouclier de 80 pendant 3 s, +20 armure et résistance magique pendant 3 s, et une onde de froid autour de lui (40/65/90/115/140 (+60 % AD bonus) dégâts magiques, ralentit de 35 % pendant 1,5 s).",
            new Color(0.55f, 0.75f, 1f), HeroSetup.BasicRanks, so => {
                so.FindProperty("targeting").enumValueIndex = (int)TargetingType.Self;
                so.FindProperty("radius").floatValue = 3f;
                so.FindProperty("castTime").floatValue = 0.15f;
                so.FindProperty("canMoveWhileCasting").boolValue = true;
                so.FindProperty("kind").enumValueIndex = (int)SpellKind.Magic;
                SpellAssets.SetFloats(so.FindProperty("baseDamage"), 40f, 65f, 90f, 115f, 140f);
                SpellAssets.SetFloats(so.FindProperty("cooldowns"), 16f, 15f, 14f, 13f, 12f);
                SpellAssets.SetFloats(so.FindProperty("manaCosts"), 50f);
                so.FindProperty("bonusAdRatio").floatValue = 0.6f;
                SerializedProperty sustain = so.FindProperty("selfSustain");
                sustain.FindPropertyRelative("Shield").floatValue = 80f;
                sustain.FindPropertyRelative("ShieldDuration").floatValue = 3f;
                SerializedProperty buffs = so.FindProperty("buffs");
                buffs.arraySize = 2;
                Buff(buffs.GetArrayElementAtIndex(0), StatType.Armor, 20f, ModifierType.Flat, 3f);
                Buff(buffs.GetArrayElementAtIndex(1), StatType.MagicResist, 20f, ModifierType.Flat, 3f);
                HeroSetup.Statuses(so, new StatusApplication { Kind = StatusKind.Slow, Power = 0.35f, Duration = 1.5f });
            });

        // E: leap to the cursor, roots where he lands
        private static SpellDefinition PolarLeap() => Ability("Aldric_E", "Bond polaire",
            "Bondit vers le point visé et retombe dans une gerbe de givre : 60/90/120/150/180 (+70 % AD bonus) dégâts physiques autour du point d'impact, et les ennemis sont enracinés 0,75 s.",
            new Color(0.65f, 0.55f, 1f), HeroSetup.BasicRanks, so => {
                so.FindProperty("targeting").enumValueIndex = (int)TargetingType.GroundPoint;
                so.FindProperty("range").floatValue = 6.5f;
                so.FindProperty("radius").floatValue = 2.2f;
                so.FindProperty("castTime").floatValue = 0.15f;
                so.FindProperty("blink").boolValue = true;
                so.FindProperty("kind").enumValueIndex = (int)SpellKind.Physical;
                SpellAssets.SetFloats(so.FindProperty("baseDamage"), 60f, 90f, 120f, 150f, 180f);
                SpellAssets.SetFloats(so.FindProperty("cooldowns"), 15f, 13.5f, 12f, 10.5f, 9f);
                SpellAssets.SetFloats(so.FindProperty("manaCosts"), 45f);
                so.FindProperty("bonusAdRatio").floatValue = 0.7f;
                HeroSetup.Statuses(so, new StatusApplication { Kind = StatusKind.Root, Duration = 0.75f });
            });

        // R: the aurora bursts around him: heavy damage, stun, then attack speed
        private static SpellDefinition AuroraCrown() => Ability("Aldric_R", "Couronne d'aurore",
            "Après 0,6 s, l'aurore éclate autour d'Aldric : 250/400/550 (+120 % AD bonus) dégâts magiques aux ennemis à moins de 5 m, étourdis 1 s ; Aldric gagne ensuite +50 % de vitesse d'attaque pendant 5 s.",
            new Color(0.6f, 0.5f, 1f), HeroSetup.UltimateRanks, so => {
                so.FindProperty("targeting").enumValueIndex = (int)TargetingType.Self;
                so.FindProperty("radius").floatValue = 5f;
                so.FindProperty("castTime").floatValue = 0.6f;
                so.FindProperty("kind").enumValueIndex = (int)SpellKind.Magic;
                SpellAssets.SetFloats(so.FindProperty("baseDamage"), 250f, 400f, 550f);
                SpellAssets.SetFloats(so.FindProperty("cooldowns"), 90f, 75f, 60f);
                SpellAssets.SetFloats(so.FindProperty("manaCosts"), 100f);
                so.FindProperty("bonusAdRatio").floatValue = 1.2f;
                SerializedProperty buffs = so.FindProperty("buffs");
                buffs.arraySize = 1;
                Buff(buffs.GetArrayElementAtIndex(0), StatType.AttackSpeed, 0.5f, ModifierType.PercentAdd, 5f);
                HeroSetup.Statuses(so, new StatusApplication { Kind = StatusKind.Stun, Duration = 1f });
            });

        private static void Buff(SerializedProperty b, StatType stat, float value, ModifierType type, float duration) {
            b.FindPropertyRelative("Stat").enumValueIndex = (int)stat;
            b.FindPropertyRelative("Type").enumValueIndex = (int)type;
            b.FindPropertyRelative("Value").floatValue = value;
            b.FindPropertyRelative("Duration").floatValue = duration;
        }

        // Givre mordant: every third basic attack cleaves around the target and slows it
        private static PassiveDefinition Passive() => PassiveSetup.Passive(new PassiveSetup.P {
            Id = "Aldric_BitingFrost", Name = "Givre mordant",
            Description = "Toutes les 3 attaques, la lame d'Aldric gèle : 20 (+60 % AD) dégâts physiques aux ennemis à moins de 2 m de la cible, qui est ralentie de 30 % pendant 1 s.",
            Triggers = new[] {
                new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 3, Chance = 1f, Effect = PassiveEffectKind.Splash,
                                     Radius = 2f, Damage = 20f, ScalingStat = StatType.PhysicalAttack, DamageRatio = 0.6f, DamageType = DamageType.Physical },
                new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 3, Chance = 1f, Effect = PassiveEffectKind.ApplyStatus,
                                     Status = new StatusApplication { Kind = StatusKind.Slow, Power = 0.3f, Duration = 1f } },
            },
        }, null, AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsDir}/Aldric_Passive.png"), overwrite: true);
    }
}
#endif
