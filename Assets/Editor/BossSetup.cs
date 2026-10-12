#if UNITY_EDITOR
using System.IO;
using Features.Characters;
using Features.Combat;
using Features.Economy;
using Features.Enemies;
using Features.Movement;
using Features.Run;
using Features.Spells;
using Features.Stats;
using UnityEditor;
using UnityEngine;

namespace EditorTools {
    /// <summary>
    /// Phase 9 boss (design doc §9.6), called by <see cref="ProjectSetup"/>: the Boss prefab (Enemy
    /// variant + PatternRunner), Grokk the goblin king with his 3-phase pattern (slam, charge, nova,
    /// summons) and 2 items, set as the boss of the test stage's wave 5. Created once, never
    /// overwritten; values are placeholders.
    /// </summary>
    public static class BossSetup {
        public const string BossPrefabPath = "Assets/Prefabs/Characters/Boss.prefab";
        public const string Dir = "Assets/Data/Characters/Bosses";
        public const string GrokkPath = Dir + "/Boss_GoblinKing.asset";
        private const string SpritePath = "Assets/GraphicAssets/Sprites/Characters/Boss_GoblinKing.png";

        public static void Run(Character enemyPrefab) {
            CharacterPrefabSetup.ConfigureCharacterSprite(SpritePath);
            Character bossPrefab = CreateBossPrefab(enemyPrefab);
            BossDefinition grokk = CreateGrokk(enemyPrefab);

            // The test stage's wave 5 gets the boss, once (a boss chosen by hand is kept)
            var wave = AssetDatabase.LoadAssetAtPath<WaveDefinition>(RunSetup.StageDir + "/Wave_05.asset");
            if (wave != null && wave.Boss == null) {
                var so = new SerializedObject(wave);
                so.FindProperty("boss").objectReferenceValue = grokk;
                so.FindProperty("bossPrefab").objectReferenceValue = bossPrefab;
                so.FindProperty("timeLimit").floatValue = 120f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static Character CreateBossPrefab(Character enemyPrefab) {
            if (!File.Exists(BossPrefabPath)) {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab.gameObject);
                instance.name = "Boss";
                instance.AddComponent<PatternRunner>();
                PrefabUtility.SaveAsPrefabAsset(instance, BossPrefabPath);
                Object.DestroyImmediate(instance);
            }
            GameObject root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            var runner = root.GetComponent<PatternRunner>() ?? root.AddComponent<PatternRunner>();
            var so = new SerializedObject(runner);
            so.FindProperty("owner").objectReferenceValue = root.GetComponent<Character>();
            so.FindProperty("ai").objectReferenceValue = root.GetComponent<EnemyAI>();
            so.FindProperty("movement").objectReferenceValue = root.GetComponent<CharacterMovement>();
            so.FindProperty("attacker").objectReferenceValue = root.GetComponent<BasicAttacker>();
            so.FindProperty("caster").objectReferenceValue = root.GetComponent<SpellCaster>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            return AssetDatabase.LoadAssetAtPath<Character>(BossPrefabPath);
        }

        private static BossDefinition CreateGrokk(Character enemyPrefab) {
            var boss = AssetDatabase.LoadAssetAtPath<BossDefinition>(GrokkPath);
            if (boss != null) return boss;

            SpellDefinition slam = Spell("Spell_GrokkSlam", "Coup de massue", 5f, 2.6f, 1.0f, 90f, 0.8f, new Color(1f, 0.45f, 0.2f));
            SpellDefinition nova = Spell("Spell_GrokkNova", "Onde royale", 0f, 4.5f, 1.3f, 70f, 0.6f, new Color(1f, 0.8f, 0.3f));
            var goblin = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemySetup.GoblinPath);

            PatternStep Wait(float s) => new() { Kind = PatternStepKind.Wait, Duration = s, Weight = 1f };
            PatternStep Cast(SpellDefinition spell, bool self) => new() { Kind = PatternStepKind.CastSpell, Spell = spell, AtSelf = self, Duration = 0.6f, Weight = 1f };
            PatternStep Charge(float damage) => new() { Kind = PatternStepKind.Charge, ChargeDistance = 11f, ChargeSpeed = 15f, ChargeDamage = damage, Telegraph = 0.8f, Duration = 0.8f, Weight = 1f };
            PatternStep Summon(int n) => new() { Kind = PatternStepKind.Summon, Summon = goblin, SummonPrefab = enemyPrefab, Count = n, Duration = 0.8f, Weight = 1f };

            var pattern = CreateAsset<PatternDefinition>(Dir + "/Pattern_GoblinKing.asset");
            var phases = new[] {
                new PatternPhase { HealthThreshold = 1f, Order = PatternOrder.Sequence,
                                   Steps = new[] { Wait(3f), Cast(slam, false), Wait(3f), Charge(80f) } },
                new PatternPhase { HealthThreshold = 0.6f, Order = PatternOrder.Sequence,
                                   Steps = new[] { Summon(4), Wait(2f), Cast(nova, true), Charge(100f), Wait(1.5f), Cast(slam, false) } },
                new PatternPhase { HealthThreshold = 0.3f, Order = PatternOrder.WeightedRandom,
                                   Steps = new[] { Cast(slam, false), Charge(120f), Cast(nova, true), Summon(5), Wait(1.2f) } },
            };
            typeof(PatternDefinition).GetField("phases", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                                     .SetValue(pattern, phases);
            EditorUtility.SetDirty(pattern);

            var stats = CreateAsset<StatBlock>(Dir + "/StatBlock_GoblinKing.asset");
            var statsSo = new SerializedObject(stats);
            (StatType, float)[] values = {
                (StatType.MaxHealth, 3000f), (StatType.PhysicalAttack, 55f), (StatType.AttackSpeed, 0.6f), (StatType.AttackRange, 1.8f),
                (StatType.Armor, 45f), (StatType.MagicResist, 35f), (StatType.MoveSpeed, 3f), (StatType.CritDamage, 1.5f),
            };
            SerializedProperty entries = statsSo.FindProperty("entries");
            entries.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) {
                entries.GetArrayElementAtIndex(i).FindPropertyRelative("Type").enumValueIndex = (int)values[i].Item1;
                entries.GetArrayElementAtIndex(i).FindPropertyRelative("Value").floatValue = values[i].Item2;
            }
            statsSo.ApplyModifiedPropertiesWithoutUndo();

            boss = CreateAsset<BossDefinition>(GrokkPath);
            var so = new SerializedObject(boss);
            so.FindProperty("displayName").stringValue = "Grokk";
            so.FindProperty("title").stringValue = "Roi des gobelins";
            so.FindProperty("faction").enumValueIndex = (int)Faction.Enemy;
            so.FindProperty("sprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            so.FindProperty("bodySize").vector2Value = new Vector2(1.8f, 2.9f);
            so.FindProperty("baseStats").objectReferenceValue = stats;
            so.FindProperty("basicAttack").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BasicAttackDefinition>(EnemySetup.EnemiesDir + "/BasicAttack_EnemyMelee.asset");
            so.FindProperty("navigation").enumValueIndex = (int)EnemyNavigation.NavMesh;
            so.FindProperty("xpReward").intValue = 600;
            so.FindProperty("goldReward").intValue = 150;
            so.FindProperty("pattern").objectReferenceValue = pattern;
            SerializedProperty items = so.FindProperty("items");
            string[] itemIds = { "ColossusArmor", "SharpBlade" };
            items.arraySize = itemIds.Length;
            for (int i = 0; i < itemIds.Length; i++)
                items.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{EconomySetup.Dir}/Items/Item_{itemIds[i]}.asset");
            so.ApplyModifiedPropertiesWithoutUndo();
            return boss;
        }

        private static SpellDefinition Spell(string id, string name, float range, float radius, float castTime, float damage, float ratio, Color color) {
            string path = $"Assets/Data/Spells/Enemies/{id}.asset";
            var spell = AssetDatabase.LoadAssetAtPath<SpellDefinition>(path);
            if (spell != null) return spell;
            spell = CreateAsset<SpellDefinition>(path);
            var so = new SerializedObject(spell);
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("color").colorValue = color;
            so.FindProperty("targeting").enumValueIndex = (int)TargetingType.GroundPoint;
            so.FindProperty("range").floatValue = range;
            so.FindProperty("radius").floatValue = radius;
            so.FindProperty("castTime").floatValue = castTime;
            so.FindProperty("kind").enumValueIndex = (int)SpellKind.Physical;
            so.FindProperty("powerRatio").floatValue = ratio;
            so.FindProperty("scaling").FindPropertyRelative("Base").floatValue = damage;
            so.FindProperty("scaling").FindPropertyRelative("PerElementLevel").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return spell;
        }

        private static T CreateAsset<T>(string path) where T : ScriptableObject {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
#endif
