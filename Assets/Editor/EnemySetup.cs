#if UNITY_EDITOR
using System.IO;
using Features.Characters;
using Features.Combat;
using Features.Enemies;
using Features.Spells;
using Features.Stats;
using UnityEditor;
using UnityEngine;

namespace EditorTools {
    /// <summary>
    /// Phase 6 content (design doc §9.6), called by <see cref="ProjectSetup"/>: the three test enemies
    /// (goblin and skeleton archer for the horde, brute elite with a special spell), their sprites'
    /// import settings, and the test scene's HordeSettings. Every asset is created once and never
    /// overwritten: balance is tuned in the assets themselves. Values are placeholders.
    /// </summary>
    public static class EnemySetup {
        public const string EnemiesDir = "Assets/Data/Characters/Enemies";
        public const string GoblinPath = EnemiesDir + "/Enemy_Goblin.asset";
        public const string ArcherPath = EnemiesDir + "/Enemy_SkeletonArcher.asset";
        public const string BrutePath = EnemiesDir + "/Enemy_Brute.asset";
        public const string BruteSlamPath = "Assets/Data/Spells/Enemies/Spell_BruteSlam.asset";
        public const string HordeSettingsPath = "Assets/Data/Settings/HordeSettings.asset";
        private const string SpritesDir = "Assets/GraphicAssets/Sprites/Characters";

        /// <summary>Creates what is missing; returns the horde settings of the test scene.</summary>
        public static HordeSettings Run(Character enemyPrefab) {
            foreach (string sprite in new[] { "Enemy_Goblin", "Enemy_SkeletonArcher", "Enemy_Brute" })
                CharacterPrefabSetup.ConfigureCharacterSprite($"{SpritesDir}/{sprite}.png");

            var melee = AttackOnce(EnemiesDir + "/BasicAttack_EnemyMelee.asset", windup: 0.35f, ratio: 1f, projectile: null, speed: 0f);
            var archerShot = AttackOnce(EnemiesDir + "/BasicAttack_EnemyArrow.asset", windup: 0.3f, ratio: 1f,
                                        projectile: AssetDatabase.LoadAssetAtPath<Projectile>(CharacterPrefabSetup.ArcaneBoltPrefabPath), speed: 14f);
            SpellDefinition slam = CreateBruteSlam();

            var goblin = EnemyOnce(GoblinPath, "Gobelin", "Enemy_Goblin", new Vector2(0.7f, 1.25f), melee, EnemyNavigation.FlowField,
                xp: 18, gold: 3, special: null, new (StatType, float)[] {
                    (StatType.MaxHealth, 140f), (StatType.PhysicalAttack, 10f), (StatType.AttackSpeed, 0.8f),
                    (StatType.AttackRange, 0.9f), (StatType.Armor, 10f), (StatType.MagicResist, 10f), (StatType.MoveSpeed, 3.4f),
                });
            var archer = EnemyOnce(ArcherPath, "Archer squelette", "Enemy_SkeletonArcher", new Vector2(0.7f, 1.6f), archerShot, EnemyNavigation.FlowField,
                xp: 22, gold: 4, special: null, new (StatType, float)[] {
                    (StatType.MaxHealth, 100f), (StatType.PhysicalAttack, 12f), (StatType.AttackSpeed, 0.6f),
                    (StatType.AttackRange, 6f), (StatType.Armor, 5f), (StatType.MagicResist, 15f), (StatType.MoveSpeed, 3f),
                });
            var brute = EnemyOnce(BrutePath, "Brute", "Enemy_Brute", new Vector2(1.3f, 2.3f), melee, EnemyNavigation.NavMesh,
                xp: 120, gold: 20, special: slam, new (StatType, float)[] {
                    (StatType.MaxHealth, 900f), (StatType.PhysicalAttack, 40f), (StatType.AttackSpeed, 0.5f),
                    (StatType.AttackRange, 1.3f), (StatType.Armor, 40f), (StatType.MagicResist, 25f), (StatType.MoveSpeed, 2.6f),
                    (StatType.MaxMana, 100f),
                });

            return CreateHordeSettings(enemyPrefab, goblin, archer, brute);
        }

        private static BasicAttackDefinition AttackOnce(string path, float windup, float ratio, Projectile projectile, float speed) {
            var attack = AssetDatabase.LoadAssetAtPath<BasicAttackDefinition>(path);
            if (attack != null) return attack;
            attack = CreateAsset<BasicAttackDefinition>(path);
            var so = new SerializedObject(attack);
            so.FindProperty("windupPercent").floatValue = windup;
            so.FindProperty("damageRatio").floatValue = ratio;
            so.FindProperty("projectilePrefab").objectReferenceValue = projectile;
            if (speed > 0f) so.FindProperty("projectileSpeed").floatValue = speed;
            so.ApplyModifiedPropertiesWithoutUndo();
            return attack;
        }

