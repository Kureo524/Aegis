using Features.Spells;
using NUnit.Framework;
using UnityEditor;
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
        public void ElementLevels_CountOncePerDistinctElement() {
            ElementDefinition fire = SpellTestData.Element("Fire", 10f);
            ElementDefinition elec = SpellTestData.Element("Elec", 10f);
            ElementDefinition wind = SpellTestData.Element("Wind", 10f);
            var slots = new[] { fire, elec, wind };
            int[] levels = { 3, 2, 5 };

            SpellDefinition fireX3 = SpellTestData.Spell("Meteor", (fire, 3));
            SpellDefinition fireFireElec = SpellTestData.Spell("Lance", (fire, 2), (elec, 1));

            Assert.AreEqual(2, SpellMath.SumOfLevelsAboveOne(fireX3, slots, s => levels[s]), "Fire x3 counts Fire once");
            Assert.AreEqual(3, SpellMath.SumOfLevelsAboveOne(fireFireElec, slots, s => levels[s]), "(3-1) + (2-1)");
            Object.DestroyImmediate(fireX3);
            Object.DestroyImmediate(fireFireElec);
        }

        [Test]
        public void RawDamage_IsScalingPlusStatRatio() {
            SpellDefinition spell = SpellTestData.Spell("Bolt");
            var so = new SerializedObject(spell);
            so.FindProperty("powerRatio").floatValue = 0.5f;
            so.FindProperty("scaling").FindPropertyRelative("Base").floatValue = 100f;
            so.FindProperty("scaling").FindPropertyRelative("PerElementLevel").floatValue = 20f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 100 + 20 × 2 + 40 (magic attack) × 0.5
            Assert.AreEqual(160f, SpellMath.RawDamage(spell, 2, magicAttack: 40f, physicalAttack: 999f), 1e-3f);
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
