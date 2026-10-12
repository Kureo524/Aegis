#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Containers;
using Features.CameraControl;
using Features.Characters;
using Features.Controls;
using Features.Movement;
using Features.UI;
using Features.UI.DebugTools;
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

            // Player
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0, 1, 0);
            player.GetComponent<Renderer>().material.color = Color.blue;

            var rb = player.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            var agent = player.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.speed = 5f;
            agent.acceleration = 1000f;
            agent.angularSpeed = 360f;
            agent.autoBraking = false;

            var movement = player.AddComponent<CharacterMovement>();

            var input = player.AddComponent<PlayerCommandInput>();
            input.characterMovement = movement;

            // Camera
            var cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            var camera = cameraObj.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 10, -8);
            camera.transform.rotation = Quaternion.Euler(45, 0, 0);
            cameraObj.AddComponent<AudioListener>();

            var cameraController = cameraObj.AddComponent<CameraController>();
            cameraController.target = player.transform;

            input.mainCamera = camera;

            // Light
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);

            // Click Marker Prefab
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "ClickMarker";
            marker.transform.localScale = new Vector3(0.3f, 0.1f, 0.3f);
            marker.GetComponent<Renderer>().material.color = Color.green;
            Object.DestroyImmediate(marker.GetComponent<Collider>());

            marker.AddComponent<ClickMarker>();

            string prefabPath = "Assets/Prefabs/ClickMarker.prefab";
            System.IO.Directory.CreateDirectory("Assets/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(marker, prefabPath);
            Object.DestroyImmediate(marker);

            // Assign marker prefab to input
            var markerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            input.clickMarkerPrefab = markerPrefab;

            // Quit affordance: a windowed build with a confined cursor cannot be closed
            // otherwise (the WM close button is unreachable).
            var quitObj = new GameObject("QuitOverlay");
            var quit = quitObj.AddComponent<QuitOverlay>();

            // Scene LifetimeScope (DI): child of the RootLifetimeScope, injects the scene components
            var scopeObj = new GameObject("GameLifetimeScope");
            var scope = scopeObj.AddComponent<GameLifetimeScope>();
            scope.playerCommandInput = input;
            scope.cameraController = cameraController;
            scope.quitOverlay = quit;
            WirePlayer(scope, movement, AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ProjectSetup.TestHeroPath));

            // Save scene
            System.IO.Directory.CreateDirectory("Assets/Scenes/Tests");
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"[TestSceneSetup] Test scene created at {ScenePath}");
        }

        /// <summary>
        /// Makes the scene's player a data-driven <see cref="Character"/> (design doc §8) and adds
        /// the character debug panel. Idempotent.
        /// </summary>
        public static void WirePlayer(GameLifetimeScope scope, CharacterMovement movement, CharacterDefinition definition) {
            GameObject player = movement.gameObject;
            if (!player.TryGetComponent(out Character character)) character = player.AddComponent<Character>();

            var so = new SerializedObject(character);
            so.FindProperty("movement").objectReferenceValue = movement;
            so.ApplyModifiedPropertiesWithoutUndo();

            var panel = Object.FindFirstObjectByType<CharacterDebugPanel>();
            if (panel == null) panel = new GameObject("CharacterDebugPanel").AddComponent<CharacterDebugPanel>();

            scope.player = character;
            scope.playerDefinition = definition;
            scope.characterDebugPanel = panel;
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
        ///   Unity -batchmode -executeMethod Systems.Editor.TestSceneSetup.ValidateTestScene
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

            var input = Object.FindFirstObjectByType<PlayerCommandInput>();
            Check(input != null, "PlayerCommandInput present in scene");

            var cameraController = Object.FindFirstObjectByType<CameraController>();
            Check(cameraController != null, "CameraController present in scene");

            var movement = Object.FindFirstObjectByType<CharacterMovement>();
            Check(movement != null, "CharacterMovement present in scene");

            var agent = Object.FindFirstObjectByType<UnityEngine.AI.NavMeshAgent>();
            Check(agent != null, "NavMeshAgent present on Player");

            var surface = Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>();
            Check(surface != null, "NavMeshSurface present on Ground");

            var quit = Object.FindFirstObjectByType<QuitOverlay>();
            Check(quit != null, "QuitOverlay present in scene");

            var obstacles = GameObject.Find(ObstaclesRoot);
            Check(obstacles != null && obstacles.transform.childCount == Obstacles.Length, "pathfinding obstacles present");
            Check(surface != null && surface.navMeshData != null && AssetDatabase.Contains(surface.navMeshData),
                  "NavMesh baked and stored as an asset");

            if (scope != null) {
                Check(scope.playerCommandInput != null, "scope.playerCommandInput wired");
                Check(scope.cameraController != null, "scope.cameraController wired");
                Check(scope.quitOverlay != null, "scope.quitOverlay wired");
                Check(scope.characterDebugPanel != null, "scope.characterDebugPanel wired");
                Check(scope.player != null, "scope.player (Character) wired");
                Check(scope.playerDefinition != null, "scope.playerDefinition wired");
                if (scope.playerDefinition != null) {
                    Check(scope.playerDefinition.Faction == Faction.Player, "player definition is in the Player faction");
                    Check(scope.playerDefinition.BaseStats != null, "player definition has base stats");
                }
                if (scope.player != null) {
                    var characterSo = new SerializedObject(scope.player);
                    Check(characterSo.FindProperty("movement").objectReferenceValue != null, "player.movement wired");
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
            }

            if (input != null) {
                Check(input.characterMovement != null, "input.characterMovement wired");
                Check(input.mainCamera != null, "input.mainCamera wired");
                Check(input.clickMarkerPrefab != null, "input.clickMarkerPrefab wired");
            }

            if (cameraController != null) {
                Check(cameraController.target != null, "cameraController.target wired");
            }

            if (errors.Length > 0) {
                Debug.LogError($"[TestSceneSetup] VALIDATION FAILED ({checks} checks):\n{errors}");
                EditorApplication.Exit(1);
            }
            else {
                Debug.Log($"[TestSceneSetup] VALIDATION PASSED ({checks} checks) in scene '{scene.name}'");
                EditorApplication.Exit(0);
            }
        }
    }
}
#endif
