using System.Collections;
using Features.CameraControl;
using Features.Events;
using Features.StateMachines;
using NUnit.Framework;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Game.Tests.PlayMode {
    /// <summary>
    /// Boots the real scenes with the real RootLifetimeScope. Any injection error or exception
    /// logged during boot fails the test (Unity Test Framework treats logged errors as failures).
    /// </summary>
    public class GameFlowSmokeTests {
        private const string MenuScene = "Menu";
        private const string GameScene = "TestScene";

        private static IObjectResolver Root =>
            VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().Container;

        // The root scope survives scene loads: destroy it so each test starts a fresh session.
        [UnityTearDown]
        public IEnumerator TearDown() {
            Object.Destroy(VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().gameObject);
            yield return null;
        }

        private static IEnumerator WaitFor(System.Func<bool> condition, float timeout = 10f) {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(condition(), "Timed out");
        }

        [UnityTest]
        public IEnumerator GameScene_BootsDirectly_IntoGameState() {
            yield return SceneManager.LoadSceneAsync(GameScene, LoadSceneMode.Single);
            var machine = Root.Resolve<GameFlowStateMachine>();

            yield return WaitFor(() => machine.HasState && machine.CurrentState == GameState.Game);

            var camera = Object.FindFirstObjectByType<CameraController>();
            Assert.IsNotNull(camera);
            yield return null;
            Assert.IsTrue(camera.IsLocked, "Camera starts locked (CameraSettings.StartLocked)");
            Root.Resolve<IEventBus>().Publish(new Systems.Inputs.Events.CameraLockEvent());
            Assert.IsFalse(camera.IsLocked, "CameraLockEvent toggles the lock");
        }

        // Destinations hidden behind the test course obstacles (see TestSceneSetup.Obstacles):
        // reaching them proves the path goes around instead of into the walls.
        [UnityTest]
        public IEnumerator Character_PathsAroundObstacles([Values(0, 1, 2)] int caseIndex) {
            Vector3[] destinations = {
                new(0f, 0f, 9f),     // behind Wall_Front
                new(-18f, 0f, 0f),   // behind the U trap
                new(19.5f, 0f, 14f), // inside the narrow corridor
            };
            Vector3 destination = destinations[caseIndex];

            yield return SceneManager.LoadSceneAsync(GameScene, LoadSceneMode.Single);
            yield return new WaitForFixedUpdate();
            var movement = Object.FindFirstObjectByType<Features.Movement.CharacterMovement>();
            Assert.IsNotNull(movement);

            movement.SetDestination(destination);
            Transform body = movement.transform;
            yield return WaitFor(() => !movement.IsMoving, timeout: 20f);

            Vector3 delta = body.position - destination;
            delta.y = 0f;
            Assert.Less(delta.magnitude, 0.5f, $"Stopped at {body.position}, expected {destination}");
        }

        [UnityTest]
        public IEnumerator Menu_Play_LoadsGameScene() {
            yield return SceneManager.LoadSceneAsync(MenuScene, LoadSceneMode.Single);
            var machine = Root.Resolve<GameFlowStateMachine>();
            yield return WaitFor(() => machine.HasState && machine.CurrentState == GameState.Menu);

            Root.Resolve<IEventBus>().Publish(new PlayRequestedEvent());

            yield return WaitFor(() => machine.CurrentState == GameState.Game);
            Assert.AreEqual(GameScene, SceneManager.GetActiveScene().name);
        }
    }
}
