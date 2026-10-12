#if UNITY_EDITOR
using System.IO;
using Features.Characters;
using Features.Combat;
using Features.Enemies;
using Features.Passives;
using Features.Run;
using Features.Spells;
using Features.Stats;
using UnityEditor;
using UnityEngine;
using P = EditorTools.PassiveSetup.P;

namespace EditorTools {
    /// <summary>
    /// Phase 9c enemies (design doc §9.6), called by <see cref="ProjectSetup"/> after the 9b content:
    /// - Loup: fast horde melee whose bites bleed (an enemy passive, same bricks as the player's);
    /// - Mage squelette: ranged, special = frost bolt skillshot (telegraphed line, slows);
    /// - Chaman gobelin: support, special = shield + attack / move speed for the allies around;
    /// - Golem de pierre: NavMesh elite, special = quake around itself that roots.
    /// They are added to the test stage's waves (from wave 2) when missing. Created once, never
    /// overwritten; values are placeholders.
    /// </summary>
    public static class EnemyContentSetup {
        private const string Dir = EnemySetup.EnemiesDir;
        private const string SpellsDir = "Assets/Data/Spells/Enemies";
        public const string WolfPath = Dir + "/Enemy_Wolf.asset";
        public const string MagePath = Dir + "/Enemy_SkeletonMage.asset";
        public const string ShamanPath = Dir + "/Enemy_GoblinShaman.asset";
        public const string GolemPath = Dir + "/Enemy_StoneGolem.asset";

        // Spawn weights per wave (wolf, mage, shaman, golem), regular waves then bonus waves
        private static readonly float[][] WaveWeights = {
            new[] { 0f, 0f, 0f, 0f }, new[] { 1f, 0f, 0f, 0f }, new[] { 2f, 1f, 0f, 0f },
            new[] { 1f, 1f, 1f, 0f }, new[] { 2f, 0f, 1f, 0f },
        };
        private static readonly float[][] BonusWeights = { new[] { 2f, 2f, 1f, 1f }, new[] { 2f, 2f, 1.5f, 1.5f } };

