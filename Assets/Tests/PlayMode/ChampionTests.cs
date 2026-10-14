using System.Collections;
using System.Linq;
using Features.Characters;
using Features.Combat;
using Features.Enemies;
using Features.Events;
using Features.Movement;
using Features.Spells;
using Features.StateMachines;
using Features.UI.Panels;
using NUnit.Framework;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace Game.Tests.PlayMode {
    /// <summary>The champion roster, the champion select screen and Aldric's kit (design doc §7.10).</summary>
    public class ChampionTests {
        private static IObjectResolver Root => VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().Container;
        private static IObjectResolver Game => Object.FindFirstObjectByType<Containers.GameLifetimeScope>().Container;
        private static IEventBus Bus => Root.Resolve<IEventBus>();

        [UnityTearDown]
        public IEnumerator TearDown() {
            Object.Destroy(VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().gameObject);
            yield return null;
        }

        private static CharacterDefinition Champion(string name) =>
            Root.Resolve<ChampionSelection>().Champions.First(c => c.DisplayName == name);

        private static IEnumerator WaitFor(System.Func<bool> condition, float timeout = 10f) {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(condition(), "Timed out");
        }

        [UnityTest]
        public IEnumerator Menu_Play_ChampionSelect_PickAldric_TheRunStartsWithHim() {
            yield return SceneManager.LoadSceneAsync("Menu", LoadSceneMode.Single);
            var machine = Root.Resolve<GameFlowStateMachine>();
            yield return WaitFor(() => machine.HasState && machine.CurrentState == GameState.Menu);
            var selection = Root.Resolve<ChampionSelection>();
            Assert.AreEqual("Vesna", selection.Current.DisplayName, "the roster's first champion by default");
            CollectionAssert.AreEqual(new[] { "Vesna", "Aldric" }, selection.Champions.Select(c => c.DisplayName));

            Bus.Publish(new ChampionSelectRequestedEvent());   // main menu "Play"
            yield return null;
            var panel = Object.FindFirstObjectByType<ChampionSelectPanel>();
            Assert.IsTrue(panel.IsOpen, "Play opens the champion select screen");
            Assert.AreEqual(GameState.Menu, machine.CurrentState, "not in game yet");
            VisualElement root = panel.GetComponent<UIDocument>().rootVisualElement;
            Assert.AreEqual(2, root.Query(className: "ae-champ").ToList().Count, "a card per champion");
            Assert.AreEqual(10, root.Query(className: "ae-champ__spell").ToList().Count, "passive + Q W E R each");

            CharacterDefinition aldric = Champion("Aldric");
            panel.Pick(aldric);
            yield return null;
            Assert.AreSame(aldric, selection.Current);
            VisualElement selected = root.Query(className: "ae-champ--selected").ToList().Single();
            Assert.AreEqual("Aldric", selected.Q<Label>(className: "ae-champ__name").text);

            // His progression tree first
            Bus.Publish(new MetaTreeRequestedEvent());
            yield return null;
            Assert.AreSame(aldric, Object.FindFirstObjectByType<MetaPanel>().ShownChampion);
            Object.FindFirstObjectByType<MetaPanel>().Show(false);

            Bus.Publish(new PlayRequestedEvent());   // "Jouer"
            yield return WaitFor(() => machine.CurrentState == GameState.Game);
            Character player = Game.Resolve<EnemyDirector>().Player;
            Assert.AreEqual("Aldric", player.Definition.DisplayName);
            Assert.IsFalse(player.Definition.BasicAttack.IsRanged, "a melee champion");
            Assert.AreEqual("Couronne d'aurore", player.Definition.Abilities[3].DisplayName);
        }

        // Aldric in the test scene, next to the nearest training dummy
        private static IEnumerator AldricByADummy(System.Action<Character, Character> found) {
            // The session's root scope first (booted by the scene), then the pick, then the run
            yield return GameFlowSmokeTests.LoadGameScene();
            Bus.Publish(new ChampionPickRequestedEvent(Champion("Aldric")));
            yield return GameFlowSmokeTests.LoadGameScene();
            yield return null;
            Character player = Game.Resolve<EnemyDirector>().Player;
            Assert.AreEqual("Aldric", player.Definition.DisplayName);
            Character dummy = Object.FindObjectsByType<Character>(FindObjectsSortMode.None)
                                    .Where(c => c != player && c.IsAlive)
                                    .OrderBy(c => (c.transform.position - player.transform.position).sqrMagnitude).First();
            player.GetComponent<CharacterMovement>().Teleport(dummy.transform.position + new Vector3(-1.5f, 0f, 0f));
            yield return null;
            found(player, dummy);
        }

        private static void Cast(Character caster, int slot, Vector3 at, int rank = 1) {
            Vector3 origin = caster.transform.position;
            Vector3 direction = at - origin;
            direction.y = 0f;
            Game.Resolve<SpellExecutor>().Execute(caster, caster.Definition.Abilities[slot],
                new CastContext { Origin = origin, Direction = direction.normalized, TargetPoint = at }, rank);
        }

        [UnityTest]
        public IEnumerator Aldric_Kit_SweepShreds_RampartShieldsAndSlows_LeapRoots_CrownStuns() {
            Character player = null, dummy = null;
            yield return AldricByADummy((p, d) => { player = p; dummy = d; });

            float hp = dummy.Health.Current;
            Cast(player, 0, dummy.transform.position);                                  // Q
            Assert.Less(dummy.Health.Current, hp, "the sweep hits in front of him");
            Assert.Greater(dummy.Status.Remaining(StatusKind.Shred), 0f, "and shreds");

            hp = dummy.Health.Current;
            Cast(player, 1, player.transform.position);                                 // W
            Assert.Less(dummy.Health.Current, hp, "the frost burst hits around him");
            Assert.Greater(dummy.Status.Remaining(StatusKind.Slow), 0f, "and slows");
            Assert.Greater(player.Health.Shield, 0f, "the rampart shields him");

            // E: leaps away from the dummy, then back onto it
            player.GetComponent<CharacterMovement>().Teleport(dummy.transform.position + new Vector3(-5f, 0f, 0f));
            yield return null;
            hp = dummy.Health.Current;
            Cast(player, 2, dummy.transform.position + new Vector3(-0.8f, 0f, 0f));
            Assert.Less((player.transform.position - dummy.transform.position).magnitude, 2f, "he landed by the dummy");
            Assert.Less(dummy.Health.Current, hp, "the landing hits");
            Assert.IsTrue(dummy.Status.IsRooted, "and roots");

            hp = dummy.Health.Current;
            Cast(player, 3, player.transform.position);                                 // R
            Assert.Less(dummy.Health.Current, hp, "the aurora bursts around him");
            Assert.IsTrue(dummy.Status.IsStunned, "and stuns");
        }
    }
}
