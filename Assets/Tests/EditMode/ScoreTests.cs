using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Features.Events;
using Features.Meta;
using Features.Run;
using Features.Score;
using NUnit.Framework;
using Systems.EventBusses;
using UnityEngine;

namespace Game.Tests.EditMode {
    public class ScoreTests {
        private EventBus _bus;
        private ScoreRules _rules;
        private ScoreService _score;
        private readonly List<int> _changes = new();

        private static RunStatusEvent Status(int lives) => new(RunState.Combat, 1, 2, 2, 0, 6, lives, 30f);

        [SetUp]
        public void SetUp() {
            _bus = new EventBus();
            _rules = ScriptableObject.CreateInstance<ScoreRules>();   // 10 / kill, 500 / boss, 200 / wave, 1000 + 250 / life
            _score = new ScoreService(_rules, _bus);
            _changes.Clear();
            _bus.Subscribe<ScoreChangedEvent>(e => _changes.Add(e.Score));
            _score.Start();
        }

        [TearDown]
        public void TearDown() {
            _score.Dispose();
            Object.DestroyImmediate(_rules);
        }

        [Test]
        public void Kills_Bosses_Waves_AddTheirPoints() {
            _bus.Publish(new WaveEnemyKilledEvent());
            _bus.Publish(new WaveEnemyKilledEvent());
            _bus.Publish(new BossDefeatedEvent());
            _bus.Publish(new WaveRewardEvent(1, false, new RewardKind[0]));
            Assert.AreEqual(2 * 10 + 500 + 200, _score.Score);
            Assert.AreEqual(new[] { 0, 10, 20, 520, 720 }, _changes.ToArray(), "a ScoreChangedEvent per change, 0 at start");
        }

        [Test]
        public void Victory_CountsTheLastWave_TheBonus_AndTheLivesLeft_Once() {
            RunScoredEvent? scored = null;
            int count = 0;
            _bus.Subscribe<RunScoredEvent>(e => {
                scored = e;
                count++;
            });
            _bus.Publish(new WaveEnemyKilledEvent());
            _bus.Publish(Status(lives: 2));
            _bus.Publish(new RunEndedEvent(true, 2));
            _bus.Publish(new RunEndedEvent(true, 2));
            _bus.Publish(new WaveEnemyKilledEvent());
            Assert.AreEqual(1, count, "scored once");
            Assert.IsTrue(scored.Value.Won);
            Assert.AreEqual(10 + 200 + 1000 + 2 * 250, scored.Value.Score);
            Assert.AreEqual(scored.Value.Score, _score.Score, "no more points once the run is over");
        }

        [Test]
        public void Defeat_KeepsThePointsEarned_WithoutBonus() {
            RunScoredEvent? scored = null;
            _bus.Subscribe<RunScoredEvent>(e => scored = e);
            _bus.Publish(new WaveEnemyKilledEvent());
            _bus.Publish(new RunEndedEvent(false, 0));
            Assert.IsFalse(scored.Value.Won);
            Assert.AreEqual(10, scored.Value.Score);
        }

        [Test]
        public void MetaProgress_KeepsTheBestScore_AcrossLoads() {
            string path = Path.Combine(Path.GetTempPath(), $"aegis-score-{System.Guid.NewGuid():N}.json");
            var settings = ScriptableObject.CreateInstance<MetaSettings>();
            try {
                var bus = new EventBus();
                var meta = new MetaProgress(settings, bus, path);
                var records = new List<BestScoreEvent>();
                bus.Subscribe<BestScoreEvent>(e => records.Add(e));
                bus.Publish(new RunScoredEvent(300, false));
                bus.Publish(new RunScoredEvent(200, false));
                bus.Publish(new RunScoredEvent(900, true));
                Assert.AreEqual(new[] { true, false, true }, records.ConvertAll(r => r.NewRecord).ToArray());
                Assert.AreEqual(new[] { 300, 300, 900 }, records.ConvertAll(r => r.Best).ToArray());
                meta.Dispose();

                var reloaded = new MetaProgress(settings, new EventBus(), path);
                Assert.AreEqual(900, reloaded.BestScore, "saved between runs");
                reloaded.Dispose();
            }
            finally {
                if (File.Exists(path)) File.Delete(path);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void StageSelection_PicksOnlyListedStages_AndDefaultsToTheFirst() {
            var demo = ScriptableObject.CreateInstance<StageDefinition>();
            var full = ScriptableObject.CreateInstance<StageDefinition>();
            var other = ScriptableObject.CreateInstance<StageDefinition>();
            var catalog = ScriptableObject.CreateInstance<StageCatalog>();
            typeof(StageCatalog).GetField("stages", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(catalog, new[] { demo, full });
            try {
                var selection = new StageSelection(catalog, _bus);
                selection.Start();
                Assert.AreSame(demo, selection.Default, "the short run first");
                Assert.IsNull(selection.Current, "nothing picked: the game scene plays its own stage");
                _bus.Publish(new StagePickRequestedEvent(other));
                Assert.IsNull(selection.Current, "not in the catalog");
                _bus.Publish(new StagePickRequestedEvent(full));
                Assert.AreSame(full, selection.Current);
                selection.Dispose();
            }
            finally {
                foreach (Object o in new Object[] { demo, full, other, catalog }) Object.DestroyImmediate(o);
            }
        }
    }
}
