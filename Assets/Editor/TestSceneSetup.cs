#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Containers;
using System.Collections.Generic;
using Features.CameraControl;
using Features.Characters;
using Features.Controls;
using Features.Movement;
using Features.UI;
using Features.UI.DebugTools;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace EditorTools {
    /// <summary>
    /// Regenerates the test scene from scratch (overwrites it) and validates its wiring.
    /// Shared services (event bus, input, game flow, settings) come from the RootLifetimeScope
    /// prefab created by <see cref="ProjectSetup"/>: run that first.
    /// </summary>
    public static class TestSceneSetup {
        private const string ScenePath = ProjectSetup.TestScenePath;

        // NOTE: the baked NavMeshData is binary-only. Left inside the scene, it forces Unity to save
        // the whole scene in binary (no diffs, no merges): BakeNavMesh stores it as its own asset
        // next to the scene instead, so TestScene.unity stays YAML.

        [MenuItem("Tools/Create Test Scene")]
        public static void CreateTestScene() {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ground
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10, 1, 10);
            ground.GetComponent<Renderer>().material.color = new Color(0.2f, 0.3f, 0.2f);

            // NavMeshSurface (baked once the obstacles exist, see below)
            var navSurface = ground.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            navSurface.collectObjects = Unity.AI.Navigation.CollectObjects.All;

            CreateObstacles();
            BakeNavMesh(navSurface);

            // Player spawn point: the player itself is spawned from its prefab at runtime (PlayerSpawner)
            var spawn = new GameObject(PlayerSpawnName).transform;

            // Camera
            var cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            var camera = cameraObj.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 10, -8);
            camera.transform.rotation = Quaternion.LookRotation(-new Vector3(0, 10, -8));
            cameraObj.AddComponent<AudioListener>();

            var cameraController = cameraObj.AddComponent<CameraController>();

            // Light
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);

            // Quit affordance: a windowed build with a confined cursor cannot be closed
            // otherwise (the WM close button is unreachable).
            var quitObj = new GameObject("QuitOverlay");
            var quit = quitObj.AddComponent<QuitOverlay>();

            // Scene LifetimeScope (DI): child of the RootLifetimeScope, injects the scene components
            var scopeObj = new GameObject("GameLifetimeScope");
            var scope = scopeObj.AddComponent<GameLifetimeScope>();
            scope.mainCamera = camera;
            scope.cameraController = cameraController;
            scope.quitOverlay = quit;
            scope.playerSpawn = spawn;
            WirePlayer(scope, CharacterPrefabSetup.CreatePlayerPrefab(),
                       AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ProjectSetup.TestHeroPath));
            WireCombat(scope, CharacterPrefabSetup.CreateDummyPrefab(),
                       AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ProjectSetup.DummyPath));

            // Save scene
            System.IO.Directory.CreateDirectory("Assets/Scenes/Tests");
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"[TestSceneSetup] Test scene created at {ScenePath}");
        }

        private const string PlayerSpawnName = "PlayerSpawn";

        /// <summary>
        /// Points the scope at the player prefab, its definition and the spawn point, and adds the
        /// character debug panel. Idempotent.
        /// </summary>
        public static void WirePlayer(GameLifetimeScope scope, Character playerPrefab, CharacterDefinition definition) {
            var panel = Object.FindFirstObjectByType<CharacterDebugPanel>();
            if (panel == null) panel = new GameObject("CharacterDebugPanel").AddComponent<CharacterDebugPanel>();

            scope.playerPrefab = playerPrefab;
            scope.playerDefinition = definition;
            scope.characterDebugPanel = panel;
            EditorUtility.SetDirty(scope);
        }

        /// <summary>
        /// Migrates a test scene whose player was a capsule placed in the scene: removes it, adds a
        /// spawn point at its position and wires the scope to spawn the Player prefab instead.
        /// Rebakes the NavMesh (the old capsule was part of the bake). Idempotent.
        /// </summary>
        public static void MigrateToSpawnedPlayer(GameLifetimeScope scope, Character playerPrefab, CharacterDefinition definition) {
            Vector3 spawnPosition = Vector3.zero;
            foreach (var movement in Object.FindObjectsByType<CharacterMovement>(FindObjectsSortMode.None)) {
                spawnPosition = new Vector3(movement.transform.position.x, 0f, movement.transform.position.z);
                Object.DestroyImmediate(movement.gameObject);
            }

            var spawnGo = GameObject.Find(PlayerSpawnName);
            if (spawnGo == null) {
                spawnGo = new GameObject(PlayerSpawnName);
                spawnGo.transform.position = spawnPosition;
            }

            var cameraController = Object.FindFirstObjectByType<CameraController>();
            if (cameraController != null) cameraController.target = null;   // set at spawn time

            scope.mainCamera = cameraController != null ? cameraController.GetComponent<Camera>() : null;
            scope.cameraController = cameraController;
            scope.playerSpawn = spawnGo.transform;
            WirePlayer(scope, playerPrefab, definition);

            var surface = Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>();
            if (surface != null) BakeNavMesh(surface);
        }

        private const string DummiesRoot = "=== Dummies ===";
        private const string PanelSettingsPath = "Assets/UI Toolkit/PanelSettings.asset";

        // Two dummies in range of the spawn, one that has to be chased
        private static readonly Vector3[] DummyPositions = {
            new(3f, 0f, 3f), new(-4f, 0f, 2f), new(0f, 0f, -7f),
        };

        /// <summary>
        /// Training dummies (spawn points + scope spawn list) and the game HUD (player bars, damage
        /// numbers) in the test scene. Idempotent.
        /// </summary>
        public static void WireCombat(GameLifetimeScope scope, Character dummyPrefab, CharacterDefinition dummyDefinition) {
            // Reuses the existing points: recreating them would rewrite their IDs in the scene file
            // on every run (noisy diffs). Positions moved by hand are kept.
            var root = GameObject.Find(DummiesRoot) ?? new GameObject(DummiesRoot);

            scope.enemySpawns = new List<CharacterSpawn>();
            for (int i = 0; i < DummyPositions.Length; i++) {
                string pointName = $"DummySpawn_{i + 1}";
                Transform point = root.transform.Find(pointName);
                if (point == null) {
                    point = new GameObject(pointName).transform;
                    point.SetParent(root.transform);
                    point.position = DummyPositions[i];
                }
                scope.enemySpawns.Add(new CharacterSpawn { Prefab = dummyPrefab, Definition = dummyDefinition, Point = point });
            }

            var hud = Object.FindFirstObjectByType<PlayerHud>();
            if (hud == null) {
                var hudGo = new GameObject("GameHud");
                var document = hudGo.AddComponent<UIDocument>();
                document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
                hud = hudGo.AddComponent<PlayerHud>();
                hudGo.AddComponent<DamageNumbersView>();
            }
            scope.playerHud = hud;
            scope.damageNumbers = hud.GetComponent<DamageNumbersView>();
            EditorUtility.SetDirty(scope);
        }

        private const string ObstaclesRoot = "=== Obstacles ===";
        private const string NavMeshAssetPath = "Assets/Scenes/Tests/TestScene/NavMesh-Ground.asset";

        // Pathfinding test course around the spawn (player at the origin, ground is 100 x 100):
        // (name, position, size). Height 2 so the character cannot step onto them.
        private static readonly (string name, Vector3 position, Vector3 size)[] Obstacles = {
            ("Wall_Front",     new Vector3(  0f, 1f,   6f), new Vector3(10f,  2f, 1f)),   // straight wall: go around
            ("L_Long",         new Vector3(  9f, 1f,  -4f), new Vector3( 1f,  2f, 9f)),   // L corner
            ("L_Short",        new Vector3( 12f, 1f,  -8f), new Vector3( 7f,  2f, 1f)),
            ("U_Back",         new Vector3(-15f, 1f,   0f), new Vector3( 1f,  2f, 9f)),   // U trap, open towards the spawn
            ("U_Top",          new Vector3(-11.5f, 1f, 4f), new Vector3( 6f,  2f, 1f)),
            ("U_Bottom",       new Vector3(-11.5f, 1f,-4f), new Vector3( 6f,  2f, 1f)),
            ("Pillar_1",       new Vector3( -3f, 1f,  12f), new Vector3(1.5f, 2f, 1.5f)), // pillar field
            ("Pillar_2",       new Vector3(  0f, 1f,  14f), new Vector3(1.5f, 2f, 1.5f)),
            ("Pillar_3",       new Vector3(  3f, 1f,  12f), new Vector3(1.5f, 2f, 1.5f)),
            ("Pillar_4",       new Vector3( -1.5f, 1f, 17f), new Vector3(1.5f, 2f, 1.5f)),
            ("Pillar_5",       new Vector3(  1.5f, 1f, 17f), new Vector3(1.5f, 2f, 1.5f)),
            ("Corridor_Left",  new Vector3( 18f, 1f,  10f), new Vector3( 1f,  2f, 12f)),  // narrow corridor
            ("Corridor_Right", new Vector3( 21f, 1f,  10f), new Vector3( 1f,  2f, 12f)),
        };

        /// <summary>
        /// Adds (or rebuilds) the pathfinding test course in the existing test scene and rebakes
        /// the NavMesh, without touching anything else in the scene.
        ///   Unity -batchmode -quit -executeMethod EditorTools.TestSceneSetup.AddTestObstacles
        /// </summary>
        [MenuItem("Tools/Add Test Obstacles")]
        public static void AddTestObstacles() {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            CreateObstacles();

            var surface = Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>();
            if (surface == null) {
                Debug.LogError("[TestSceneSetup] No NavMeshSurface in the test scene.");
                return;
            }
            BakeNavMesh(surface);

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[TestSceneSetup] {Obstacles.Length} obstacles added, NavMesh rebaked.");
        }

        private static void CreateObstacles() {
            var existing = GameObject.Find(ObstaclesRoot);
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject(ObstaclesRoot);
            foreach (var (name, position, size) in Obstacles) {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = name;
                cube.transform.SetParent(root.transform);
                cube.transform.position = position;
                cube.transform.localScale = size;
                cube.isStatic = true;

                // Carved out of the NavMesh (top faces included) instead of becoming walkable islands
                var modifier = cube.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
                modifier.overrideArea = true;
                modifier.area = UnityEngine.AI.NavMesh.GetAreaFromName("Not Walkable");
            }
        }

        // Bakes and stores the NavMeshData as an asset next to the scene: data left inside the
        // scene would force Unity to save the scene in binary.
        private static void BakeNavMesh(Unity.AI.Navigation.NavMeshSurface surface) {
            surface.BuildNavMesh();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(NavMeshAssetPath)!);
            AssetDatabase.DeleteAsset(NavMeshAssetPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAssetPath);
            EditorUtility.SetDirty(surface);
        }

        /// <summary>
        /// Opens the saved test scene and asserts every reference the scene must carry.
        /// Run headless so a broken scene fails loudly instead of silently in Play mode:
        ///   Unity -batchmode -executeMethod EditorTools.TestSceneSetup.ValidateTestScene
        /// </summary>
        [MenuItem("Tools/Validate Test Scene")]
        public static void ValidateTestScene() {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var errors = new System.Text.StringBuilder();
            int checks = 0;

            void Check(bool ok, string label) {
                checks++;
                if (!ok) errors.AppendLine($"  FAIL: {label}");
                Debug.Log($"  {(ok ? "ok  " : "FAIL")} {label}");
            }

            var scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            Check(scope != null, "GameLifetimeScope present in scene");

            var cameraController = Object.FindFirstObjectByType<CameraController>();
            Check(cameraController != null, "CameraController present in scene");

            Check(Object.FindFirstObjectByType<CharacterMovement>() == null,
                  "no character placed in the scene (the player is spawned from its prefab)");

            var surface = Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>();
            Check(surface != null, "NavMeshSurface present on Ground");

            var quit = Object.FindFirstObjectByType<QuitOverlay>();
            Check(quit != null, "QuitOverlay present in scene");

            var obstacles = GameObject.Find(ObstaclesRoot);
            Check(obstacles != null && obstacles.transform.childCount == Obstacles.Length, "pathfinding obstacles present");
            Check(surface != null && surface.navMeshData != null && AssetDatabase.Contains(surface.navMeshData),
                  "NavMesh baked and stored as an asset");

            if (scope != null) {
                Check(scope.mainCamera != null, "scope.mainCamera wired");
                Check(scope.cameraController != null, "scope.cameraController wired");
                Check(scope.quitOverlay != null, "scope.quitOverlay wired");
                Check(scope.characterDebugPanel != null, "scope.characterDebugPanel wired");
                Check(scope.playerSpawn != null, "scope.playerSpawn wired");
                Check(scope.playerPrefab != null, "scope.playerPrefab wired");
                Check(scope.playerHud != null, "scope.playerHud wired");
                Check(scope.damageNumbers != null, "scope.damageNumbers wired");
                Check(scope.playerHud != null && scope.playerHud.GetComponent<UIDocument>().panelSettings != null,
                      "game HUD has panel settings");
                Check(scope.enemySpawns.Count == DummyPositions.Length, "training dummies listed in scope.enemySpawns");
                foreach (CharacterSpawn spawn in scope.enemySpawns) {
                    Check(spawn.Prefab != null && spawn.Definition is EnemyDefinition && spawn.Point != null,
                          $"dummy spawn {(spawn.Point != null ? spawn.Point.name : "?")} complete");
                }
                Check(scope.playerDefinition != null, "scope.playerDefinition wired");
                if (scope.playerDefinition != null) {
                    Check(scope.playerDefinition.Faction == Faction.Player, "player definition is in the Player faction");
                    Check(scope.playerDefinition.BaseStats != null, "player definition has base stats");
                    Check(scope.playerDefinition.Sprite != null, "player definition has a sprite");
                    var attack = scope.playerDefinition.BasicAttack;
                    Check(attack != null && attack.ProjectilePrefab != null && attack.ProjectileSpeed > 0f,
                          "player basic attack fires a projectile");
                    Check(scope.playerDefinition.HasElements, "player has 3 elements and a spell book");
                    var signature = scope.playerDefinition.SignatureAbility;
                    Check(signature != null && signature.CooldownOverride > 0f && signature.Icon != null, "player has a signature ability (F) with a cooldown");
                    if (scope.playerDefinition.HasElements) {
                        var resolver = new Features.Spells.SpellResolver(scope.playerDefinition.SpellBook, scope.playerDefinition.Elements);
                        Check(resolver.Problems.Count == 0 && resolver.MissingKeys(3, 3).Count == 0 && resolver.Count == 19,
                              "player spell book covers the 19 recipes, no duplicate");
                    }
                }
                if (scope.playerPrefab != null) {
                    Character prefab = scope.playerPrefab;
                    var characterSo = new SerializedObject(prefab);
                    foreach (string field in new[] { "movement", "visual", "body", "agent", "attacker", "healthBar" })
                        Check(characterSo.FindProperty(field).objectReferenceValue != null, $"Player prefab: character.{field} wired");

                    var input = prefab.GetComponent<PlayerCommandInput>();
                    Check(input != null, "Player prefab has a PlayerCommandInput");
                    if (input != null) {
                        Check(input.characterMovement != null, "Player prefab: input.characterMovement wired");
                        Check(input.clickMarkerPrefab != null, "Player prefab: input.clickMarkerPrefab wired");
                        Check(input.character != null && input.attacker != null, "Player prefab: input.character / attacker wired");
                        Check(input.rangeIndicator != null, "Player prefab: input.rangeIndicator wired");
                        var caster = prefab.GetComponent<Features.Spells.ElementCaster>();
                        Check(caster != null && new SerializedObject(caster).FindProperty("owner").objectReferenceValue != null,
                              "Player prefab: ElementCaster wired");
                        foreach (var (component, fields) in new (Component, string[])[] {
                                     (caster, new[] { "targeting", "caster" }),
                                     (prefab.GetComponent<Features.Spells.SpellTargeting>(), new[] { "owner", "indicator", "rangeIndicator" }),
                                     (prefab.GetComponent<Features.Spells.SignatureAbility>(), new[] { "owner", "targeting", "caster" }),
                                     (prefab.GetComponent<Features.Spells.SpellCaster>(), new[] { "owner", "movement", "attacker", "visual" }),
                                 }) {
                            bool ok = component != null;
                            if (ok) {
                                var so = new SerializedObject(component);
                                foreach (string f in fields) ok &= so.FindProperty(f).objectReferenceValue != null;
                            }
                            Check(ok, $"Player prefab: {(component != null ? component.GetType().Name : "component")} wired");
                        }
                        Check(input.spellCaster != null, "Player prefab: input.spellCaster wired");
                    }

                    var rb = prefab.GetComponent<Rigidbody>();
                    Check(rb != null && rb.interpolation == RigidbodyInterpolation.Interpolate, "Player prefab: Rigidbody interpolated");

                    var visual = prefab.GetComponentInChildren<CharacterVisual>();
                    Check(visual != null && visual.transform != prefab.transform, "Player prefab: CharacterVisual on a child");

                    Check(PrefabUtility.GetPrefabAssetType(prefab.gameObject) == PrefabAssetType.Variant
                          && PrefabUtility.GetCorrespondingObjectFromSource(prefab.gameObject) ==
                             AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabSetup.BasePrefabPath),
                          "Player prefab is a variant of Character.prefab");
                }
            }

            // Root scope: the scene relies on it for every shared service
            var vcSettings = PlayerSettings.GetPreloadedAssets().OfType<VContainerSettings>().FirstOrDefault();
            Check(vcSettings != null, "VContainerSettings registered in preloaded assets");
            var root = vcSettings != null ? vcSettings.RootLifetimeScope as RootLifetimeScope : null;
            Check(root != null, "VContainerSettings.RootLifetimeScope is the RootLifetimeScope prefab");
            if (root != null) {
                var rootSo = new SerializedObject(root);
                Check(rootSo.FindProperty("stateMachine").objectReferenceValue != null, "root.stateMachine wired");
                Check(rootSo.FindProperty("gameFlowSettings").objectReferenceValue != null, "root.gameFlowSettings wired");
                Check(rootSo.FindProperty("cameraSettings").objectReferenceValue != null, "root.cameraSettings wired");
                Check(rootSo.FindProperty("experienceCurve").objectReferenceValue != null, "root.experienceCurve wired");
                Check(rootSo.FindProperty("controlsSettings").objectReferenceValue != null, "root.controlsSettings wired");
                Check(rootSo.FindProperty("spellSettings").objectReferenceValue != null, "root.spellSettings wired");
                var spellSettings = rootSo.FindProperty("spellSettings").objectReferenceValue as Features.Spells.SpellSettings;
                Check(spellSettings != null && spellSettings.HomingProjectile != null && spellSettings.SkillshotProjectile != null
                      && spellSettings.ShapeVfx != null, "spell settings: effect prefabs assigned");
            }

            if (errors.Length > 0) {
                Debug.LogError($"[TestSceneSetup] VALIDATION FAILED ({checks} checks):\n{errors}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
            else {
                Debug.Log($"[TestSceneSetup] VALIDATION PASSED ({checks} checks) in scene '{scene.name}'");
                // Exit code for the command line only: from the Tools menu this would close the Editor.
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        }
    }
}
#endif