        public static void Run(Character enemyPrefab) {
            foreach (string sprite in new[] { "Enemy_Wolf", "Enemy_SkeletonMage", "Enemy_GoblinShaman", "Enemy_StoneGolem" })
                CharacterPrefabSetup.ConfigureCharacterSprite($"Assets/GraphicAssets/Sprites/Characters/{sprite}.png");

            var bite = EnemySetup.AttackOnce(Dir + "/BasicAttack_EnemyBite.asset", windup: 0.3f, ratio: 1f, projectile: null, speed: 0f);
            var bolt = EnemySetup.AttackOnce(Dir + "/BasicAttack_EnemyBolt.asset", windup: 0.35f, ratio: 1f,
                AssetDatabase.LoadAssetAtPath<Projectile>(CharacterPrefabSetup.ArcaneBoltPrefabPath), speed: 12f, homing: false);
            var heavy = EnemySetup.AttackOnce(Dir + "/BasicAttack_EnemyHeavy.asset", windup: 0.45f, ratio: 1f, projectile: null, speed: 0f);
            SetOnce(bolt, "damageType", (int)DamageType.Magic);

            var wolf = EnemySetup.EnemyOnce(WolfPath, "Loup", "Enemy_Wolf", new Vector2(1.1f, 0.9f), bite, EnemyNavigation.FlowField,
                xp: 16, gold: 3, special: null, new (StatType, float)[] {
                    (StatType.MaxHealth, 60f), (StatType.PhysicalAttack, 8f), (StatType.AttackSpeed, 1f),
                    (StatType.AttackRange, 0.8f), (StatType.Armor, 5f), (StatType.MagicResist, 5f), (StatType.MoveSpeed, 4.6f),
                });
            AssignPassive(wolf, new P { Id = "Enemy_WolfBite", Name = "Morsure", Description = "Les morsures font saigner.",
                Triggers = new[] { new PassiveTrigger { On = PassiveEvent.BasicAttackHit, EveryN = 1, Chance = 1f, Effect = PassiveEffectKind.ApplyStatus,
                    Status = new StatusApplication { Kind = StatusKind.Bleed, Power = 4f, Duration = 3f, MaxStacks = 3 } } } });

            var mage = EnemySetup.EnemyOnce(MagePath, "Mage squelette", "Enemy_SkeletonMage", new Vector2(0.7f, 1.7f), bolt, EnemyNavigation.FlowField,
                xp: 26, gold: 5, special: FrostBolt(), new (StatType, float)[] {
                    (StatType.MaxHealth, 65f), (StatType.MagicAttack, 14f), (StatType.AttackSpeed, 0.5f),
                    (StatType.AttackRange, 7f), (StatType.Armor, 5f), (StatType.MagicResist, 25f), (StatType.MoveSpeed, 2.9f),
                    (StatType.MaxMana, 100f),
                });

            var shaman = EnemySetup.EnemyOnce(ShamanPath, "Chaman gobelin", "Enemy_GoblinShaman", new Vector2(0.7f, 1.4f), bolt, EnemyNavigation.FlowField,
                xp: 30, gold: 6, special: Ward(), new (StatType, float)[] {
                    (StatType.MaxHealth, 80f), (StatType.MagicAttack, 8f), (StatType.AttackSpeed, 0.5f),
                    (StatType.AttackRange, 5f), (StatType.Armor, 10f), (StatType.MagicResist, 20f), (StatType.MoveSpeed, 3.1f),
                    (StatType.MaxMana, 100f),
                });

            var golem = EnemySetup.EnemyOnce(GolemPath, "Golem de pierre", "Enemy_StoneGolem", new Vector2(1.6f, 2.6f), heavy, EnemyNavigation.NavMesh,
                xp: 150, gold: 25, special: Quake(), new (StatType, float)[] {
                    (StatType.MaxHealth, 900f), (StatType.PhysicalAttack, 45f), (StatType.AttackSpeed, 0.45f),
                    (StatType.AttackRange, 1.4f), (StatType.Armor, 60f), (StatType.MagicResist, 30f), (StatType.MoveSpeed, 2.2f),
                    (StatType.MaxMana, 100f), (StatType.Tenacity, 0.5f),
                });

            AddToWaves(enemyPrefab, new[] { wolf, mage, shaman, golem });
        }

        // Frost bolt: telegraphed line, slows; ~1 every 10 s
        private static SpellDefinition FrostBolt() => SpellOnce("Spell_FrostBolt", "Trait de givre", new Color(0.55f, 0.85f, 1f), so => {
            so.FindProperty("targeting").enumValueIndex = (int)TargetingType.Skillshot;
            so.FindProperty("range").floatValue = 10f;
            so.FindProperty("radius").floatValue = 0.5f;
            so.FindProperty("castTime").floatValue = 0.7f;
            so.FindProperty("kind").enumValueIndex = (int)SpellKind.Magic;
            so.FindProperty("powerRatio").floatValue = 0.6f;
            so.FindProperty("scaling").FindPropertyRelative("Base").floatValue = 40f;
            so.FindProperty("scaling").FindPropertyRelative("PerElementLevel").floatValue = 0f;
            so.FindProperty("projectileSpeed").floatValue = 13f;
        }, statuses: new[] { new StatusApplication { Kind = StatusKind.Slow, Power = 0.4f, Duration = 2f } });

        // Ward: shield + speed for the goblins around the shaman (green telegraph)
        private static SpellDefinition Ward() => SpellOnce("Spell_ShamanWard", "Protection tribale", new Color(0.45f, 1f, 0.5f), so => {
            so.FindProperty("targeting").enumValueIndex = (int)TargetingType.Self;
            so.FindProperty("radius").floatValue = 5f;
            so.FindProperty("castTime").floatValue = 0.6f;
            so.FindProperty("kind").enumValueIndex = (int)SpellKind.Buff;
            so.FindProperty("affectsAllies").boolValue = true;
        }, sustain: new SelfSustain { Shield = 60f, ShieldDuration = 4f },
           buffs: new[] {
               new BuffEffect { Stat = StatType.AttackSpeed, Type = ModifierType.PercentAdd, Value = 0.3f, Duration = 4f },
               new BuffEffect { Stat = StatType.MoveSpeed, Type = ModifierType.PercentAdd, Value = 0.15f, Duration = 4f },
           });

