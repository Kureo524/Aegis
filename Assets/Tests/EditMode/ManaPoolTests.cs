using Features.Combat;
using Features.Stats;
using NUnit.Framework;

namespace Game.Tests.EditMode {
    public class ManaPoolTests {
        private Stat _max;
        private ManaPool _mana;

        [SetUp]
        public void SetUp() {
            _max = new Stat(StatType.MaxMana, 100f, 0f, float.MaxValue);
            _mana = new ManaPool(_max);
        }

        [Test]
        public void Spend_OnlyWhenEnough() {
            Assert.IsTrue(_mana.TrySpend(60f));
            Assert.IsFalse(_mana.TrySpend(60f));
            Assert.AreEqual(40f, _mana.Current, 1e-4f);
        }

        [Test]
        public void Restore_IsCappedAtMax() {
            _mana.TrySpend(30f);
            _mana.Restore(100f);
            Assert.AreEqual(100f, _mana.Current, 1e-4f);
        }

        [Test]
        public void MaxIncrease_AddsTheSameAmountOfCurrentMana() {
            _mana.TrySpend(50f);
            _max.AddModifier(new StatModifier(30f, ModifierType.Flat));
            Assert.AreEqual(80f, _mana.Current, 1e-4f);
        }
    }
}
