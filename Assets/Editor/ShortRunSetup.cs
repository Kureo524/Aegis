#if UNITY_EDITOR
using System.IO;
using Containers;
using Features.Run;
using Features.Score;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EditorTools {
    /// <summary>
    /// The short run and the score (design doc §9.1, §9.9), called by <see cref="ProjectSetup"/>
    /// after the test stage:
    /// - ScoreRules (created once, tuned in the asset) wired in the test scene's GameLifetimeScope;
    /// - Stage_Demo: two waves copied from the test stage's first two (same horde), shorter
    ///   timers and breaks, the same reward schedule (wave 1 → augment), so a run lasts 2-3 minutes;
    /// - StageCatalog (short run first = default) wired in the RootLifetimeScope prefab.
    ///   Unity -batchmode -quit -projectPath . -executeMethod EditorTools.ShortRunSetup.RunStandalone
    /// </summary>
    public static class ShortRunSetup {
        public const string ScoreRulesPath = "Assets/Data/Run/ScoreRules.asset";
        public const string CatalogPath = "Assets/Data/Run/StageCatalog.asset";
        public const string DemoDir = "Assets/Data/Run/DemoStage";
        public const string DemoStagePath = DemoDir + "/Stage_Demo.asset";

        // (time limit) of the copied waves; the stage timings
        private static readonly float[] WaveTimes = { 40f, 45f };
        private const float FirstWaveCountdown = 5f;
        private const float Intermission = 20f;

        [MenuItem("Tools/Short Run and Score Setup")]
        public static void RunStandalone() {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(RunSetup.StagePath);
            Run(ProjectSetup.RootPrefabPath, ProjectSetup.TestScenePath, stage);
            AssetDatabase.SaveAssets();
            Debug.Log("[ShortRunSetup] Done.");
        }

        public static void Run(string rootPrefabPath, string gameScenePath, StageDefinition fullStage) {
            ScoreRules rules = AssetDatabase.LoadAssetAtPath<ScoreRules>(ScoreRulesPath);
            if (rules == null) rules = CreateAsset<ScoreRules>(ScoreRulesPath);   // defaults = the doc's values

            if (fullStage != null) {
                var full = new SerializedObject(fullStage);
                full.FindProperty("displayName").stringValue = "Partie complète (5 vagues + 2 bonus)";
                full.ApplyModifiedPropertiesWithoutUndo();
            }
            StageDefinition demo = CreateDemoStage(fullStage);

            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(CatalogPath);
            if (catalog == null) catalog = CreateAsset<StageCatalog>(CatalogPath);
            var cso = new SerializedObject(catalog);
            StageDefinition[] stages = fullStage != null ? new[] { demo, fullStage } : new[] { demo };
            SerializedProperty list = cso.FindProperty("stages");
            list.arraySize = stages.Length;
            for (int i = 0; i < stages.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = stages[i];
            cso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);

            GameObject root = PrefabUtility.LoadPrefabContents(rootPrefabPath);
            var scopeSo = new SerializedObject(root.GetComponent<RootLifetimeScope>());
            if (scopeSo.FindProperty("stageCatalog").objectReferenceValue != catalog) {
                scopeSo.FindProperty("stageCatalog").objectReferenceValue = catalog;
                scopeSo.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, rootPrefabPath);
            }
            PrefabUtility.UnloadPrefabContents(root);

            var scene = EditorSceneManager.OpenScene(gameScenePath, OpenSceneMode.Single);
            rules = AssetDatabase.LoadAssetAtPath<ScoreRules>(ScoreRulesPath);   // unloaded by OpenScene (unused asset)
            var game = Object.FindFirstObjectByType<GameLifetimeScope>(FindObjectsInactive.Include);
            if (game == null) Debug.LogWarning($"[ShortRunSetup] No GameLifetimeScope in {gameScenePath}, score not wired.");
            else if (game.scoreRules != rules) {
                game.scoreRules = rules;
                EditorUtility.SetDirty(game);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        // Created once from the full stage's first waves: later runs keep the designer's edits
        private static StageDefinition CreateDemoStage(StageDefinition fullStage) {
            var demo = AssetDatabase.LoadAssetAtPath<StageDefinition>(DemoStagePath);
            if (demo != null) return demo;

            demo = CreateAsset<StageDefinition>(DemoStagePath);
            var so = new SerializedObject(demo);
            so.FindProperty("displayName").stringValue = "Run courte (2 vagues, ~3 min)";
            so.FindProperty("firstWaveCountdown").floatValue = FirstWaveCountdown;
            so.FindProperty("intermission").floatValue = Intermission;
            if (fullStage != null) {
                so.FindProperty("rewardSchedule").objectReferenceValue = fullStage.RewardSchedule;
                so.FindProperty("lives").intValue = fullStage.Lives;
                SerializedProperty waves = so.FindProperty("waves");
                int count = Mathf.Min(WaveTimes.Length, fullStage.Waves.Length);
                waves.arraySize = count;
                for (int i = 0; i < count; i++) {
                    string path = $"{DemoDir}/Demo_Wave_{i + 1:00}.asset";
                    AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(fullStage.Waves[i]), path);
                    var wave = AssetDatabase.LoadAssetAtPath<WaveDefinition>(path);
                    var wso = new SerializedObject(wave);
                    wso.FindProperty("timeLimit").floatValue = WaveTimes[i];
                    wso.ApplyModifiedPropertiesWithoutUndo();
                    waves.GetArrayElementAtIndex(i).objectReferenceValue = wave;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return demo;
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
