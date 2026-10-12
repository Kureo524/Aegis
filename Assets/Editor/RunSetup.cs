#if UNITY_EDITOR
using System.IO;
using Features.Characters;
using Features.Run;
using UnityEditor;
using UnityEngine;

namespace EditorTools {
    /// <summary>
    /// Phase 7a content (design doc §9.1, §9.3), called by <see cref="ProjectSetup"/>: the test
    /// stage (5 waves + 2 bonus waves, 3 lives) and its reward schedule. Created once, never
    /// overwritten: balance is tuned in the assets. Values are placeholders.
    /// </summary>
    public static class RunSetup {
        public const string StageDir = "Assets/Data/Run/TestStage";
        public const string StagePath = StageDir + "/Stage_Test.asset";
        public const string SchedulePath = "Assets/Data/Run/WaveRewardSchedule.asset";

        // Design doc §14.2 (RunStateMachine)
        public static readonly (RunState from, RunState[] to)[] Rules = {
            (RunState.PreWave,  new[] { RunState.Combat }),
            (RunState.Combat,   new[] { RunState.Reward, RunState.Overtime }),
            (RunState.Overtime, new[] { RunState.Reward }),
            (RunState.Reward,   new[] { RunState.PreWave }),
        };

        // (quota, seconds, goblin / archer / brute weights, spawn interval, max alive, health ×, damage ×)
        private static readonly (int quota, float time, float g, float a, float b, float interval, int max, float hp, float dmg)[] Waves = {
            (12, 40f, 1f, 0f, 0f, 1.6f, 12, 1f,   1f),
            (16, 45f, 3f, 1f, 0f, 1.4f, 14, 1.1f, 1.05f),
            (20, 50f, 3f, 2f, 0f, 1.3f, 16, 1.2f, 1.1f),
            (24, 55f, 3f, 2f, 1f, 1.2f, 18, 1.35f, 1.2f),
            (28, 60f, 3f, 2f, 1f, 1.1f, 20, 1.5f, 1.3f),
        };
        private static readonly (int quota, float time, float g, float a, float b, float interval, int max, float hp, float dmg)[] BonusWaves = {
            (32, 60f, 3f, 2f, 1f, 1.0f, 22, 1.8f, 1.5f),
            (36, 60f, 3f, 2f, 1.5f, 0.9f, 24, 2.1f, 1.7f),
        };

        public static StageDefinition Run(Character enemyPrefab) {
            var schedule = AssetDatabase.LoadAssetAtPath<WaveRewardSchedule>(SchedulePath);
            if (schedule == null) schedule = CreateAsset<WaveRewardSchedule>(SchedulePath);   // defaults = the doc's order

            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(StagePath);
            if (stage != null) return stage;

            var goblin = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemySetup.GoblinPath);
            var archer = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemySetup.ArcherPath);
            var brute = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemySetup.BrutePath);

            stage = CreateAsset<StageDefinition>(StagePath);
            var so = new SerializedObject(stage);
            so.FindProperty("displayName").stringValue = "Plaine d'essai";
            so.FindProperty("rewardSchedule").objectReferenceValue = schedule;
            FillWaves(so.FindProperty("waves"), Waves, "Wave_", enemyPrefab, goblin, archer, brute);
            FillWaves(so.FindProperty("bonusWaves"), BonusWaves, "BonusWave_", enemyPrefab, goblin, archer, brute);
            so.ApplyModifiedPropertiesWithoutUndo();
            return stage;
        }

        private static void FillWaves(SerializedProperty list, (int quota, float time, float g, float a, float b, float interval, int max, float hp, float dmg)[] data,
                                      string prefix, Character prefab, EnemyDefinition goblin, EnemyDefinition archer, EnemyDefinition brute) {
            list.arraySize = data.Length;
            for (int i = 0; i < data.Length; i++) {
                var w = data[i];
                var wave = CreateAsset<WaveDefinition>($"{StageDir}/{prefix}{i + 1:00}.asset");
                var so = new SerializedObject(wave);
                so.FindProperty("killQuota").intValue = w.quota;
                so.FindProperty("timeLimit").floatValue = w.time;
                so.FindProperty("spawnInterval").floatValue = w.interval;
                so.FindProperty("postQuotaSpawnInterval").floatValue = w.interval * 1.8f;
                so.FindProperty("maxAlive").intValue = w.max;
                so.FindProperty("healthMultiplier").floatValue = w.hp;
                so.FindProperty("damageMultiplier").floatValue = w.dmg;
                SerializedProperty table = so.FindProperty("spawnTable");
                (EnemyDefinition def, float weight)[] entries = { (goblin, w.g), (archer, w.a), (brute, w.b) };
                table.arraySize = 0;
                foreach ((EnemyDefinition def, float weight) in entries) {
                    if (weight <= 0f) continue;
                    table.arraySize++;
                    SerializedProperty e = table.GetArrayElementAtIndex(table.arraySize - 1);
                    e.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
                    e.FindPropertyRelative("Definition").objectReferenceValue = def;
                    e.FindPropertyRelative("Weight").floatValue = weight;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                list.GetArrayElementAtIndex(i).objectReferenceValue = wave;
            }
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
