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

        [UnityTest]
        public IEnumerator Camera_KeepsAFixedAngle_WhileFollowing() {
            yield return SceneManager.LoadSceneAsync(GameScene, LoadSceneMode.Single);
            yield return null;
            var camera = Object.FindFirstObjectByType<CameraController>();
            var movement = FindPlayer().GetComponent<Features.Movement.CharacterMovement>();
            Quaternion initial = camera.transform.rotation;

            movement.SetDestination(new Vector3(-8f, 0f, -8f));
            for (int i = 0; i < 60; i++) {
                yield return null;
                Assert.Less(Quaternion.Angle(initial, camera.transform.rotation), 0.01f, "Camera tilted");
            }
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
            var movement = FindPlayer().GetComponent<Features.Movement.CharacterMovement>();
            Assert.IsNotNull(movement);

            movement.SetDestination(destination);
            Transform body = movement.transform;
            yield return WaitFor(() => !movement.IsMoving, timeout: 20f);

            Vector3 delta = body.position - destination;
            delta.y = 0f;
            Assert.Less(delta.magnitude, 0.5f, $"Stopped at {body.position}, expected {destination}");
        }

        [UnityTest]
        public IEnumerator Player_IsInitializedFromItsDefinition_AndLevelsUpFromBusXp() {
            yield return SceneManager.LoadSceneAsync(GameScene, LoadSceneMode.Single);
            yield return null;
            var player = FindPlayer();
            Assert.IsNotNull(player.Definition, "Player not initialized");
            Assert.IsTrue(player.IsPlayer);
            Assert.Greater(player.Stats.Value(Features.Stats.StatType.MoveSpeed), 0f, "MoveSpeed comes from the stats");

            int reportedLevel = 0;
            var bus = Root.Resolve<IEventBus>();
            using (bus.Subscribe<LevelUpEvent>(e => reportedLevel = e.Level)) {
                float attackBefore = player.Stats.Value(Features.Stats.StatType.PhysicalAttack);
                bus.Publish(new XpGrantedEvent(player.Level.XpToNext));

                Assert.AreEqual(2, player.Level.Level);
                Assert.AreEqual(2, reportedLevel);
                Assert.Greater(player.Stats.Value(Features.Stats.StatType.PhysicalAttack), attackBefore, "Stat growth applied");
            }
        }

        [UnityTest]
        public IEnumerator Player_IsSpawnedFromPrefab_SpriteFacesCamera_AndFlips() {
            yield return SceneManager.LoadSceneAsync(GameScene, LoadSceneMode.Single);
            yield return null;
            var player = FindPlayer();
            var camera = Object.FindFirstObjectByType<CameraController>();
            Assert.IsNotNull(player, "Player not spawned");
            Assert.AreEqual(player.transform, camera.target, "Camera received the player through PlayerSpawnedEvent");

            var visual = player.GetComponentInChildren<Features.Characters.CharacterVisual>();
            var movement = player.GetComponent<Features.Movement.CharacterMovement>();
            Assert.AreEqual(player.Definition.Sprite, visual.Renderer.sprite, "Sprite comes from the definition");

            movement.SetDestination(player.transform.position - camera.transform.right * 6f);   // left on screen
            yield return new WaitForSeconds(0.3f);   // time, not frames: batchmode runs uncapped
            Assert.IsTrue(visual.FacesLeft, "Flipped when moving left");
            Assert.Less(Quaternion.Angle(camera.transform.rotation, visual.transform.rotation), 0.5f, "Billboard faces the camera");

            movement.SetDestination(player.transform.position + camera.transform.right * 6f);   // right on screen
            yield return new WaitForSeconds(0.3f);   // time, not frames: batchmode runs uncapped
            Assert.IsFalse(visual.FacesLeft, "Faces right when moving right");
        }

        // Training dummies are Characters too: always look the player up explicitly.
        private static Features.Characters.Character FindPlayer() {
            foreach (var c in Object.FindObjectsByType<Features.Characters.Character>(FindObjectsSortMode.None))
                if (c.IsPlayer) return c;
            return null;
        }

        private static Features.Characters.Character FindDummy(Features.Characters.Character player) {
            foreach (var c in Object.FindObjectsByType<Features.Characters.Character>(FindObjectsSortMode.None))
                if (c != player && c.IsAlive) return c;
            return null;
        }

        [UnityTest]
        public IEnumerator Player_AttacksADummy_DamageIsDealtAndShown() {
            yield return SceneManager.LoadSceneAsync(GameScene, LoadSceneMode.Single);
            yield return null;
            var player = FindPlayer();
            var dummy = FindDummy(player);
            Assert.IsNotNull(dummy, "Training dummies spawned");

            int hits = 0;
            var bus = Root.Resolve<IEventBus>();
            using (bus.Subscribe<Features.Events.DamageDealtEvent>(e => { if (!e.TargetIsPlayer) hits++; })) {
                var attacker = player.GetComponent<Features.Combat.BasicAttacker>();
                Assert.IsTrue(attacker.Attack(dummy));
                yield return new WaitForSeconds(4f);
            }

            Assert.Less(dummy.Health.Current, dummy.Health.Max, "Dummy took damage");
            Assert.Greater(hits, 1, "Several attacks landed (and were published for the damage numbers)");
        }

        [UnityTest]
        public IEnumerator KillingADummy_GivesXp_ThenItRespawns() {
            yield return SceneManager.LoadSceneAsync(GameScene, LoadSceneMode.Single);
            yield return null;
            var player = FindPlayer();
            var dummy = FindDummy(player);
            int xpBefore = player.Level.Xp;

            dummy.TakeDamage(new Features.Combat.DamageInfo(100000f, Features.Combat.DamageType.True, false, false, player));
            Assert.IsFalse(dummy.IsAlive);
            var dummyDef = (Features.Characters.EnemyDefinition)dummy.Definition;
            Assert.AreEqual(xpBefore + dummyDef.XpReward, player.Level.Xp, "XP granted on kill");

            yield return new WaitForSeconds(2.5f);
            Assert.IsTrue(dummy.IsAlive, "Dummy respawned");
            Assert.AreEqual(dummy.Health.Max, dummy.Health.Current, 1e-3f);
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
