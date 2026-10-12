using Features.Combat;
using NUnit.Framework;

namespace Game.Tests.EditMode {
    public class DamageCalculatorTests {
        private static DamageInfo Hit(float raw, DamageType type, float penFlat = 0f, float penPercent = 0f) =>
            type == DamageType.Magic
                ? new DamageInfo(raw, type, false, false, null, magicPenFlat: penFlat, magicPenPercent: penPercent)
                : new DamageInfo(raw, type, false, false, null, armorPenFlat: penFlat, armorPenPercent: penPercent);

        [Test]
        public void HundredArmor_HalvesPhysicalDamage() {
            Assert.AreEqual(50f, DamageCalculator.Mitigate(Hit(100f, DamageType.Physical), armor: 100f, magicResist: 0f), 1e-3f);
        }

        [Test]
        public void MagicDamage_UsesMagicResist_NotArmor() {
            Assert.AreEqual(50f, DamageCalculator.Mitigate(Hit(100f, DamageType.Magic), armor: 300f, magicResist: 100f), 1e-3f);
        }

        [Test]
        public void TrueDamage_IgnoresResistances() {
            Assert.AreEqual(100f, DamageCalculator.Mitigate(Hit(100f, DamageType.True), armor: 300f, magicResist: 300f), 1e-3f);
        }

        [Test]
        public void PercentPen_AppliesBeforeFlatPen() {
            // 100 × (1 − 0.3) − 20 = 50
            Assert.AreEqual(50f, DamageCalculator.EffectiveResistance(100f, penFlat: 20f, penPercent: 0.3f), 1e-3f);
        }

        [Test]
        public void Penetration_NeverGoesBelowZero() {
            Assert.AreEqual(0f, DamageCalculator.EffectiveResistance(10f, penFlat: 50f, penPercent: 0f), 1e-3f);
        }

        [Test]
        public void NegativeResistance_IncreasesDamage() {
            // 2 − 100 / (100 + 25)
            Assert.AreEqual(1.2f, DamageCalculator.Multiplier(-25f), 1e-3f);
            Assert.AreEqual(-25f, DamageCalculator.EffectiveResistance(-25f, 10f, 0.5f), 1e-3f, "pen does not touch negative resistance");
        }

        [Test]
        public void Sustain_LifestealOnlyOnBasicAttacks_ScaledByHealShieldPower() {
            Assert.AreEqual((100f * 0.1f + 100f * 0.2f) * 1.5f, DamageCalculator.Sustain(100f, true, 0.1f, 0.2f, 1.5f), 1e-3f);
            Assert.AreEqual(100f * 0.1f * 1.5f, DamageCalculator.Sustain(100f, false, 0.1f, 0.2f, 1.5f), 1e-3f);
        }
    }
}
