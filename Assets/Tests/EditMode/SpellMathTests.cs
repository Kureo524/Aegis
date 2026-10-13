using Features.Spells;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode {
    public class SpellMathTests {
        [Test]
        public void AbilityHaste_ReducesCooldown() {
            Assert.AreEqual(5.5f, SpellMath.CooldownWithHaste(5.5f, 0f), 1e-4f);
            Assert.AreEqual(2.75f, SpellMath.CooldownWithHaste(5.5f, 100f), 1e-4f, "100 haste halves it");
        }

        [Test]
        public void AimPoint_IsClampedToRange_OrAlwaysAtFullRangeWithFixedRange() {
            Vector3 origin = new(1f, 0f, 1f);
            Vector3 close = new(2f, 0f, 1f);   // 1 unit to the right
            Vector3 far = new(11f, 0f, 1f);
            Vector3 right = Vector3.right;

            Assert.AreEqual(close, SpellMath.AimPoint(origin, close, right, 4.5f, fixedRange: false), "close: the cursor point");
            Assert.AreEqual(new Vector3(5.5f, 0f, 1f), SpellMath.AimPoint(origin, far, right, 4.5f, fixedRange: false), "far: clamped");
            Assert.AreEqual(new Vector3(5.5f, 0f, 1f), SpellMath.AimPoint(origin, close, right, 4.5f, fixedRange: true), "fixed: full range");
        }

        [Test]
        public void RawDamage_IsRankBasePlusAdBonusAdAndApRatios() {
            SpellDefinition spell = SpellTestData.Spell("Shot", new[] { 1, 3, 5 }, new[] { 20f, 45f, 70f },
                                                        adRatio: 1.3f, bonusAdRatio: 0.5f, apRatio: 0.15f);
            // rank 2: 45 + 1.3 × 100 + 0.5 × 40 + 0.15 × 20
            Assert.AreEqual(198f, SpellMath.RawDamage(spell, 2, attackDamage: 100f, bonusAttackDamage: 40f, abilityPower: 20f), 1e-3f);
            Assert.AreEqual(70f, SpellMath.RawDamage(spell, 9, 0f, 0f, 0f), 1e-3f, "beyond the table: the last rank");
            Assert.AreEqual(20f, SpellMath.RawDamage(spell, 0, 0f, 0f, 0f), 1e-3f, "rank 0 (enemy spell): the first rank");
            Object.DestroyImmediate(spell);
        }

        [Test]
        public void ClampToRange_KeepsTheDirection() {
            Vector3 p = SpellMath.ClampToRange(Vector3.zero, new Vector3(10f, 0f, 0f), 4f);
            Assert.AreEqual(new Vector3(4f, 0f, 0f), p);
            Assert.AreEqual(new Vector3(1f, 0f, 2f), SpellMath.ClampToRange(Vector3.zero, new Vector3(1f, 0f, 2f), 4f));
        }

        [Test]
        public void Cone_ContainsWhatIsInsideItsAngleAndRange() {
            Vector3 o = Vector3.zero, forward = Vector3.forward;
            Assert.IsTrue(SpellMath.IsInCone(o, forward, 5f, 60f, new Vector3(0f, 0f, 4f), 0f));
            Assert.IsTrue(SpellMath.IsInCone(o, forward, 5f, 60f, new Vector3(1.5f, 0f, 4f), 0f), "~20° off axis");
            Assert.IsFalse(SpellMath.IsInCone(o, forward, 5f, 60f, new Vector3(4f, 0f, 1f), 0f), "~76° off axis");
            Assert.IsFalse(SpellMath.IsInCone(o, forward, 5f, 60f, new Vector3(0f, 0f, 7f), 0.5f), "out of range");
            Assert.IsTrue(SpellMath.IsInCone(o, forward, 5f, 60f, new Vector3(0f, 0f, 5.4f), 0.5f), "body radius counts");
        }
    }
}
