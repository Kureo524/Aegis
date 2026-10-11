using Features.Stats;
using NUnit.Framework;

namespace Game.Tests.EditMode {
    public class StatControllerTests {
        [Test]
        public void Init_CreatesEveryStatType() {
            var controller = new StatController();
            controller.Init(null);

            foreach (StatType type in System.Enum.GetValues(typeof(StatType))) {
                Assert.IsNotNull(controller.Get(type), $"Missing stat: {type}");
            }
        }

        [Test]
        public void Init_AppliesIntrinsicClampRules() {
            var controller = new StatController();
            controller.Init(null);

            Stat crit = controller.Get(StatType.CritChance);
            crit.AddModifier(new StatModifier(10f, ModifierType.Flat));
            Assert.AreEqual(1f, crit.Value, 1e-4f);

            Stat tenacity = controller.Get(StatType.Tenacity);
            tenacity.AddModifier(new StatModifier(10f, ModifierType.Flat));
            Assert.AreEqual(0.9f, tenacity.Value, 1e-4f);
        }

        [Test]
        public void Init_WithNullBlock_LeavesStatsAtZero() {
            var controller = new StatController();
            controller.Init(null);

            Assert.AreEqual(0f, controller.Value(StatType.PhysicalAttack), 1e-4f);
            Assert.AreEqual(0f, controller.Value(StatType.MaxHealth), 1e-4f);
        }

        [Test]
        public void Value_ReturnsFinalValueThroughModifiers() {
            var controller = new StatController();
            controller.Init(null);

            Stat attack = controller.Get(StatType.PhysicalAttack);
            attack.BaseValue = 80f;
            attack.AddModifier(new StatModifier(0.25f, ModifierType.PercentAdd));

            Assert.AreEqual(100f, controller.Value(StatType.PhysicalAttack), 1e-4f);
        }

        [Test]
        public void Init_CanBeCalledAgain_ToReset() {
            var controller = new StatController();
            controller.Init(null);
            controller.Get(StatType.PhysicalAttack).AddModifier(new StatModifier(50f, ModifierType.Flat));
            Assert.AreEqual(50f, controller.Value(StatType.PhysicalAttack), 1e-4f);

            controller.Init(null);

            Assert.AreEqual(0f, controller.Value(StatType.PhysicalAttack), 1e-4f);
        }
    }
}
