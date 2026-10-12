using Features.Stats;
using NUnit.Framework;

namespace Game.Tests.EditMode {
    public class TimedModifiersTests {
        [Test]
        public void Modifier_AppliesThenExpires() {
            var speed = new Stat(StatType.MoveSpeed, 5f);
            var timed = new TimedModifiers();

            timed.Apply(speed, 0.5f, ModifierType.PercentAdd, source: "buff", duration: 2f);
            Assert.AreEqual(7.5f, speed.Value, 1e-4f);

            timed.Tick(1.5f);
            Assert.AreEqual(7.5f, speed.Value, 1e-4f);
            timed.Tick(0.6f);
            Assert.AreEqual(5f, speed.Value, 1e-4f);
            Assert.AreEqual(0, timed.Count);
        }

        [Test]
        public void SameSourceAgain_RefreshesInsteadOfStacking() {
            var speed = new Stat(StatType.MoveSpeed, 5f);
            var timed = new TimedModifiers();
            object spell = new object();

            timed.Apply(speed, 0.5f, ModifierType.PercentAdd, spell, 2f);
            timed.Tick(1.5f);
            timed.Apply(speed, 0.5f, ModifierType.PercentAdd, spell, 2f);

            Assert.AreEqual(7.5f, speed.Value, 1e-4f, "not +100%");
            timed.Tick(1.5f);
            Assert.AreEqual(7.5f, speed.Value, 1e-4f, "timer restarted");
        }

        [Test]
        public void DifferentSources_Stack() {
            var speed = new Stat(StatType.MoveSpeed, 10f);
            var timed = new TimedModifiers();
            timed.Apply(speed, 0.1f, ModifierType.PercentAdd, "a", 2f);
            timed.Apply(speed, 0.2f, ModifierType.PercentAdd, "b", 2f);
            Assert.AreEqual(13f, speed.Value, 1e-4f);
        }
    }
}
