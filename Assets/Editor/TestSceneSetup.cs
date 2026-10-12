#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Containers;
using Features.CameraControl;
using Features.Controls;
using Features.Movement;
using Features.UI;
using VContainer.Unity;

namespace EditorTools {
    /// <summary>
    /// Regenerates the test scene from scratch (overwrites it) and validates its wiring.
    /// Shared services (event bus, input, game flow, settings) come from the RootLifetimeScope
    /// prefab created by <see cref="ProjectSetup"/>: run that first.
    /// </summary>
    public static class TestSceneSetup {
        private const string ScenePath = ProjectSetup.TestScenePath;

        // NOTE: this scene is saved as a BINARY .unity file even though the project is in
        // Force Text mode. Cause: the baked NavMesh. NavMeshData is a binary-only serializable
        // type, and any scene holding one cannot be written as YAML (verified empirically:
        // a scene with a NavMeshSurface is TEXT until BuildNavMesh() is called, then BINARY).
        // This is a Unity constraint, not a project misconfiguration. The scene is a generated
        // artifact rebuilt by this script, so losing textual diffs on it is acceptable.
        // If textual diffs ever become necessary, bake the NavMesh at runtime instead of in-editor.

        [MenuItem("Tools/Create Test Scene")]
        public static void CreateTestScene() {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ground
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10, 1, 10);
            ground.GetComponent<Renderer>().material.color = new Color(0.2f, 0.3f, 0.2f);

            // NavMeshSurface
            var navSurface = ground.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            navSurface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
            navSurface.BuildNavMesh();

            // Player
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0, 1, 0);
            player.GetComponent<Renderer>().material.color = Color.blue;

            var rb = player.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            var agent = player.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.speed = 5f;
            agent.acceleration = 1000f;
            agent.angularSpeed = 360f;
            agent.autoBraking = false;

            var movement = player.AddComponent<CharacterMovement>();
            movement.moveSpeed = 5f;

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

            // Save scene
            System.IO.Directory.CreateDirectory("Assets/Scenes/Tests");
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"[TestSceneSetup] Test scene created at {ScenePath}");
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

            if (scope != null) {
                Check(scope.playerCommandInput != null, "scope.playerCommandInput wired");
                Check(scope.cameraController != null, "scope.cameraController wired");
                Check(scope.quitOverlay != null, "scope.quitOverlay wired");
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
