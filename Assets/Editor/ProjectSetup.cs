#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Containers;
using Features.CameraControl;
using Features.Flow;
using Features.StateMachines;
using Features.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VContainer.Unity;

namespace EditorTools {
    /// <summary>
    /// Creates / repairs the project-wide plumbing. Idempotent: safe to run again at any time.
    ///   Unity -batchmode -quit -projectPath . -executeMethod EditorTools.ProjectSetup.Run
    ///
    /// - settings assets (GameFlowSettings, CameraSettings) in Assets/Data/Settings
    /// - RootLifetimeScope prefab with the GameFlowStateMachine and its transition rules (§14.2)
    /// - VContainerSettings asset pointing to that prefab, registered as a preloaded asset
    /// - build scene list: Menu, then TestScene
    /// - scene migrations (Menu: drop its old local state machine; TestScene: wire QuitOverlay)
    /// </summary>
    public static class ProjectSetup {
        private const string SettingsDir = "Assets/Data/Settings";
        private const string GameFlowSettingsPath = SettingsDir + "/GameFlowSettings.asset";
        private const string CameraSettingsPath = SettingsDir + "/CameraSettings.asset";
        private const string VContainerSettingsPath = SettingsDir + "/VContainerSettings.asset";
        private const string RootPrefabPath = "Assets/Prefabs/RootLifetimeScope.prefab";

        public const string MenuScenePath = "Assets/Scenes/Releases/Menu.unity";
        public const string TestScenePath = "Assets/Scenes/Tests/TestScene.unity";

        // Design doc §14.2
        private static readonly (GameState from, GameState[] to)[] FlowRules = {
            (GameState.Menu,     new[] { GameState.Loading }),
            (GameState.Loading,  new[] { GameState.Game, GameState.Menu }),
            (GameState.Game,     new[] { GameState.Pause, GameState.Win, GameState.GameOver }),
            (GameState.Pause,    new[] { GameState.Game, GameState.Menu }),
            (GameState.Win,      new[] { GameState.Loading, GameState.Menu }),
            (GameState.GameOver, new[] { GameState.Loading, GameState.Menu }),
        };

        [MenuItem("Tools/Project Setup (Root Scope, Settings, Scenes)")]
        public static void Run() {
            Directory.CreateDirectory(SettingsDir);
            Directory.CreateDirectory(Path.GetDirectoryName(RootPrefabPath)!);

            var flowSettings = LoadOrCreate<GameFlowSettings>(GameFlowSettingsPath);
            var cameraSettings = LoadOrCreate<CameraSettings>(CameraSettingsPath);
            RootLifetimeScope rootPrefab = CreateOrUpdateRootPrefab(flowSettings, cameraSettings);
            RegisterVContainerSettings(rootPrefab);
            SetBuildScenes();
            MigrateMenuScene();
            MigrateTestScene();

            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Done.");
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static RootLifetimeScope CreateOrUpdateRootPrefab(GameFlowSettings flowSettings, CameraSettings cameraSettings) {
            bool exists = File.Exists(RootPrefabPath);
            GameObject root = exists
                ? PrefabUtility.LoadPrefabContents(RootPrefabPath)
                : new GameObject("RootLifetimeScope");

            var machine = GetOrAdd<GameFlowStateMachine>(root);
            var scope = GetOrAdd<RootLifetimeScope>(root);

            var machineSo = new SerializedObject(machine);
            SerializedProperty rules = machineSo.FindProperty("rules");
            rules.ClearArray();
            for (int i = 0; i < FlowRules.Length; i++) {
                rules.InsertArrayElementAtIndex(i);
                SerializedProperty rule = rules.GetArrayElementAtIndex(i);
                rule.FindPropertyRelative("from").enumValueIndex = (int)FlowRules[i].from;
                SerializedProperty to = rule.FindPropertyRelative("to");
                to.ClearArray();
                for (int j = 0; j < FlowRules[i].to.Length; j++) {
                    to.InsertArrayElementAtIndex(j);
                    to.GetArrayElementAtIndex(j).enumValueIndex = (int)FlowRules[i].to[j];
                }
            }
            machineSo.ApplyModifiedPropertiesWithoutUndo();

            var scopeSo = new SerializedObject(scope);
            scopeSo.FindProperty("stateMachine").objectReferenceValue = machine;
            scopeSo.FindProperty("gameFlowSettings").objectReferenceValue = flowSettings;
            scopeSo.FindProperty("cameraSettings").objectReferenceValue = cameraSettings;
            scopeSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, RootPrefabPath);
            if (exists) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);

            return AssetDatabase.LoadAssetAtPath<RootLifetimeScope>(RootPrefabPath);
        }

        private static void RegisterVContainerSettings(RootLifetimeScope rootPrefab) {
            var settings = LoadOrCreate<VContainerSettings>(VContainerSettingsPath);
            settings.RootLifetimeScope = rootPrefab;
            EditorUtility.SetDirty(settings);

            List<Object> preloaded = PlayerSettings.GetPreloadedAssets().Where(a => a != null && a is not VContainerSettings).ToList();
            preloaded.Add(settings);
            PlayerSettings.SetPreloadedAssets(preloaded.ToArray());
        }

        private static void SetBuildScenes() {
            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(TestScenePath, true),
            };
        }

        // The Menu scene used to hold its own GameFlowStateMachine; it now lives on the root prefab.
        private static void MigrateMenuScene() {
            var scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            bool dirty = false;
            foreach (GameObject go in scene.GetRootGameObjects()) {
                if (go.GetComponent<GameFlowStateMachine>() == null) continue;
                Object.DestroyImmediate(go);
                dirty = true;
            }
            if (dirty) EditorSceneManager.SaveScene(scene);
        }

        private static void MigrateTestScene() {
            var scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            var scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            if (scope == null) {
                Debug.LogWarning("[ProjectSetup] No GameLifetimeScope in TestScene, skipped.");
                return;
            }

            var quit = Object.FindFirstObjectByType<QuitOverlay>();
            if (quit == null) quit = new GameObject("QuitOverlay").AddComponent<QuitOverlay>();
            scope.quitOverlay = quit;
            EditorUtility.SetDirty(scope);
            EditorSceneManager.SaveScene(scene);
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component =>
            go.TryGetComponent(out T c) ? c : go.AddComponent<T>();
    }
}
#endif
