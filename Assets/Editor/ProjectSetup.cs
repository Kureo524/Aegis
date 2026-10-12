#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Containers;
using Features.CameraControl;
using Features.Characters;
using Features.Combat;
using Features.Progression;
using Features.Stats;
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
    /// - ExperienceCurve and the test hero data (Char_TestHero + its stats, growth, basic attack).
    ///   Created once with placeholder values: later runs never overwrite designer edits.
    /// - character prefabs (Character + Player variant) and a placeholder sprite, see CharacterPrefabSetup
    /// - scene migrations (Menu: drop its old local state machine; TestScene: wire QuitOverlay,
    ///   spawn the player from its prefab, debug panel)
    /// </summary>
    public static class ProjectSetup {
        private const string SettingsDir = "Assets/Data/Settings";
        private const string GameFlowSettingsPath = SettingsDir + "/GameFlowSettings.asset";
        private const string CameraSettingsPath = SettingsDir + "/CameraSettings.asset";
        private const string VContainerSettingsPath = SettingsDir + "/VContainerSettings.asset";
        private const string RootPrefabPath = "Assets/Prefabs/RootLifetimeScope.prefab";
        private const string ExperienceCurvePath = "Assets/Data/Progression/ExperienceCurve.asset";
        private const string TestHeroDir = "Assets/Data/Characters/TestHero";
        public const string TestHeroPath = TestHeroDir + "/Char_TestHero.asset";

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
            var experienceCurve = CreateExperienceCurve();
            CreateTestHero();
            Character playerPrefab = CharacterPrefabSetup.CreatePlayerPrefab();
            AssignMissingHeroSprite();
            RootLifetimeScope rootPrefab = CreateOrUpdateRootPrefab(flowSettings, cameraSettings, experienceCurve);
            RegisterVContainerSettings(rootPrefab);
            SetBuildScenes();
            MigrateMenuScene();
            MigrateTestScene(playerPrefab);

            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Done.");
        }

        // Placeholder balance values, tuned later in the assets themselves.
        private static ExperienceCurve CreateExperienceCurve() {
            var curve = AssetDatabase.LoadAssetAtPath<ExperienceCurve>(ExperienceCurvePath);
            if (curve != null) return curve;

            curve = CreateAsset<ExperienceCurve>(ExperienceCurvePath);
            var so = new SerializedObject(curve);
            SerializedProperty xp = so.FindProperty("xpToNextLevel");
            xp.arraySize = 17;   // max level 18
            for (int i = 0; i < xp.arraySize; i++) xp.GetArrayElementAtIndex(i).intValue = 280 + 100 * i;
            so.FindProperty("lastSkillPointLevel").intValue = 15;
            so.ApplyModifiedPropertiesWithoutUndo();
            return curve;
        }

        // Test character of design doc §7.8 (Fire / Electricity / Wind mage, added with the elements).
        private static void CreateTestHero() {
            if (AssetDatabase.LoadAssetAtPath<CharacterDefinition>(TestHeroPath) != null) return;

            var stats = CreateAsset<StatBlock>(TestHeroDir + "/StatBlock_TestHero.asset");
            SetList(stats, "entries", new (StatType, float)[] {
                (StatType.MaxHealth, 600f), (StatType.HealthRegen, 1.5f),
                (StatType.MaxMana, 400f), (StatType.ManaRegen, 1.5f),
                (StatType.PhysicalAttack, 55f), (StatType.MagicAttack, 0f),
                (StatType.AttackSpeed, 0.65f), (StatType.AttackRange, 5.5f),
                (StatType.CritDamage, 1.75f),
                (StatType.Armor, 25f), (StatType.MagicResist, 30f),
                (StatType.MoveSpeed, 5f), (StatType.HealShieldPower, 1f),
            }, (e, v) => {
                e.FindPropertyRelative("Type").enumValueIndex = (int)v.Item1;
                e.FindPropertyRelative("Value").floatValue = v.Item2;
            });

            var growth = CreateAsset<StatGrowth>(TestHeroDir + "/StatGrowth_TestHero.asset");
            var growthSo = new SerializedObject(growth);
            growthSo.FindProperty("mode").enumValueIndex = (int)GrowthMode.LoL;
            growthSo.ApplyModifiedPropertiesWithoutUndo();
            SetList(growth, "entries", new (StatType, float, ModifierType)[] {
                (StatType.MaxHealth, 100f, ModifierType.Flat), (StatType.HealthRegen, 0.1f, ModifierType.Flat),
                (StatType.MaxMana, 30f, ModifierType.Flat), (StatType.ManaRegen, 0.08f, ModifierType.Flat),
                (StatType.PhysicalAttack, 3f, ModifierType.Flat), (StatType.AttackSpeed, 0.02f, ModifierType.PercentAdd),
                (StatType.Armor, 4f, ModifierType.Flat), (StatType.MagicResist, 1.3f, ModifierType.Flat),
            }, (e, v) => {
                e.FindPropertyRelative("Stat").enumValueIndex = (int)v.Item1;
                e.FindPropertyRelative("GainPerLevel").floatValue = v.Item2;
                e.FindPropertyRelative("Type").enumValueIndex = (int)v.Item3;
            });

            var attack = CreateAsset<BasicAttackDefinition>(TestHeroDir + "/BasicAttack_TestHero.asset");

            var hero = CreateAsset<CharacterDefinition>(TestHeroPath);
            var heroSo = new SerializedObject(hero);
            heroSo.FindProperty("displayName").stringValue = "Test Hero";
            heroSo.FindProperty("faction").enumValueIndex = (int)Faction.Player;
            heroSo.FindProperty("baseStats").objectReferenceValue = stats;
            heroSo.FindProperty("growthPerLevel").objectReferenceValue = growth;
            heroSo.FindProperty("basicAttack").objectReferenceValue = attack;
            heroSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // Only fills an empty sprite slot: never replaces art chosen by hand.
        private static void AssignMissingHeroSprite() {
            var hero = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(TestHeroPath);
            if (hero == null || hero.Sprite != null) return;
            var so = new SerializedObject(hero);
            so.FindProperty("sprite").objectReferenceValue = CharacterPrefabSetup.CreatePlaceholderSprite();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T CreateAsset<T>(string path) where T : ScriptableObject {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void SetList<TValue>(Object target, string property, TValue[] values,
                                            System.Action<SerializedProperty, TValue> write) {
            var so = new SerializedObject(target);
            SerializedProperty list = so.FindProperty(property);
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) write(list.GetArrayElementAtIndex(i), values[i]);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static RootLifetimeScope CreateOrUpdateRootPrefab(GameFlowSettings flowSettings, CameraSettings cameraSettings,
                                                                   ExperienceCurve experienceCurve) {
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
            scopeSo.FindProperty("experienceCurve").objectReferenceValue = experienceCurve;
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

        private static void MigrateTestScene(Character playerPrefab) {
            var scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            var scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            if (scope == null) {
                Debug.LogWarning("[ProjectSetup] No GameLifetimeScope in TestScene, skipped.");
                return;
            }

            var quit = Object.FindFirstObjectByType<QuitOverlay>();
            if (quit == null) quit = new GameObject("QuitOverlay").AddComponent<QuitOverlay>();
            scope.quitOverlay = quit;

            TestSceneSetup.MigrateToSpawnedPlayer(scope, playerPrefab,
                                                  AssetDatabase.LoadAssetAtPath<CharacterDefinition>(TestHeroPath));
            EditorUtility.SetDirty(scope);
            EditorSceneManager.SaveScene(scene);
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component =>
            go.TryGetComponent(out T c) ? c : go.AddComponent<T>();
    }
}
#endif
