using System.IO;
using System.Reflection;
using Features.Economy;
using Features.Events;
using Features.Meta;
using Features.Stats;
using NUnit.Framework;
using Systems.EventBusses;
using UnityEngine;

namespace Game.Tests.EditMode {
    public class MetaProgressTests {
        private string _path;
        private MetaSettings _settings;
        private MetaTreeDefinition _tree;
        private MetaNodeDefinition _root, _child, _gold, _lives, _augment;

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);

        private static MetaNodeDefinition Node(string id, int[] costs, params MetaEffect[] effects) {
            var node = ScriptableObject.CreateInstance<MetaNodeDefinition>();
            node.name = id;
            Set(node, "displayName", id);
            Set(node, "costs", costs);
            Set(node, "effects", effects);
            return node;
        }

        [SetUp]
        public void SetUp() {
            _path = Path.Combine(Path.GetTempPath(), $"aegis-meta-{System.Guid.NewGuid():N}.json");
            _root = Node("Root", new[] { 10, 20 }, new MetaEffect { Kind = MetaEffectKind.Stat, Stat = StatType.MaxHealth, Value = 40 });
            _child = Node("Child", new[] { 30 }, new MetaEffect { Kind = MetaEffectKind.Stat, Stat = StatType.Armor, Value = 4 });
            Set(_child, "prerequisites", new[] { _root });
            _gold = Node("Gold", new[] { 5, 5, 5 }, new MetaEffect { Kind = MetaEffectKind.StartingGold, Value = 60 });
            _lives = Node("Lives", new[] { 5 }, new MetaEffect { Kind = MetaEffectKind.ExtraLives, Value = 1 },
                          new MetaEffect { Kind = MetaEffectKind.AugmentRerolls, Value = 2 });
            _augment = Node("Augment", new[] { 1, 1, 1 }, new MetaEffect { Kind = MetaEffectKind.StartingAugment, Value = 1 });
            _tree = ScriptableObject.CreateInstance<MetaTreeDefinition>();
            Set(_tree, "nodes", new[] { _root, _child, _gold, _lives, _augment });
            _settings = ScriptableObject.CreateInstance<MetaSettings>();
            Set(_settings, "trees", new[] { _tree });
        }

        [TearDown]
        public void TearDown() {
            if (File.Exists(_path)) File.Delete(_path);
            foreach (Object o in new Object[] { _root, _child, _gold, _lives, _augment, _tree, _settings }) Object.DestroyImmediate(o);
        }

        [Test]
        public void Reward_OnlyAFullClearPays() {
            var bus = new EventBus();
            var progress = new MetaProgress(_settings, bus, _path);
            int rewarded = -1;
            bus.Subscribe<MetaRewardEvent>(e => rewarded = e.Amount);

            bus.Publish(new RunEndedEvent(false, 4));
            Assert.AreEqual(0, rewarded, "a defeat pays nothing by default (design doc: gained on stage clear)");
            Assert.AreEqual(0, progress.Currency);

            bus.Publish(new RunEndedEvent(true, 10));
            Assert.AreEqual(6 * 10 + 40, rewarded);
            Assert.AreEqual(100, progress.Currency);
        }

        [Test]
        public void Buy_NeedsCurrencyAndPrerequisites_AndStopsAtMaxLevel() {
            var bus = new EventBus();
            int changed = 0;
            bus.Subscribe<MetaChangedEvent>(_ => changed++);
            var progress = new MetaProgress(_settings, bus, _path);

            Assert.IsFalse(progress.TryBuy(_root), "no currency");
            progress.Earn(100);
            Assert.IsFalse(progress.TryBuy(_child), "prerequisite not owned");
            StringAssert.StartsWith("Nécessite", progress.RefusalReason(_child));

            Assert.IsTrue(progress.TryBuy(_root));
            Assert.IsTrue(progress.TryBuy(_root), "level 2 costs 20");
            Assert.AreEqual(70, progress.Currency);
            Assert.AreEqual("Niveau maximum", progress.RefusalReason(_root));
            Assert.IsFalse(progress.TryBuy(_root));
            Assert.IsTrue(progress.TryBuy(_child));
            Assert.AreEqual(40, progress.Currency);
            Assert.AreEqual(4, changed, "earn + 3 purchases");
        }

        [Test]
        public void Progress_IsSavedAndReloaded() {
            var progress = new MetaProgress(_settings, new EventBus(), _path);
            progress.Earn(50);
            progress.TryBuy(_root);

            var reloaded = new MetaProgress(_settings, new EventBus(), _path);
            Assert.AreEqual(40, reloaded.Currency);
            Assert.AreEqual(1, reloaded.Level(_root));
            Assert.AreEqual(0, reloaded.Level(_child));

            reloaded.ResetAll();
            Assert.AreEqual(0, new MetaProgress(_settings, new EventBus(), _path).Currency);
        }

        [Test]
        public void CorruptSave_StartsFresh() {
            File.WriteAllText(_path, "{ nope");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("MetaProgress"));
            Assert.AreEqual(0, new MetaProgress(_settings, new EventBus(), _path).Currency);
        }

        [Test]
        public void RunModifiers_SumTheLevelsBought() {
            var progress = new MetaProgress(_settings, new EventBus(), _path);
            progress.Earn(1000);
            progress.TryBuy(_root);
            progress.TryBuy(_root);
            progress.TryBuy(_gold);
            progress.TryBuy(_gold);
            progress.TryBuy(_lives);
            progress.TryBuy(_augment);
            progress.TryBuy(_augment);

            var run = new RunModifiers(progress, null);
            Assert.AreEqual(120, run.StartingGold);
            Assert.AreEqual(1, run.ExtraLives);
            Assert.AreEqual(2, run.AugmentRerolls);
            Assert.AreEqual(RarityTier.Gold, run.StartingAugment, "level 2: a Gold augment");

            var stats = new StatController();
            stats.Init(null);
            run.ApplyStats(stats);
            Assert.AreEqual(80f, stats.Value(StatType.MaxHealth), 0.001f, "2 levels × 40");
            Assert.AreEqual(0f, stats.Value(StatType.Armor), 0.001f, "child not bought");
        }

        [Test]
        public void RunModifiers_WithoutProgress_GiveNothing() {
            var run = new RunModifiers(new MetaProgress(_settings, new EventBus(), _path), null);
            Assert.AreEqual(0, run.StartingGold);
            Assert.AreEqual(0, run.ExtraLives);
            Assert.IsNull(run.StartingAugment);
        }
    }
}
