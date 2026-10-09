#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Systems.Characters;
using Systems.Containers;
using Systems.Inputs;
using Systems.UI;

namespace Systems.Editor {
    public static class TestSceneSetup {
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
            input.groundLayer = LayerMask.GetMask("Default");

            // Camera
            var cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            var camera = cameraObj.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 10, -8);
            camera.transform.rotation = Quaternion.Euler(45, 0, 0);
            cameraObj.AddComponent<AudioListener>();

            var cameraController = cameraObj.AddComponent<CameraController>();
            cameraController.target = player.transform;
            cameraController.offset = new Vector3(0, 10, -8);

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

            // LifetimeScope (DI) - provides IEventBus and injects scene components
            var scopeObj = new GameObject("GameLifetimeScope");
            var scope = scopeObj.AddComponent<GameLifetimeScope>();
            scope.playerCommandInput = input;
            scope.cameraController = cameraController;

            // Save scene
            string scenePath = "Assets/Scenes/Tests/TestScene.unity";
            System.IO.Directory.CreateDirectory("Assets/Scenes/Tests");
            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log($"Test scene created at {scenePath}");
        }
    }
}
#endif
