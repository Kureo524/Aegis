using Features.Stats;
using NUnit.Framework;

namespace Game.Tests.EditMode {
    public class StatTests {
        private static readonly (float Min, float Max) NoBounds = (float.MinValue, float.MaxValue);

        private static Stat NewStat(float baseValue, StatType type = StatType.PhysicalAttack) {
            return new Stat(type, baseValue, NoBounds.Min, NoBounds.Max);
        }

        [Test]
        public void NewStat_ValueEqualsBaseValue() {
            Stat stat = NewStat(100f);
            Assert.AreEqual(100f, stat.Value, 1e-4f);
        }

        [Test]
        public void FlatModifiers_AreSummedBeforePercentages() {
            Stat stat = NewStat(100f);
            stat.AddModifier(new StatModifier(10f, ModifierType.Flat));
            stat.AddModifier(new StatModifier(15f, ModifierType.Flat));

            Assert.AreEqual(125f, stat.Value, 1e-4f);
        }

        [Test]
        public void PercentAdd_ModifiersAreSummed() {
            Stat stat = NewStat(100f);
            stat.AddModifier(new StatModifier(0.10f, ModifierType.PercentAdd));
            stat.AddModifier(new StatModifier(0.20f, ModifierType.PercentAdd));

            // +10% and +20% -> +30%, not 1.1 * 1.2
            Assert.AreEqual(130f, stat.Value, 1e-4f);
        }

        [Test]
        public void PercentMult_ModifiersAreMultiplied() {
            Stat stat = NewStat(100f);
            stat.AddModifier(new StatModifier(0.10f, ModifierType.PercentMult));
            stat.AddModifier(new StatModifier(0.20f, ModifierType.PercentMult));

            // 100 * 1.1 * 1.2
            Assert.AreEqual(132f, stat.Value, 1e-4f);
        }

        [Test]
        public void FullFormula_CombinesAllThreeTypes() {
            Stat stat = NewStat(100f);
            stat.AddModifier(new StatModifier(50f, ModifierType.Flat));
            stat.AddModifier(new StatModifier(0.20f, ModifierType.PercentAdd));
            stat.AddModifier(new StatModifier(0.10f, ModifierType.PercentAdd));
            stat.AddModifier(new StatModifier(0.50f, ModifierType.PercentMult));

            // (100 + 50) * (1 + 0.30) * (1 + 0.50) = 150 * 1.3 * 1.5
            Assert.AreEqual(292.5f, stat.Value, 1e-3f);
        }

        [Test]
        public void RemoveModifier_RecomputesValue() {
            Stat stat = NewStat(100f);
            var mod = new StatModifier(25f, ModifierType.Flat);
            stat.AddModifier(mod);
            Assert.AreEqual(125f, stat.Value, 1e-4f);

            bool removed = stat.RemoveModifier(mod);
            Assert.IsTrue(removed);
            Assert.AreEqual(100f, stat.Value, 1e-4f);
        }

        [Test]
        public void RemoveModifier_UnknownModifier_ReturnsFalse() {
            Stat stat = NewStat(100f);
            Assert.IsFalse(stat.RemoveModifier(new StatModifier(5f, ModifierType.Flat)));
            Assert.AreEqual(100f, stat.Value, 1e-4f);
        }

        [Test]
        public void RemoveAllFromSource_RemovesOnlyThatSource() {
            Stat stat = NewStat(100f);
            var itemA = new object();
            var itemB = new object();

            stat.AddModifier(new StatModifier(10f, ModifierType.Flat, itemA));
            stat.AddModifier(new StatModifier(20f, ModifierType.Flat, itemA));
            stat.AddModifier(new StatModifier(5f, ModifierType.Flat, itemB));
            Assert.AreEqual(135f, stat.Value, 1e-4f);

            int removed = stat.RemoveAllFromSource(itemA);

            Assert.AreEqual(2, removed);
            Assert.AreEqual(105f, stat.Value, 1e-4f);
            Assert.AreEqual(1, stat.Modifiers.Count);
        }

        [Test]
        public void BaseValueChange_RecomputesWithModifiers() {
            Stat stat = NewStat(100f);
            stat.AddModifier(new StatModifier(0.50f, ModifierType.PercentAdd));

            stat.BaseValue = 200f;

            Assert.AreEqual(300f, stat.Value, 1e-4f);
        }

        [Test]
        public void Changed_IsRaisedWithTheNewValue() {
            Stat stat = NewStat(100f);
            float reported = -1f;
            stat.Changed += v => reported = v;

            stat.AddModifier(new StatModifier(40f, ModifierType.Flat));

            Assert.AreEqual(140f, reported, 1e-4f);
        }

        [Test]
        public void CritChance_IsClampedToOne() {
            Stat crit = new Stat(StatType.CritChance, 0.5f, 0f, 1f);
            crit.AddModifier(new StatModifier(1f, ModifierType.Flat));

            Assert.AreEqual(1f, crit.Value, 1e-4f);
        }

        [Test]
        public void Tenacity_IsClampedToPointNine() {
            Stat tenacity = new Stat(StatType.Tenacity, 0f, 0f, 0.9f);
            tenacity.AddModifier(new StatModifier(5f, ModifierType.Flat));

            Assert.AreEqual(0.9f, tenacity.Value, 1e-4f);
        }

        [Test]
        public void MoveSpeed_IsClampedToZero() {
            Stat moveSpeed = new Stat(StatType.MoveSpeed, 5f, 0f, float.MaxValue);
            moveSpeed.AddModifier(new StatModifier(-100f, ModifierType.Flat));

            Assert.AreEqual(0f, moveSpeed.Value, 1e-4f);
        }

        [Test]
        public void Armor_AllowsNegativeValues() {
            Stat armor = new Stat(StatType.Armor, 20f, NoBounds.Min, NoBounds.Max);
            armor.AddModifier(new StatModifier(-50f, ModifierType.Flat));

            Assert.AreEqual(-30f, armor.Value, 1e-4f);
        }
    }
}
