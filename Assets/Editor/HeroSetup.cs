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
    /// The champion, Vesna, la Lanceuse de braises (design doc §7, §8), called by
    /// <see cref="ProjectSetup"/>. A ranged fire caster who chains spells into charged basic attacks:
    /// - Passive, Surchauffe: after a spell, her next basic attack explodes around its target and
    ///   sets it on fire.
    /// - Q Gerbe de braises: short cone of embers, slows.
    /// - W Bombe de cendres: a firebomb at the cursor, burns the area.
    /// - E Roulade: a short dodge roll, then a burst of attack speed (to cash in the passive).
    /// - R Pluie de braises: after a short delay, a rain of fire on a wide area: heavy damage, stun.
    /// Abilities, passive and stats are rewritten on every run (the code is the reference).
    /// </summary>
    public static class HeroSetup {
        public const string HeroName = "Vesna";
        private const string SpellsDir = "Assets/Data/Spells/Vesna";
        private const string IconsDir = "Assets/GraphicAssets/Sprites/Spells";
        private static readonly int[] BasicRanks = { 1, 3, 5, 7, 9 };
        private static readonly int[] UltimateRanks = { 6, 11, 16 };

        private static readonly string[] Recommended = {
            "DuelistGreaves", "TriadSigil", "WrathBlade", "GaleBow", "SunsteelBlade", "CrimsonChalice", "GiantsBane", "HungryAxe",
        };

        public static void Run(SpellDefinition flash) {
            foreach (string png in Directory.GetFiles(IconsDir, "Vesna_*.png")) EconomySetup.ConfigureIcon(png.Replace('\\', '/'));
            SpellDefinition q = EmberSpray(), w = AshBomb(), e = Roll(), r = EmberRain();
            GiveEverySkillPoint();

            var hero = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ProjectSetup.TestHeroPath);
            if (hero == null) return;
            var so = new SerializedObject(hero);
            so.FindProperty("displayName").stringValue = HeroName;
            SerializedProperty abilities = so.FindProperty("abilities");
            abilities.arraySize = 4;
            abilities.GetArrayElementAtIndex(0).objectReferenceValue = q;
            abilities.GetArrayElementAtIndex(1).objectReferenceValue = w;
            abilities.GetArrayElementAtIndex(2).objectReferenceValue = e;
            abilities.GetArrayElementAtIndex(3).objectReferenceValue = r;
            so.FindProperty("summonerF").objectReferenceValue = flash;
            so.FindProperty("passive").objectReferenceValue = Passive();
            // Shop "Recommended" tab: attack speed and spellblade to cash in Overheat, then damage
            var recommended = new System.Collections.Generic.List<ItemDefinition>();
            foreach (string id in Recommended) {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Data/Economy/Items/Item_{id}.asset");
                if (item != null) recommended.Add(item);
            }
            SerializedProperty list = so.FindProperty("recommendedItems");
            list.arraySize = recommended.Count;
            for (int i = 0; i < recommended.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = recommended[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            WriteStats(hero.BaseStats);
            WriteGrowth(hero.GrowthPerLevel);
        }

        // Q: cone in front of her, physical, slows
        private static SpellDefinition EmberSpray() => Ability("Vesna_Q", "Gerbe de braises",
            "Projette une gerbe de braises devant Vesna : 30/55/80/105/130 (+110 % AD) (+30 % AP) dégâts physiques aux ennemis du cône, ralentis de 25 % pendant 1 s.",
            new Color(1f, 0.55f, 0.2f), BasicRanks, so => {
                so.FindProperty("targeting").enumValueIndex = (int)TargetingType.Cone;
                so.FindProperty("range").floatValue = 5.5f;
                so.FindProperty("coneAngle").floatValue = 55f;
                so.FindProperty("radius").floatValue = 0.5f;
                so.FindProperty("castTime").floatValue = 0.2f;
                so.FindProperty("kind").enumValueIndex = (int)SpellKind.Physical;
                SpellAssets.SetFloats(so.FindProperty("baseDamage"), 30f, 55f, 80f, 105f, 130f);
                SpellAssets.SetFloats(so.FindProperty("cooldowns"), 5f, 4.5f, 4f, 3.5f, 3f);
                SpellAssets.SetFloats(so.FindProperty("manaCosts"), 25f, 28f, 31f, 34f, 37f);
                so.FindProperty("adRatio").floatValue = 1.1f;
                so.FindProperty("apRatio").floatValue = 0.3f;
                Statuses(so, new StatusApplication { Kind = StatusKind.Slow, Power = 0.25f, Duration = 1f });
            });

        // W: area at the cursor, magic, burns
        private static SpellDefinition AshBomb() => Ability("Vesna_W", "Bombe de cendres",
            "Lance une bombe qui explose au point visé : 70/110/150/190/230 (+40 % AD bonus) (+60 % AP) dégâts magiques dans la zone, puis les ennemis brûlent pendant 3 s (12 (+8 % AP) dégâts par seconde).",
            new Color(1f, 0.35f, 0.15f), BasicRanks, so => {
                so.FindProperty("targeting").enumValueIndex = (int)TargetingType.GroundPoint;
                so.FindProperty("range").floatValue = 9f;
                so.FindProperty("radius").floatValue = 2.6f;
                so.FindProperty("castTime").floatValue = 0.25f;
                so.FindProperty("kind").enumValueIndex = (int)SpellKind.Magic;
                SpellAssets.SetFloats(so.FindProperty("baseDamage"), 70f, 110f, 150f, 190f, 230f);
                SpellAssets.SetFloats(so.FindProperty("cooldowns"), 10f, 9.5f, 9f, 8.5f, 8f);
                SpellAssets.SetFloats(so.FindProperty("manaCosts"), 60f);
                so.FindProperty("bonusAdRatio").floatValue = 0.4f;
                so.FindProperty("apRatio").floatValue = 0.6f;
                Statuses(so, new StatusApplication { Kind = StatusKind.Burn, Power = 12f, ScalingStat = StatType.MagicAttack, Ratio = 0.08f, Duration = 3f, MaxStacks = 1 });
            });

        // E: short dodge towards the cursor, then attack speed
        private static SpellDefinition Roll() => Ability("Vesna_E", "Roulade",
            "Roule vers le curseur, puis gagne +40/50/60/70/80 % de vitesse d'attaque pendant 3 s.",
            new Color(1f, 0.8f, 0.4f), BasicRanks, so => {
                so.FindProperty("targeting").enumValueIndex = (int)TargetingType.GroundPoint;
                so.FindProperty("range").floatValue = 4f;
                so.FindProperty("radius").floatValue = 0.5f;
                so.FindProperty("castTime").floatValue = 0.1f;
                so.FindProperty("blink").boolValue = true;
                so.FindProperty("blinkBoltRadius").floatValue = 0f;
                so.FindProperty("kind").enumValueIndex = (int)SpellKind.Buff;
                SpellAssets.SetFloats(so.FindProperty("baseDamage"), 0f);
                SpellAssets.SetFloats(so.FindProperty("cooldowns"), 16f, 14.5f, 13f, 11.5f, 10f);
                SpellAssets.SetFloats(so.FindProperty("manaCosts"), 40f);
                so.FindProperty("adRatio").floatValue = 0f;
                so.FindProperty("bonusAdRatio").floatValue = 0f;
                so.FindProperty("apRatio").floatValue = 0f;
                SerializedProperty buffs = so.FindProperty("buffs");
                buffs.arraySize = 1;
                SerializedProperty b = buffs.GetArrayElementAtIndex(0);
                b.FindPropertyRelative("Stat").enumValueIndex = (int)StatType.AttackSpeed;
                b.FindPropertyRelative("Type").enumValueIndex = (int)ModifierType.PercentAdd;
                b.FindPropertyRelative("Value").floatValue = 0.4f;
                b.FindPropertyRelative("Duration").floatValue = 3f;
            });

        // R: delayed rain of fire on a wide area, stuns
        private static SpellDefinition EmberRain() => Ability("Vesna_R", "Pluie de braises",
            "Après 0,9 s, une pluie de feu s'abat sur une large zone : 300/475/650 (+100 % AD bonus) (+90 % AP) dégâts magiques, étourdit 1 s et brûle 3 s.",
            new Color(1f, 0.45f, 0.1f), UltimateRanks, so => {
                so.FindProperty("targeting").enumValueIndex = (int)TargetingType.GroundPoint;
                so.FindProperty("range").floatValue = 16f;
                so.FindProperty("radius").floatValue = 4.5f;
                so.FindProperty("castTime").floatValue = 0.9f;
                so.FindProperty("kind").enumValueIndex = (int)SpellKind.Magic;
                SpellAssets.SetFloats(so.FindProperty("baseDamage"), 300f, 475f, 650f);
                SpellAssets.SetFloats(so.FindProperty("cooldowns"), 100f, 85f, 70f);
                SpellAssets.SetFloats(so.FindProperty("manaCosts"), 100f);
                so.FindProperty("bonusAdRatio").floatValue = 1f;
                so.FindProperty("apRatio").floatValue = 0.9f;
                Statuses(so,
                    new StatusApplication { Kind = StatusKind.Stun, Duration = 1f },
                    new StatusApplication { Kind = StatusKind.Burn, Power = 20f, ScalingStat = StatType.MagicAttack, Ratio = 0.1f, Duration = 3f, MaxStacks = 1 });
            });

        private static void Statuses(SerializedObject so, params StatusApplication[] statuses) {
            SerializedProperty list = so.FindProperty("statuses");
            list.arraySize = statuses.Length;
            for (int i = 0; i < statuses.Length; i++) {
                SerializedProperty s = list.GetArrayElementAtIndex(i);
                s.FindPropertyRelative("Kind").enumValueIndex = (int)statuses[i].Kind;
                s.FindPropertyRelative("Duration").floatValue = statuses[i].Duration;
                s.FindPropertyRelative("Power").floatValue = statuses[i].Power;
                s.FindPropertyRelative("ScalingStat").enumValueIndex = (int)statuses[i].ScalingStat;
                s.FindPropertyRelative("Ratio").floatValue = statuses[i].Ratio;
                SerializedProperty max = s.FindPropertyRelative("MaxStacks");
                if (max != null) max.intValue = Mathf.Max(1, statuses[i].MaxStacks);
            }
        }

        private static SpellDefinition Ability(string id, string name, string description, Color color, int[] ranks,
                                               System.Action<SerializedObject> configure) {
            string path = $"{SpellsDir}/Spell_{id}.asset";
            var spell = AssetDatabase.LoadAssetAtPath<SpellDefinition>(path);
            if (spell == null) {
                Directory.CreateDirectory(SpellsDir);
                spell = ScriptableObject.CreateInstance<SpellDefinition>();
                AssetDatabase.CreateAsset(spell, path);
            }
            var so = new SerializedObject(spell);
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsDir}/{id}.png");
            so.FindProperty("color").colorValue = color;
            SpellAssets.SetInts(so.FindProperty("rankUnlockLevels"), ranks);
            // Whatever an older version of the ability carried
            so.FindProperty("pierce").boolValue = false;
            so.FindProperty("blink").boolValue = false;
            so.FindProperty("appliesOnHit").boolValue = false;
            so.FindProperty("cooldownRefundOnHit").floatValue = 0f;
            so.FindProperty("blinkBoltRadius").floatValue = 0f;
            so.FindProperty("mark").FindPropertyRelative("Enabled").boolValue = false;
            so.FindProperty("buffs").arraySize = 0;
            so.FindProperty("statuses").arraySize = 0;
            so.FindProperty("adRatio").floatValue = 0f;
            so.FindProperty("bonusAdRatio").floatValue = 0f;
            so.FindProperty("apRatio").floatValue = 0f;
            configure(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spell);
            return spell;
        }

        // Surchauffe: a spell charges the next basic attack (6 s): it explodes around the target and burns it
        private static PassiveDefinition Passive() {
            var passive = PassiveSetup.Passive(new PassiveSetup.P {
                Id = "Vesna_Overheat", Name = "Surchauffe",
                Description = "Après un sort, la prochaine attaque de Vesna (dans les 6 s) explose : 15 (+50 % AD) dégâts magiques aux ennemis à moins de 2,5 m de la cible, qui brûle 2 s (10 (+10 % AD) dégâts par seconde).",
                Triggers = new[] {
                    new PassiveTrigger { On = PassiveEvent.SpellCast, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.Charge, Duration = 6f },
                    new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 1, Chance = 1f, ConsumesCharge = true, Effect = PassiveEffectKind.Splash,
                                         Radius = 2.5f, Damage = 15f, ScalingStat = StatType.PhysicalAttack, DamageRatio = 0.5f, DamageType = DamageType.Magic },
                    new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 1, Chance = 1f, ConsumesCharge = true, Effect = PassiveEffectKind.ApplyStatus,
                                         Status = new StatusApplication { Kind = StatusKind.Burn, Power = 10f, ScalingStat = StatType.PhysicalAttack, Ratio = 0.1f, Duration = 2f, MaxStacks = 1 } },
                },
            }, null, AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsDir}/Vesna_Passive.png"), overwrite: true);
            return passive;
        }

        // Ranged caster: a bit frailer than a bruiser, a lot of mana for her spell chains
        private static void WriteStats(StatBlock block) {
            if (block == null) return;
            WriteList(block, "entries", new (StatType, float)[] {
                (StatType.MaxHealth, 590f), (StatType.HealthRegen, 0.8f),
                (StatType.MaxMana, 360f), (StatType.ManaRegen, 3f),
                (StatType.PhysicalAttack, 60f), (StatType.MagicAttack, 0f),
                (StatType.AttackSpeed, 0.66f), (StatType.AttackRange, 5.5f),
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
                (StatType.MaxHealth, 100f, ModifierType.Flat), (StatType.HealthRegen, 0.13f, ModifierType.Flat),
                (StatType.MaxMana, 65f, ModifierType.Flat), (StatType.ManaRegen, 0.15f, ModifierType.Flat),
                (StatType.PhysicalAttack, 2.6f, ModifierType.Flat), (StatType.AttackSpeed, 0.025f, ModifierType.PercentAdd),
                (StatType.Armor, 4.5f, ModifierType.Flat), (StatType.MagicResist, 1.3f, ModifierType.Flat),
            }, (e, v) => {
                e.FindPropertyRelative("Stat").enumValueIndex = (int)v.Item1;
                e.FindPropertyRelative("GainPerLevel").floatValue = v.Item2;
                e.FindPropertyRelative("Type").enumValueIndex = (int)v.Item3;
            });
        }

        // A skill point on every level up to the max (5 + 5 + 5 + 3 = 18)
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
