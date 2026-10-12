using Features.Combat;
using Features.Stats;
using NUnit.Framework;

namespace Game.Tests.EditMode {
    public class HealthTests {
        private Stat _maxHealth;
        private Health _health;

        [SetUp]
        public void SetUp() {
            _maxHealth = new Stat(StatType.MaxHealth, 100f, 0f, float.MaxValue);
            _health = new Health(_maxHealth);
        }

        [Test]
        public void StartsFull() {
            Assert.AreEqual(100f, _health.Current, 1e-4f);
            Assert.IsFalse(_health.IsDead);
        }

        [Test]
        public void Damage_ReturnsWhatWasActuallyTaken() {
            Assert.AreEqual(30f, _health.TakeDamage(30f), 1e-4f);
            Assert.AreEqual(70f, _health.TakeDamage(500f), 1e-4f, "overkill is not counted");
            Assert.AreEqual(0f, _health.Current, 1e-4f);
        }

        [Test]
        public void Dies_Once() {
            int deaths = 0;
            _health.Died += () => deaths++;

            _health.TakeDamage(100f);
            _health.TakeDamage(10f);

            Assert.IsTrue(_health.IsDead);
            Assert.AreEqual(1, deaths);
        }

        [Test]
        public void Heal_IsCappedAtMax_AndDoesNothingWhenDead() {
            _health.TakeDamage(50f);
            Assert.AreEqual(50f, _health.Heal(80f), 1e-4f);
            Assert.AreEqual(100f, _health.Current, 1e-4f);

            _health.TakeDamage(100f);
            Assert.AreEqual(0f, _health.Heal(50f), 1e-4f);
        }

        [Test]
        public void MaxIncrease_AddsTheSameAmountOfCurrentHp() {
            _health.TakeDamage(40f);   // 60 / 100
            _maxHealth.AddModifier(new StatModifier(50f, ModifierType.Flat));

            Assert.AreEqual(110f, _health.Current, 1e-4f);
            Assert.AreEqual(150f, _health.Max, 1e-4f);
        }

        [Test]
        public void MaxDecrease_ClampsCurrent() {
            var mod = new StatModifier(-60f, ModifierType.Flat);
            _maxHealth.AddModifier(mod);

            Assert.AreEqual(40f, _health.Current, 1e-4f);
        }

        [Test]
        public void Revive_RestoresFullHp() {
            _health.TakeDamage(100f);
            _health.Revive();

            Assert.IsFalse(_health.IsDead);
            Assert.AreEqual(100f, _health.Current, 1e-4f);
        }
    }
}
