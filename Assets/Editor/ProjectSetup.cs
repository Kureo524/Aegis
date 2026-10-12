#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Containers;
using Features.CameraControl;
using Features.Characters;
using Features.Combat;
using Features.Progression;
using Features.Spells;
using Features.Stats;
using Systems.Inputs;
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
        private const string ControlsSettingsPath = SettingsDir + "/ControlsSettings.asset";
        private const string SpellSettingsPath = SettingsDir + "/SpellSettings.asset";
        private const string ElementsDir = "Assets/Data/Elements";
        private const string SpellsDir = "Assets/Data/Spells/FireElectricityWind";
        public const string TestSpellBookPath = SpellsDir + "/SpellBook_FireElectricityWind.asset";
        private const string ElementIconsDir = "Assets/GraphicAssets/Sprites/Elements";
        private const string VContainerSettingsPath = SettingsDir + "/VContainerSettings.asset";
        private const string RootPrefabPath = "Assets/Prefabs/RootLifetimeScope.prefab";
        private const string ExperienceCurvePath = "Assets/Data/Progression/ExperienceCurve.asset";
        private const string TestHeroDir = "Assets/Data/Characters/TestHero";
        public const string TestHeroPath = TestHeroDir + "/Char_TestHero.asset";
        private const string DummyDir = "Assets/Data/Characters/TrainingDummy";
        public const string DummyPath = DummyDir + "/Enemy_TrainingDummy.asset";
        private const string DummySpritePath = "Assets/GraphicAssets/Sprites/Characters/Training_Dummy.png";

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
            var controlsSettings = LoadOrCreate<ControlsSettings>(ControlsSettingsPath);
            var spellSettings = LoadOrCreate<SpellSettings>(SpellSettingsPath);
            CreateTestElementsAndSpells();
            CreateTestHero();
            Character playerPrefab = CharacterPrefabSetup.CreatePlayerPrefab();
            AssignMissingHeroSprite();
            AssignMissingHeroProjectile();
            AssignMissingHeroElements();
            CreateTrainingDummy();
            Character dummyPrefab = CharacterPrefabSetup.CreateDummyPrefab();
            RootLifetimeScope rootPrefab = CreateOrUpdateRootPrefab(flowSettings, cameraSettings, experienceCurve, controlsSettings, spellSettings);
            RegisterVContainerSettings(rootPrefab);
            SetBuildScenes();
            MigrateMenuScene();
            MigrateTestScene(playerPrefab, dummyPrefab);

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

        // Training target: lots of HP, no regen, never attacks, respawns (TrainingDummy prefab).
        private static void CreateTrainingDummy() {
            if (AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DummyPath) != null) return;

            var stats = CreateAsset<StatBlock>(DummyDir + "/StatBlock_TrainingDummy.asset");
            SetList(stats, "entries", new (StatType, float)[] {
                (StatType.MaxHealth, 1000f), (StatType.Armor, 30f), (StatType.MagicResist, 30f),
            }, (e, v) => {
                e.FindPropertyRelative("Type").enumValueIndex = (int)v.Item1;
                e.FindPropertyRelative("Value").floatValue = v.Item2;
            });

            var dummy = CreateAsset<EnemyDefinition>(DummyPath);
            var so = new SerializedObject(dummy);
            so.FindProperty("displayName").stringValue = "Training Dummy";
            so.FindProperty("faction").enumValueIndex = (int)Faction.Enemy;
            so.FindProperty("sprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(DummySpritePath);
            so.FindProperty("bodySize").vector2Value = new Vector2(0.9f, 1.6f);
            so.FindProperty("baseStats").objectReferenceValue = stats;
            so.FindProperty("xpReward").intValue = 150;
            so.FindProperty("goldReward").intValue = 25;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Test character elements (design doc §7.8): Fire / Electricity / Wind, 19 recipes. Created once.
        private static readonly (string id, string name, float cost, Color color)[] TestElements = {
            ("Fire", "Feu", 10f, new Color(1f, 0.45f, 0.15f)),
            ("Electricity", "Électricité", 12f, new Color(1f, 0.88f, 0.25f)),
            ("Wind", "Vent", 8f, new Color(0.55f, 0.95f, 0.9f)),
        };

        // (stacks Fire, Electricity, Wind, display name, kind). Placeholder content, tuned in the assets.
        private static readonly (int f, int e, int w, string name, SpellKind kind)[] TestSpells = {
            (1, 0, 0, "Boule de feu", SpellKind.Magic),        (0, 1, 0, "Étincelle", SpellKind.Magic),
            (0, 0, 1, "Rafale", SpellKind.Physical),
            (2, 0, 0, "Grande boule de feu", SpellKind.Magic), (0, 2, 0, "Arc électrique", SpellKind.Magic),
            (0, 0, 2, "Bourrasque", SpellKind.Buff),           (1, 1, 0, "Plasma", SpellKind.Magic),
            (1, 0, 1, "Tornade de feu", SpellKind.Magic),      (0, 1, 1, "Orage", SpellKind.Magic),
            (3, 0, 0, "Météore", SpellKind.Magic),             (0, 3, 0, "Foudre", SpellKind.Magic),
            (0, 0, 3, "Cyclone", SpellKind.Physical),          (2, 1, 0, "Lance de plasma", SpellKind.Magic),
            (2, 0, 1, "Tempête de feu", SpellKind.Magic),      (1, 2, 0, "Surcharge", SpellKind.Buff),
            (0, 2, 1, "Tempête électrique", SpellKind.Magic),  (1, 0, 2, "Souffle ardent", SpellKind.Magic),
            (0, 1, 2, "Ouragan statique", SpellKind.Physical), (1, 1, 1, "Cataclysme", SpellKind.Magic),
        };

        private static void CreateTestElementsAndSpells() {
            if (AssetDatabase.LoadAssetAtPath<SpellBook>(TestSpellBookPath) != null) return;

            var elements = new ElementDefinition[TestElements.Length];
            for (int i = 0; i < TestElements.Length; i++) {
                var (id, displayName, cost, color) = TestElements[i];
                elements[i] = CreateAsset<ElementDefinition>($"{ElementsDir}/Elem_{id}.asset");
                var so = new SerializedObject(elements[i]);
                so.FindProperty("displayName").stringValue = displayName;
                so.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{ElementIconsDir}/Elem_{id}.png");
                so.FindProperty("color").colorValue = color;
                so.FindProperty("manaCost").floatValue = cost;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var spells = new List<SpellDefinition>();
            foreach (var (f, e, w, displayName, kind) in TestSpells) {
                int[] stacks = { f, e, w };
                var parts = new List<string>();
                var recipe = new List<(ElementDefinition, int)>();
                for (int i = 0; i < stacks.Length; i++) {
                    if (stacks[i] == 0) continue;
                    parts.Add(stacks[i] > 1 ? $"{TestElements[i].id}_x{stacks[i]}" : TestElements[i].id);
                    recipe.Add((elements[i], stacks[i]));
                }
                var spell = CreateAsset<SpellDefinition>($"{SpellsDir}/Spell_{string.Join("_", parts)}.asset");
                var so = new SerializedObject(spell);
                so.FindProperty("displayName").stringValue = displayName;
                so.FindProperty("kind").enumValueIndex = (int)kind;
                SerializedProperty list = so.FindProperty("recipe");
                list.arraySize = recipe.Count;
                for (int i = 0; i < recipe.Count; i++) {
                    list.GetArrayElementAtIndex(i).FindPropertyRelative("Element").objectReferenceValue = recipe[i].Item1;
                    list.GetArrayElementAtIndex(i).FindPropertyRelative("Count").intValue = recipe[i].Item2;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                spells.Add(spell);
            }

            var book = CreateAsset<SpellBook>(TestSpellBookPath);
            var bookSo = new SerializedObject(book);
            SerializedProperty bookList = bookSo.FindProperty("spells");
            bookList.arraySize = spells.Count;
            for (int i = 0; i < spells.Count; i++) bookList.GetArrayElementAtIndex(i).objectReferenceValue = spells[i];
            bookSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // Only fills empty slots: never replaces elements or a spell book chosen by hand.
        private static void AssignMissingHeroElements() {
            var hero = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(TestHeroPath);
            if (hero == null || hero.SpellBook != null) return;
            var so = new SerializedObject(hero);
            SerializedProperty slots = so.FindProperty("elements");
            slots.arraySize = TestElements.Length;
            for (int i = 0; i < TestElements.Length; i++)
                slots.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<ElementDefinition>($"{ElementsDir}/Elem_{TestElements[i].id}.asset");
            so.FindProperty("spellBook").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SpellBook>(TestSpellBookPath);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Ranged hero: its basic attack fires the arcane bolt. Only fills an empty slot.
        private static void AssignMissingHeroProjectile() {
            var attack = AssetDatabase.LoadAssetAtPath<BasicAttackDefinition>(TestHeroDir + "/BasicAttack_TestHero.asset");
            if (attack == null || attack.ProjectilePrefab != null) return;
            var so = new SerializedObject(attack);
            so.FindProperty("projectilePrefab").objectReferenceValue = CharacterPrefabSetup.CreateArcaneBoltPrefab();
            so.FindProperty("projectileSpeed").floatValue = 20f;
            so.ApplyModifiedPropertiesWithoutUndo();
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
                                                                   ExperienceCurve experienceCurve, ControlsSettings controlsSettings,
                                                                   SpellSettings spellSettings) {
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
            scopeSo.FindProperty("controlsSettings").objectReferenceValue = controlsSettings;
            scopeSo.FindProperty("spellSettings").objectReferenceValue = spellSettings;
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

        private static void MigrateTestScene(Character playerPrefab, Character dummyPrefab) {
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
            TestSceneSetup.WireCombat(scope, dummyPrefab, AssetDatabase.LoadAssetAtPath<CharacterDefinition>(DummyPath));
            EditorUtility.SetDirty(scope);
            EditorSceneManager.SaveScene(scene);
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component =>
            go.TryGetComponent(out T c) ? c : go.AddComponent<T>();
    }
}
#endif
