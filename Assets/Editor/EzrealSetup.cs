#if UNITY_EDITOR
using System.IO;
using Features.Characters;
using Features.Economy;
using Features.Passives;
using Features.Progression;
using Features.Spells;
using Features.Stats;
using UnityEditor;
using UnityEngine;

namespace EditorTools {
    /// <summary>
    /// The test champion, Ezreal (design doc §7, §8), called by <see cref="ProjectSetup"/>: his four
    /// abilities (Q Tir mystique, W Flux d'essence, E Transfert arcanique, R Barrage de précision)
    /// with LoL's rank values, his passive (Sursaut de puissance), Flash on F, LoL base stats and
    /// growth (ranges and speeds / 100), and a skill point on every level up to 18.
    /// The hero asset is turned into Ezreal once, while it has no abilities: a champion edited by
    /// hand is kept. Abilities are created once, never overwritten.
    /// </summary>
    public static class EzrealSetup {
        private const string Dir = "Assets/Data/Characters/TestHero";
        private const string SpellsDir = "Assets/Data/Spells/Ezreal";
        private const string IconsDir = "Assets/GraphicAssets/Sprites/Spells";
        private static readonly int[] BasicRanks = { 1, 3, 5, 7, 9 };
        private static readonly int[] UltimateRanks = { 6, 11, 16 };

        public static void Run(SpellDefinition flash) {
            foreach (string png in Directory.GetFiles(IconsDir, "Ezreal_*.png")) EconomySetup.ConfigureIcon(png.Replace('\\', '/'));
            SpellDefinition q = MysticShot(), w = EssenceFlux(), e = ArcaneShift(), r = TrueshotBarrage();
            GiveEverySkillPoint();

            var hero = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ProjectSetup.TestHeroPath);
            if (hero == null || hero.Abilities.Count > 0) return;

            var so = new SerializedObject(hero);
            so.FindProperty("displayName").stringValue = "Ezreal";
            SerializedProperty abilities = so.FindProperty("abilities");
            abilities.arraySize = 4;
            abilities.GetArrayElementAtIndex(0).objectReferenceValue = q;
            abilities.GetArrayElementAtIndex(1).objectReferenceValue = w;
            abilities.GetArrayElementAtIndex(2).objectReferenceValue = e;
            abilities.GetArrayElementAtIndex(3).objectReferenceValue = r;
            so.FindProperty("summonerF").objectReferenceValue = flash;
            so.FindProperty("passive").objectReferenceValue = Passive();
            so.ApplyModifiedPropertiesWithoutUndo();

            WriteStats(hero.BaseStats);
            WriteGrowth(hero.GrowthPerLevel);
        }

        // Q: skillshot, on-hit effects, every hit takes 1.5 s off all his cooldowns
        private static SpellDefinition MysticShot() => Ability("Ezreal_Q", "Tir mystique",
            "Tire un projectile qui inflige 20/45/70/95/120 (+130 % AD) (+15 % AP) dégâts physiques au premier ennemi touché et applique les effets à l'impact. Toucher réduit les délais de récupération d'Ezreal de 1,5 s.",
            new Color(0.55f, 0.85f, 1f), BasicRanks, so => {
                so.FindProperty("targeting").enumValueIndex = (int)TargetingType.Skillshot;
                so.FindProperty("range").floatValue = 12f;
                so.FindProperty("radius").floatValue = 0.6f;
                so.FindProperty("projectileSpeed").floatValue = 20f;
                so.FindProperty("castTime").floatValue = 0.25f;
                so.FindProperty("kind").enumValueIndex = (int)SpellKind.Physical;
                SpellAssets.SetFloats(so.FindProperty("baseDamage"), 20f, 45f, 70f, 95f, 120f);
                SpellAssets.SetFloats(so.FindProperty("cooldowns"), 5.5f, 5.25f, 5f, 4.75f, 4.5f);
                SpellAssets.SetFloats(so.FindProperty("manaCosts"), 28f, 31f, 34f, 37f, 40f);
                so.FindProperty("adRatio").floatValue = 1.3f;
                so.FindProperty("apRatio").floatValue = 0.15f;
                so.FindProperty("appliesOnHit").boolValue = true;
                so.FindProperty("cooldownRefundOnHit").floatValue = 1.5f;
            });

