#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Containers;
using Features.CameraControl;
using Features.Characters;
using Features.Combat;
using Features.Economy;
using Features.Enemies;
using Features.Run;
using Features.Progression;
using Features.Spells;
using Features.Stats;
using Systems.Inputs;
using Features.Flow;
using Features.StateMachines;
using Features.UI;
using Features.UI.Panels;
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
        public const string FlashPath = "Assets/Data/Spells/Signature/Spell_Flash.asset";
        private const string FlashIconPath = "Assets/GraphicAssets/Sprites/Spells/Spell_Flash.png";
        private const string VContainerSettingsPath = SettingsDir + "/VContainerSettings.asset";
        internal const string RootPrefabPath = "Assets/Prefabs/RootLifetimeScope.prefab";
        internal const string ExperienceCurvePath = "Assets/Data/Progression/ExperienceCurve.asset";
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
            AssignSpellPrefabs(spellSettings);
            CreateTestHero();
            Character playerPrefab = CharacterPrefabSetup.CreatePlayerPrefab();
            AssignMissingHeroSprite();
            AssignMissingHeroProjectile();
            HeroSetup.Run(CreateFlash());   // the champion (Vesna): abilities, passive, stats, summoner spells
            CreateTrainingDummy();
            Character dummyPrefab = CharacterPrefabSetup.CreateDummyPrefab();
            Character enemyPrefab = CharacterPrefabSetup.CreateEnemyPrefab();
            HordeSettings hordeSettings = EnemySetup.Run(enemyPrefab);
            StageDefinition stage = RunSetup.Run(enemyPrefab);
            ShopCatalog shopCatalog = EconomySetup.Run();
            ItemsSetup.Run();            // the items (before the boss: he carries some)
            (Features.Augments.AugmentCatalog augmentCatalog, Features.Loot.LootSettings lootSettings) = PassiveSetup.Run();
            BossSetup.Run(enemyPrefab);   // after the items: the boss carries some
            ContentSetup.Run();           // phase 9b: statuses on spells, more items and augments
            EnemyContentSetup.Run(enemyPrefab);   // phase 9c: wolf, skeleton mage, goblin shaman, stone golem
            AugmentsSetup.Run();         // the augments (catalog = exactly that list)
            RootLifetimeScope rootPrefab = CreateOrUpdateRootPrefab(flowSettings, cameraSettings, experienceCurve, controlsSettings, spellSettings);
            RegisterVContainerSettings(rootPrefab);
            UiSetup.Run();               // MOBA-style UI: theme on the PanelSettings, stat icons
            SetBuildScenes();
            MigrateMenuScene();
            MetaSetup.Run(RootPrefabPath, MenuScenePath);   // meta progression: settings, Vesna's tree, Progression screen
            CharacterSpritesSetup.Run();   // animated sprites, creature names, ground shadows
            ArenaSetup.Run();   // forest clearing: rocks, trees, ruins, painted ground, light, fog, post-processing
            MigrateTestScene(playerPrefab, dummyPrefab, hordeSettings, stage, shopCatalog, augmentCatalog, lootSettings);

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
            so.FindProperty("lastSkillPointLevel").intValue = 18;   // a skill point on every level (MOBA)
            so.ApplyModifiedPropertiesWithoutUndo();
            return curve;
        }

        // Test character of design doc §7.8: placeholder assets, turned into Vesna by HeroSetup.
        private static void CreateTestHero() {
            if (AssetDatabase.LoadAssetAtPath<CharacterDefinition>(TestHeroPath) != null) return;

            var stats = CreateAsset<StatBlock>(TestHeroDir + "/StatBlock_TestHero.asset");
            SetList(stats, "entries", new (StatType, float)[] {
                (StatType.MaxHealth, 600f), (StatType.HealthRegen, 1.5f),
                (StatType.MaxMana, 400f), (StatType.ManaRegen, 8f),
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
            growthSo.FindProperty("mode").enumValueIndex = (int)GrowthMode.Curved;
            growthSo.ApplyModifiedPropertiesWithoutUndo();
            SetList(growth, "entries", new (StatType, float, ModifierType)[] {
                (StatType.MaxHealth, 100f, ModifierType.Flat), (StatType.HealthRegen, 0.1f, ModifierType.Flat),
                (StatType.MaxMana, 30f, ModifierType.Flat), (StatType.ManaRegen, 0.4f, ModifierType.Flat),
                (StatType.PhysicalAttack, 3f, ModifierType.Flat), (StatType.AttackSpeed, 0.02f, ModifierType.PercentAdd),
                (StatType.Armor, 4f, ModifierType.Flat), (StatType.MagicResist, 1.3f, ModifierType.Flat),
            }, (e, v) => {
                e.FindPropertyRelative("Stat").enumValueIndex = (int)v.Item1;
                e.FindPropertyRelative("GainPerLevel").floatValue = v.Item2;
                e.FindPropertyRelative("Type").enumValueIndex = (int)v.Item3;
            });

            var attack = CreateAsset<BasicAttackDefinition>(TestHeroDir + "/BasicAttack_TestHero.asset");
            var attackSo = new SerializedObject(attack);
            attackSo.FindProperty("windupPercent").floatValue = 0.18f;   // MOBA marksman-like
            attackSo.ApplyModifiedPropertiesWithoutUndo();

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

        private static void AssignSpellPrefabs(SpellSettings settings) {
            var so = new SerializedObject(settings);
            void Fill(string field, Object value) {
                SerializedProperty p = so.FindProperty(field);
                if (p.objectReferenceValue == null) p.objectReferenceValue = value;
            }
            Fill("homingProjectile", CharacterPrefabSetup.CreateArcaneBoltPrefab());
            Fill("skillshotProjectile", CharacterPrefabSetup.CreateSkillshotPrefab());
            Fill("shapeVfx", CharacterPrefabSetup.CreateShapeVfxPrefab());
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Flash (design doc §7.8): fixed-length blink towards the cursor. Created once.
        internal static SpellDefinition CreateFlash() {
            var flash = AssetDatabase.LoadAssetAtPath<SpellDefinition>(FlashPath);
            if (flash != null) return flash;
            flash = CreateAsset<SpellDefinition>(FlashPath);
            var so = new SerializedObject(flash);
            so.FindProperty("displayName").stringValue = "Bond éclair";
            so.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(FlashIconPath);
            so.FindProperty("color").colorValue = new Color(1f, 0.9f, 0.4f);
            so.FindProperty("targeting").enumValueIndex = (int)TargetingType.GroundPoint;
            so.FindProperty("range").floatValue = 4.5f;
            so.FindProperty("radius").floatValue = 0.5f;
            so.FindProperty("castTime").floatValue = 0f;
            so.FindProperty("canMoveWhileCasting").boolValue = true;
            SpellAssets.SetCooldown(so, 20f);
            so.FindProperty("kind").enumValueIndex = (int)SpellKind.Buff;
            so.FindProperty("blink").boolValue = true;
            so.FindProperty("fixedRange").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            return flash;
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
        // It also gets the options screen (Settings button) above the main menu.
        private static void MigrateMenuScene() {
            var scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            bool dirty = false;
            foreach (GameObject go in scene.GetRootGameObjects()) {
                if (go.GetComponent<GameFlowStateMachine>() == null) continue;
                Object.DestroyImmediate(go);
                dirty = true;
            }

            var scope = Object.FindFirstObjectByType<MenuLifetimeScope>();
            if (scope != null && scope.optionsPanel == null) {
                var options = Object.FindFirstObjectByType<OptionsPanel>();
                if (options == null) options = TestSceneSetup.CreateOverlayDocument<OptionsPanel>("OptionsPanel");
                scope.optionsPanel = options;
                EditorUtility.SetDirty(scope);
                dirty = true;
            }
            if (dirty) EditorSceneManager.SaveScene(scene);
        }

        private static void MigrateTestScene(Character playerPrefab, Character dummyPrefab, HordeSettings hordeSettings,
                                             StageDefinition stage, ShopCatalog shopCatalog,
                                             Features.Augments.AugmentCatalog augmentCatalog, Features.Loot.LootSettings lootSettings) {
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
            if (scope.hordeSettings == null) scope.hordeSettings = hordeSettings;
            TestSceneSetup.WireRun(scope, stage);
            TestSceneSetup.WireShop(scope, shopCatalog);
            TestSceneSetup.WireAugments(scope, augmentCatalog, lootSettings);
            EditorUtility.SetDirty(scope);
            EditorSceneManager.SaveScene(scene);
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component =>
            go.TryGetComponent(out T c) ? c : go.AddComponent<T>();
    }
}
#endif