        // Ground slam on the player's position, telegraphed during its cast time so it can be dodged.
        private static SpellDefinition CreateBruteSlam() {
            var spell = AssetDatabase.LoadAssetAtPath<SpellDefinition>(BruteSlamPath);
            if (spell != null) return spell;
            spell = CreateAsset<SpellDefinition>(BruteSlamPath);
            var so = new SerializedObject(spell);
            so.FindProperty("displayName").stringValue = "Fracas";
            so.FindProperty("color").colorValue = new Color(0.95f, 0.45f, 0.25f);
            so.FindProperty("targeting").enumValueIndex = (int)TargetingType.GroundPoint;
            so.FindProperty("range").floatValue = 4f;
            so.FindProperty("radius").floatValue = 2.2f;
            so.FindProperty("castTime").floatValue = 0.9f;
            so.FindProperty("kind").enumValueIndex = (int)SpellKind.Physical;
            so.FindProperty("powerRatio").floatValue = 0.6f;
            so.FindProperty("scaling").FindPropertyRelative("Base").floatValue = 60f;
            so.FindProperty("scaling").FindPropertyRelative("PerElementLevel").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return spell;
        }

        private static EnemyDefinition EnemyOnce(string path, string displayName, string sprite, Vector2 body,
                                                 BasicAttackDefinition attack, EnemyNavigation navigation, int xp, int gold,
                                                 SpellDefinition special, (StatType, float)[] stats) {
            var enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (enemy != null) return enemy;

            var block = CreateAsset<StatBlock>(path.Replace("/Enemy_", "/StatBlock_"));
            var blockSo = new SerializedObject(block);
            SerializedProperty entries = blockSo.FindProperty("entries");
            entries.arraySize = stats.Length;
            for (int i = 0; i < stats.Length; i++) {
                entries.GetArrayElementAtIndex(i).FindPropertyRelative("Type").enumValueIndex = (int)stats[i].Item1;
                entries.GetArrayElementAtIndex(i).FindPropertyRelative("Value").floatValue = stats[i].Item2;
            }
            blockSo.ApplyModifiedPropertiesWithoutUndo();

            enemy = CreateAsset<EnemyDefinition>(path);
            var so = new SerializedObject(enemy);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("faction").enumValueIndex = (int)Faction.Enemy;
            so.FindProperty("sprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesDir}/{sprite}.png");
            so.FindProperty("bodySize").vector2Value = body;
            so.FindProperty("baseStats").objectReferenceValue = block;
            so.FindProperty("basicAttack").objectReferenceValue = attack;
            so.FindProperty("xpReward").intValue = xp;
            so.FindProperty("goldReward").intValue = gold;
            so.FindProperty("navigation").enumValueIndex = (int)navigation;
            so.FindProperty("specialSpell").objectReferenceValue = special;
            if (special != null) {
                so.FindProperty("startMana").floatValue = 0f;
                so.FindProperty("manaPerSecond").floatValue = 8f;
                so.FindProperty("manaPerAttack").floatValue = 15f;
                so.FindProperty("manaPerHitTaken").floatValue = 1f;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return enemy;
        }

        private static HordeSettings CreateHordeSettings(Character prefab, EnemyDefinition goblin, EnemyDefinition archer, EnemyDefinition brute) {
            var settings = AssetDatabase.LoadAssetAtPath<HordeSettings>(HordeSettingsPath);
            if (settings != null) return settings;
            settings = CreateAsset<HordeSettings>(HordeSettingsPath);
            var so = new SerializedObject(settings);
            SerializedProperty table = so.FindProperty("spawnTable");
            (EnemyDefinition def, float weight)[] entries = { (goblin, 6f), (archer, 3f), (brute, 1f) };
            table.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++) {
                SerializedProperty e = table.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
                e.FindPropertyRelative("Definition").objectReferenceValue = entries[i].def;
                e.FindPropertyRelative("Weight").floatValue = entries[i].weight;
            }
            so.FindProperty("spawnInterval").floatValue = 2f;
            so.FindProperty("maxAlive").intValue = 18;
            so.FindProperty("spawnOnStart").boolValue = false;   // the stage's waves drive the spawn
            so.ApplyModifiedPropertiesWithoutUndo();
            return settings;
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