        // W: marks the first enemy hit; Ezreal's next attack or other spell on it detonates the mark
        private static SpellDefinition EssenceFlux() => Ability("Ezreal_W", "Flux d'essence",
            "Tire un orbe qui s'accroche au premier ennemi touché pendant 4 s. Une attaque ou un autre sort d'Ezreal sur la cible le fait exploser : 80/135/190/245/300 (+60 % AD bonus) (+70 % AP) dégâts magiques, et rend 60/70/80/90/100 mana.",
            new Color(1f, 0.8f, 0.35f), BasicRanks, so => {
                so.FindProperty("targeting").enumValueIndex = (int)TargetingType.Skillshot;
                so.FindProperty("range").floatValue = 12f;
                so.FindProperty("radius").floatValue = 0.8f;
                so.FindProperty("projectileSpeed").floatValue = 17f;
                so.FindProperty("castTime").floatValue = 0.25f;
                so.FindProperty("kind").enumValueIndex = (int)SpellKind.Magic;
                SpellAssets.SetFloats(so.FindProperty("baseDamage"), 80f, 135f, 190f, 245f, 300f);
                SpellAssets.SetFloats(so.FindProperty("cooldowns"), 8f);
                SpellAssets.SetFloats(so.FindProperty("manaCosts"), 50f);
                so.FindProperty("bonusAdRatio").floatValue = 0.6f;
                so.FindProperty("apRatio").floatValue = 0.7f;
                SerializedProperty mark = so.FindProperty("mark");
                mark.FindPropertyRelative("Enabled").boolValue = true;
                mark.FindPropertyRelative("Duration").floatValue = 4f;
                SpellAssets.SetFloats(mark.FindPropertyRelative("ManaRefund"), 60f, 70f, 80f, 90f, 100f);
            });

        // E: blink, then a homing bolt at the nearest enemy (a marked one first)
        private static SpellDefinition ArcaneShift() => Ability("Ezreal_E", "Transfert arcanique",
            "Se téléporte vers le curseur puis tire un trait sur l'ennemi le plus proche (en priorité celui qui porte le Flux d'essence) : 80/130/180/230/280 (+50 % AD bonus) (+75 % AP) dégâts magiques.",
            new Color(0.75f, 0.6f, 1f), BasicRanks, so => {
                so.FindProperty("targeting").enumValueIndex = (int)TargetingType.GroundPoint;
                so.FindProperty("range").floatValue = 4.75f;
                so.FindProperty("radius").floatValue = 0.5f;
                so.FindProperty("castTime").floatValue = 0.25f;
                so.FindProperty("blink").boolValue = true;
                so.FindProperty("blinkBoltRadius").floatValue = 7.5f;
                so.FindProperty("projectileSpeed").floatValue = 20f;
                so.FindProperty("kind").enumValueIndex = (int)SpellKind.Magic;
                SpellAssets.SetFloats(so.FindProperty("baseDamage"), 80f, 130f, 180f, 230f, 280f);
                SpellAssets.SetFloats(so.FindProperty("cooldowns"), 26f, 23f, 20f, 17f, 14f);
                SpellAssets.SetFloats(so.FindProperty("manaCosts"), 90f);
                so.FindProperty("bonusAdRatio").floatValue = 0.5f;
                so.FindProperty("apRatio").floatValue = 0.75f;
            });

        // R: 1 s channel, then a wide wave that goes through everything on a long line
        private static SpellDefinition TrueshotBarrage() => Ability("Ezreal_R", "Barrage de précision",
            "Après 1 s de canalisation, tire une immense vague d'énergie qui traverse tout sur son passage : 350/550/750 (+100 % AD bonus) (+90 % AP) dégâts magiques.",
            new Color(1f, 0.9f, 0.45f), UltimateRanks, so => {
                so.FindProperty("targeting").enumValueIndex = (int)TargetingType.Skillshot;
                so.FindProperty("range").floatValue = 30f;
                so.FindProperty("radius").floatValue = 1.6f;
                so.FindProperty("projectileSpeed").floatValue = 20f;
                so.FindProperty("castTime").floatValue = 1f;
                so.FindProperty("pierce").boolValue = true;
                so.FindProperty("kind").enumValueIndex = (int)SpellKind.Magic;
                SpellAssets.SetFloats(so.FindProperty("baseDamage"), 350f, 550f, 750f);
                SpellAssets.SetFloats(so.FindProperty("cooldowns"), 120f, 105f, 90f);
                SpellAssets.SetFloats(so.FindProperty("manaCosts"), 100f);
                so.FindProperty("bonusAdRatio").floatValue = 1f;
                so.FindProperty("apRatio").floatValue = 0.9f;
            });