        // Quake: zone around the golem, roots
        private static SpellDefinition Quake() => SpellOnce("Spell_GolemQuake", "Séisme", new Color(0.7f, 0.6f, 0.45f), so => {
            so.FindProperty("targeting").enumValueIndex = (int)TargetingType.GroundPoint;
            so.FindProperty("range").floatValue = 2f;   // centred close to the golem, towards the player
            so.FindProperty("radius").floatValue = 3.5f;
            so.FindProperty("castTime").floatValue = 1f;
            so.FindProperty("kind").enumValueIndex = (int)SpellKind.Physical;
            so.FindProperty("powerRatio").floatValue = 0.5f;
            so.FindProperty("scaling").FindPropertyRelative("Base").floatValue = 50f;
            so.FindProperty("scaling").FindPropertyRelative("PerElementLevel").floatValue = 0f;
        }, statuses: new[] { new StatusApplication { Kind = StatusKind.Root, Duration = 1.2f } });

        private static SpellDefinition SpellOnce(string id, string name, Color color, System.Action<SerializedObject> configure,
                                                 StatusApplication[] statuses = null, SelfSustain sustain = default, BuffEffect[] buffs = null) {
            string path = $"{SpellsDir}/{id}.asset";
            var spell = AssetDatabase.LoadAssetAtPath<SpellDefinition>(path);
            if (spell != null) return spell;
            spell = CreateAsset<SpellDefinition>(path);
            var so = new SerializedObject(spell);
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("color").colorValue = color;
            configure(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            if (statuses != null) Set(spell, "statuses", statuses);
            if (!sustain.IsEmpty) Set(spell, "selfSustain", sustain);
            if (buffs != null) Set(spell, "buffs", buffs);
            return spell;
        }

        private static void AssignPassive(EnemyDefinition enemy, P passive) {
            if (enemy.Passive != null) return;
            var so = new SerializedObject(enemy);
            so.FindProperty("passive").objectReferenceValue = PassiveSetup.Passive(passive, null, enemy.Sprite);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Each new enemy joins a wave's spawn table once (a wave edited by hand keeps its entries)
        private static void AddToWaves(Character prefab, EnemyDefinition[] enemies) {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(RunSetup.StagePath);
            if (stage == null) return;
            for (int i = 0; i < stage.Waves.Length && i < WaveWeights.Length; i++) AddToWave(stage.Waves[i], prefab, enemies, WaveWeights[i]);
            for (int i = 0; i < stage.BonusWaves.Length && i < BonusWeights.Length; i++) AddToWave(stage.BonusWaves[i], prefab, enemies, BonusWeights[i]);
        }

        private static void AddToWave(WaveDefinition wave, Character prefab, EnemyDefinition[] enemies, float[] weights) {
            if (wave == null) return;
            var so = new SerializedObject(wave);
            SerializedProperty table = so.FindProperty("spawnTable");
            for (int e = 0; e < enemies.Length; e++) {
                if (weights[e] <= 0f || System.Array.Exists(wave.SpawnTable, entry => entry.Definition == enemies[e])) continue;
                table.arraySize++;
                SerializedProperty entry = table.GetArrayElementAtIndex(table.arraySize - 1);
                entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
                entry.FindPropertyRelative("Definition").objectReferenceValue = enemies[e];
                entry.FindPropertyRelative("Weight").floatValue = weights[e];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetOnce(Object target, string property, int enumIndex) {
            var so = new SerializedObject(target);
            so.FindProperty(property).enumValueIndex = enumIndex;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, object value) {
            target.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(target, value);
            EditorUtility.SetDirty(target);
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
