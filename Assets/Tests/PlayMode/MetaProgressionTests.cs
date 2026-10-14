using System.Collections;
using Features.Augments;
using Features.Characters;
using Features.Economy;
using Features.Meta;
using Features.Run;
using Features.Stats;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Game.Tests.PlayMode {
    /// <summary>
    /// The real meta tree (MetaSetup) bought in the root MetaProgress, then a run: its bonuses
    /// land on the player and the run (design doc §9.7). The save is cleared afterwards (MetaSaveFixture).
    /// </summary>
    public class MetaProgressionTests {
        private static IObjectResolver Root =>
            VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().Container;

        [UnityTearDown]
        public IEnumerator TearDown() {
            Object.Destroy(VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().gameObject);
            yield return null;
            MetaSaveFixture.Clear();
        }

        private static MetaNodeDefinition Node(MetaTreeDefinition tree, string id) {
            foreach (MetaNodeDefinition n in tree.Nodes) if (n.name == $"Meta_Vesna_{id}") return n;
            Assert.Fail($"No node {id}");
            return null;
        }

        [UnityTest]
        public IEnumerator BoughtNodes_ApplyToTheNextRun() {
            var progress = Root.Resolve<MetaProgress>();
            progress.ResetAll();
            MetaTreeDefinition tree = progress.Settings.Trees[0];
            progress.Earn(5000);
            foreach (string id in new[] { "Endurance", "Purse", "Plating", "Vigor", "Legacy", "Rerolls", "Favor" })
                Assert.IsTrue(progress.TryBuy(Node(tree, id)), id);

            yield return GameFlowSmokeTests.LoadGameScene();
            yield return null;
            var scope = Object.FindFirstObjectByType<Containers.GameLifetimeScope>().Container;
            Character player = null;
            foreach (Character c in Object.FindObjectsByType<Character>(FindObjectsSortMode.None))
                if (c.IsPlayer) player = c;
            Assert.IsNotNull(player);

            Assert.AreEqual(player.Definition.BaseStats.TryGet(StatType.MaxHealth, out float hp) ? hp + 40f : -1f,
                            player.Stats.Value(StatType.MaxHealth), 0.01f, "Endurance level 1");
            Assert.AreEqual(player.Stats.Value(StatType.MaxHealth), player.Health.Current, 0.01f, "starts at full health");
            Assert.Greater(player.Stats.Get(StatType.Armor).Value, player.Stats.Get(StatType.Armor).BaseValue, "Plating");
            Assert.GreaterOrEqual(scope.Resolve<Wallet>().Gold, 60, "Purse level 1");
            var augments = scope.Resolve<AugmentController>();
            Assert.AreEqual(1, augments.Owned.Count, "Favor level 1: a starting Silver augment");
            Assert.AreEqual(RarityTier.Silver, augments.Owned[0].Rarity);
            var run = scope.Resolve<RunDirector>();
            Assert.AreEqual(scope.Resolve<RunModifiers>().ExtraLives, 1);
            Assert.AreEqual(run.Lives, scope.Resolve<RunModifiers>().ExtraLives + StageLives(run), "Legacy: one more life");
        }

        private static int StageLives(RunDirector run) {
            var field = typeof(RunDirector).GetField("_stage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return ((StageDefinition)field!.GetValue(run)).Lives;
        }
    }
}