        private static SpellDefinition Ability(string id, string name, string description, Color color, int[] ranks,
                                               System.Action<SerializedObject> configure) {
            string path = $"{SpellsDir}/Spell_{id}.asset";
            var spell = AssetDatabase.LoadAssetAtPath<SpellDefinition>(path);
            if (spell != null) return spell;
            Directory.CreateDirectory(SpellsDir);
            spell = ScriptableObject.CreateInstance<SpellDefinition>();
            AssetDatabase.CreateAsset(spell, path);
            var so = new SerializedObject(spell);
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsDir}/{id}.png");
            so.FindProperty("color").colorValue = color;
            SpellAssets.SetInts(so.FindProperty("rankUnlockLevels"), ranks);
            configure(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            return spell;
        }

        // Rising Spell Force: each spell hit gives attack speed, 5 stacks, 6 s
        private static PassiveDefinition Passive() => PassiveSetup.Passive(new PassiveSetup.P {
            Id = "Ezreal_RisingSpellForce", Name = "Sursaut de puissance",
            Description = "Toucher un ennemi avec un sort donne +10 % vitesse d'attaque pendant 6 s, cumulable 5 fois.",
            Stacks = new StackSettings { Source = StackSource.PerSpellHit, Reset = ResetPolicy.AfterDuration, Max = 5, Duration = 6f,
                                         PerStack = new[] { new StatBonus(StatType.AttackSpeed, ModifierType.PercentAdd, 0.1f) } },
        }, null, AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsDir}/Ezreal_Passive.png"));

        // LoL base stats (HP5 / MP5 per second, distances and speeds / 100). Mana regen above LoL's
        // 1.7/s: hordes ask for more spells than a lane (provisional).
        private static void WriteStats(StatBlock block) {
            if (block == null) return;
            WriteList(block, "entries", new (StatType, float)[] {
                (StatType.MaxHealth, 600f), (StatType.HealthRegen, 0.8f),
                (StatType.MaxMana, 375f), (StatType.ManaRegen, 3f),
                (StatType.PhysicalAttack, 62f), (StatType.MagicAttack, 0f),
                (StatType.AttackSpeed, 0.625f), (StatType.AttackRange, 5.5f),
                (StatType.CritDamage, 1.75f), (StatType.Armor, 24f), (StatType.MagicResist, 30f),
                (StatType.MoveSpeed, 5f), (StatType.HealShieldPower, 1f),
            }, (e, v) => {
                e.FindPropertyRelative("Type").enumValueIndex = (int)v.Item1;
                e.FindPropertyRelative("Value").floatValue = v.Item2;
            });
        }

        private static void WriteGrowth(StatGrowth growth) {
            if (growth == null) return;
            WriteList(growth, "entries", new (StatType, float, ModifierType)[] {
                (StatType.MaxHealth, 102f, ModifierType.Flat), (StatType.HealthRegen, 0.13f, ModifierType.Flat),
                (StatType.MaxMana, 70f, ModifierType.Flat), (StatType.ManaRegen, 0.15f, ModifierType.Flat),
                (StatType.PhysicalAttack, 2.5f, ModifierType.Flat), (StatType.AttackSpeed, 0.025f, ModifierType.PercentAdd),
                (StatType.Armor, 4.7f, ModifierType.Flat), (StatType.MagicResist, 1.3f, ModifierType.Flat),
            }, (e, v) => {
                e.FindPropertyRelative("Stat").enumValueIndex = (int)v.Item1;
                e.FindPropertyRelative("GainPerLevel").floatValue = v.Item2;
                e.FindPropertyRelative("Type").enumValueIndex = (int)v.Item3;
            });
        }

        // LoL: a skill point on every level (5 + 5 + 5 + 3 = 18)
        private static void GiveEverySkillPoint() {
            var curve = AssetDatabase.LoadAssetAtPath<ExperienceCurve>(ProjectSetup.ExperienceCurvePath);
            if (curve == null || curve.LastSkillPointLevel >= curve.MaxLevel) return;
            var so = new SerializedObject(curve);
            so.FindProperty("lastSkillPointLevel").intValue = curve.MaxLevel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WriteList<T>(Object target, string field, T[] values, System.Action<SerializedProperty, T> write) {
            var so = new SerializedObject(target);
            SerializedProperty list = so.FindProperty(field);
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) write(list.GetArrayElementAtIndex(i), values[i]);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
