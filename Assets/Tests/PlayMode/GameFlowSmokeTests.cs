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
        public IEnumerator RangedAttack_FiresPooledProjectiles() {
            yield return SceneManager.LoadSceneAsync(GameScene, LoadSceneMode.Single);
            yield return null;
            var player = FindPlayer();
            var dummy = FindDummy(player);

            int hits = 0;
            var attacker = player.GetComponent<Features.Combat.BasicAttacker>();
            attacker.Hit += (_, _) => hits++;
            attacker.Attack(dummy);
            yield return new WaitForSeconds(5f);

            int instances = Object.FindObjectsByType<Features.Combat.Projectile>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Assert.Greater(hits, 2, "Projectiles reached the dummy");
            Assert.Less(dummy.Health.Current, dummy.Health.Max);
            Assert.Greater(instances, 0, "Hits come from projectiles");
            Assert.Less(instances, hits, "Projectiles are pooled, not one instance per shot");
        }

        [UnityTest]
        public IEnumerator AttackMove_AttacksEnemiesInRange_ThenWalksToThePoint() {
            yield return SceneManager.LoadSceneAsync(GameScene, LoadSceneMode.Single);
            yield return null;
            var player = FindPlayer();
            var attacker = player.GetComponent<Features.Combat.BasicAttacker>();

            // Dummies in range on the way: one of them gets attacked
            Assert.IsTrue(attacker.AttackMove(new Vector3(5f, 0f, -3f)));
            yield return new WaitForSeconds(2f);
            Assert.IsNotNull(attacker.Target, "Acquired an enemy in range");
            Assert.Less(attacker.Target.Health.Current, attacker.Target.Health.Max, "and attacked it");

            // No enemy left: walks to the point and the order completes
            foreach (var c in Object.FindObjectsByType<Features.Characters.Character>(FindObjectsSortMode.None))
                if (!c.IsPlayer) c.TakeDamage(new Features.Combat.DamageInfo(1e6f, Features.Combat.DamageType.True, false, false, null));
            yield return WaitFor(() => !attacker.IsAttackMoving, timeout: 1.9f);   // before the dummies respawn

            Vector3 delta = player.transform.position - new Vector3(5f, 0f, -3f);
            delta.y = 0f;
            Assert.Less(delta.magnitude, 0.5f, "Arrived at the attack-move point");
        }

        [UnityTest]
        public IEnumerator HoldingAttackMove_ShowsTheAttackRange_ReleasingHidesIt() {
            yield return SceneManager.LoadSceneAsync(GameScene, LoadSceneMode.Single);
            yield return null;
            var player = FindPlayer();
            var indicator = player.GetComponentInChildren<Features.Controls.RangeIndicator>(true);
            var bus = Root.Resolve<IEventBus>();
            Assert.IsFalse(indicator.IsShown, "Hidden by default");

            bus.Publish(new Systems.Inputs.Events.AttackMoveEvent(true));
            yield return null;
            Assert.IsTrue(indicator.IsShown, "Shown while Q is held");
            Assert.AreEqual(player.Stats.Value(Features.Stats.StatType.AttackRange), indicator.Radius, 1e-3f);
            var line = indicator.GetComponent<LineRenderer>();
            Assert.Greater(line.positionCount, 8);
            Assert.AreEqual(indicator.Radius, new Vector2(line.GetPosition(0).x, line.GetPosition(0).z).magnitude, 1e-3f);

            bus.Publish(new Systems.Inputs.Events.AttackMoveEvent(false));
            yield return null;
            Assert.IsFalse(indicator.IsShown, "Hidden when Q is released");
        }

        [UnityTest]
        public IEnumerator Elements_UnlockWithSkillPoints_StackAndResolveToASpell() {
            yield return SceneManager.LoadSceneAsync(GameScene, LoadSceneMode.Single);
            yield return null;
            var player = FindPlayer();
            var caster = player.GetComponent<Features.Spells.ElementCaster>();
            var bus = Root.Resolve<IEventBus>();

            string feedback = null;
            Features.Events.ElementSelectionChangedEvent selection = default;
            using (bus.Subscribe<Features.Events.GameplayFeedbackEvent>(e => feedback = e.Message))
            using (bus.Subscribe<Features.Events.ElementSelectionChangedEvent>(e => selection = e)) {
                // Locked element: no stack, feedback
                bus.Publish(new Systems.Inputs.Events.ElementKeyEvent(0));
                Assert.AreEqual(0, caster.Selection.Total);
                Assert.IsNotNull(feedback, "Locked element gives feedback");

                // Ctrl + A: spends the level 1 skill point, Fire unlocked
                bus.Publish(new Systems.Inputs.Events.ElementKeyEvent(0, levelUp: true));
                Assert.IsTrue(caster.Levels.IsUnlocked(0));
                Assert.AreEqual(0, player.Level.SkillPoints);

                // A, A → Fire x2
                bus.Publish(new Systems.Inputs.Events.ElementKeyEvent(0));
                bus.Publish(new Systems.Inputs.Events.ElementKeyEvent(0));
                Assert.AreEqual(2, selection.Stacks0);
                Assert.IsNotNull(selection.Spell, "Fire x2 resolves to a spell");
                Assert.AreEqual("Grande boule de feu", selection.Spell.DisplayName);
                Assert.AreEqual(10f * 2f * 1.5f, selection.ManaCost, 1e-3f, "2 stacks of Fire (10) × 1.5");

                // Second element: refused at level 2, allowed at level 3
                bus.Publish(new Features.Events.XpGrantedEvent(player.Level.XpToNext));   // level 2
                bus.Publish(new Features.Events.ElementLevelUpRequestedEvent(1));
                Assert.IsFalse(caster.Levels.IsUnlocked(1), "2nd element waits for level 3");
                bus.Publish(new Features.Events.XpGrantedEvent(player.Level.XpToNext));   // level 3
                bus.Publish(new Features.Events.ElementLevelUpRequestedEvent(1));
                Assert.IsTrue(caster.Levels.IsUnlocked(1));

                // T clears the selection
                bus.Publish(new Systems.Inputs.Events.ResetElementsEvent());
                Assert.AreEqual(0, selection.Total);
                Assert.IsNull(selection.Spell);
            }
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
