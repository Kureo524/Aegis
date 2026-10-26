using System.Collections;
using Features.Events;
using Features.Meta;
using Features.Run;
using Features.Score;
using Features.StateMachines;
using Features.UI.Panels;
using NUnit.Framework;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Game.Tests.PlayMode {
    /// <summary>
    /// The short run of the hand-in (design doc §9.1, §9.9), as the menu starts it: the stage
    /// picked, two waves, an augment choice, the score, the victory and the meta save.
    /// </summary>
    public class ShortRunTests {
        private static IObjectResolver Root => VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().Container;
        private static IObjectResolver Game => Object.FindFirstObjectByType<Containers.GameLifetimeScope>().Container;
        private static IEventBus Bus => Root.Resolve<IEventBus>();

        [UnityTearDown]
        public IEnumerator TearDown() {
            Time.timeScale = 1f;
            Object.Destroy(VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().gameObject);
            yield return null;
        }

        private static IEnumerator WaitFor(System.Func<bool> condition, float timeout = 15f) {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(condition(), "Timed out");
        }

        [Test]
        public void TheShortRun_IsTheDefault_AndLastsAboutThreeMinutes() {
            StageDefinition demo = Root.Resolve<StageSelection>().Default;
            Assert.IsNotNull(demo, "StageCatalog wired in the root scope");
            Assert.AreEqual(2, demo.TotalWaves);
            float seconds = demo.FirstWaveCountdown + demo.Intermission;
            foreach (WaveDefinition w in demo.Waves) seconds += w.TimeLimit;
            Assert.That(seconds, Is.InRange(90f, 180f), "2-3 minutes of play, augment choice aside");
            Assert.AreEqual(RewardKind.Augment, demo.RewardSchedule.AfterWave(0)[0], "an upgrade choice after wave 1");
        }

        [UnityTest]
        public IEnumerator TheShortRun_GoesThroughEverySystem_AndSavesTheMeta() {
            MetaSaveFixture.Clear();   // no best score of an earlier test
            var meta = Root.Resolve<MetaProgress>();
            var selection = Root.Resolve<StageSelection>();
            int currency = meta.Currency;
            yield return null;   // the root's entry points start (StageSelection subscribes)
            Bus.Publish(new StagePickRequestedEvent(selection.Default));   // what "Play" sends
            yield return SceneManager.LoadSceneAsync("TestScene", LoadSceneMode.Single);
            yield return null;

            var stage = Game.Resolve<StageDefinition>();
            Assert.AreSame(selection.Default, stage, "the picked stage replaces the scene's own one");
            var run = Game.Resolve<RunDirector>();
            var score = Game.Resolve<ScoreService>();
            var machine = Root.Resolve<GameFlowStateMachine>();
            int xpGains = 0;
            using System.IDisposable xpSubscription = Bus.Subscribe<XpChangedEvent>(_ => xpGains++);

            for (int wave = 0; wave < stage.TotalWaves; wave++) {
                Bus.Publish(new SkipWaveRequestedEvent());   // countdown
                yield return null;
                Assert.AreEqual(RunState.Combat, run.State);
                Bus.Publish(new EnemyKilledEvent(50, 0, Vector3.zero));   // XP pickup
                Bus.Publish(new WaveEnemyKilledEvent());
                Bus.Publish(new SkipWaveRequestedEvent());   // quota + timer
                yield return null;
                if (wave < stage.TotalWaves - 1) {
                    Assert.AreEqual(RunState.Reward, run.State);
                    RunTests.FinishRewardsFor(Game);   // the augment choice
                }
            }
            yield return WaitFor(() => machine.CurrentState == GameState.Win);
            yield return null;

            Assert.Greater(xpGains, 0, "XP picked up");
            Assert.Greater(score.Score, 0);
            Assert.AreEqual(score.Score, meta.BestScore, "best score saved");
            Assert.Greater(meta.Currency, currency, "meta currency earned and saved");
            Assert.IsTrue(System.IO.File.Exists(MetaSaveFixture.SavePath), "written to disk");
            var panel = Object.FindFirstObjectByType<RunEndPanel>();
            StringAssert.StartsWith($"Score : {score.Score}", panel.ScoreText);
            StringAssert.Contains("Nouveau record", panel.ScoreText);
            MetaSaveFixture.Clear();
        }
    }
}
