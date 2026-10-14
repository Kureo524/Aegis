using System.Collections;
using Features.Characters;
using Features.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using VContainer.Unity;

namespace Game.Tests.PlayMode {
    /// <summary>Nothing leaves the map: dashes, steering and pushes stay on the NavMesh, a fall is recovered.</summary>
    public class MapBoundsTests {
        [UnityTearDown]
        public IEnumerator TearDown() {
            Object.Destroy(VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().gameObject);
            yield return null;
        }

        private static Character Player() {
            foreach (Character c in Object.FindObjectsByType<Character>(FindObjectsSortMode.None))
                if (c.IsPlayer) return c;
            return null;
        }

        private static void AssertOnMap(Transform body, string what) {
            Assert.Greater(body.position.y, -0.5f, $"{what} fell under the ground");
            Assert.IsTrue(NavMesh.SamplePosition(body.position, out NavMeshHit hit, 0.6f, NavMesh.AllAreas), $"{what} left the NavMesh at {body.position}");
        }

        [UnityTest]
        public IEnumerator Dash_And_Steering_StopAtTheEdgeOfTheMap() {
            yield return GameFlowSmokeTests.LoadGameScene();
            yield return new WaitForFixedUpdate();
            var movement = Player().GetComponent<CharacterMovement>();

            movement.Dash(new Vector3(1f, 0f, -0.3f), 60f, 4f);   // way further than the map
            for (int i = 0; i < 200; i++) yield return new WaitForFixedUpdate();
            AssertOnMap(movement.transform, "dash");

            movement.Steer(new Vector3(-1f, 0f, -1f));
            for (int i = 0; i < 400; i++) yield return new WaitForFixedUpdate();
            AssertOnMap(movement.transform, "steering");
        }

        [UnityTest]
        public IEnumerator BodyUnderTheGround_IsPutBackOnTheMap() {
            yield return GameFlowSmokeTests.LoadGameScene();
            yield return new WaitForFixedUpdate();
            Character player = Player();
            player.GetComponent<Rigidbody>().position = new Vector3(3f, -20f, 3f);
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            AssertOnMap(player.transform, "fallen body");
        }
    }
}
