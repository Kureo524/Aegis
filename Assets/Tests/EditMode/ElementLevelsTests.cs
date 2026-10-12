using Features.Progression;
using Features.Spells;
using Features.Stats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode {
    public class ElementLevelsTests {
        private ExperienceCurve _curve;
        private CharacterLevel _level;
        private ElementLevels _elements;

        [SetUp]
        public void SetUp() {
            _curve = ScriptableObject.CreateInstance<ExperienceCurve>();
            var so = new SerializedObject(_curve);
            SerializedProperty xp = so.FindProperty("xpToNextLevel");
            xp.arraySize = 17;
            for (int i = 0; i < 17; i++) xp.GetArrayElementAtIndex(i).intValue = 100;
            so.FindProperty("lastSkillPointLevel").intValue = 15;
            so.ApplyModifiedPropertiesWithoutUndo();

            var stats = new StatController();
            stats.Init(null);
            _level = new CharacterLevel(stats, null, _curve);
            _elements = new ElementLevels(_level, new[] { 1, 3, 5 }, maxLevel: 5);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_curve);

        private void ReachLevel(int level) => _level.AddXp((level - _level.Level) * 100);

        [Test]
        public void Level1_CanUnlockOneElement_WithItsSkillPoint() {
            Assert.IsTrue(_elements.TryLevelUp(1));
            Assert.IsTrue(_elements.IsUnlocked(1));
            Assert.AreEqual(0, _level.SkillPoints);
        }

        [Test]
        public void SecondUnlock_WaitsForLevel3() {
            _elements.TryLevelUp(0);
            ReachLevel(2);   // +1 point
            Assert.IsFalse(_elements.TryLevelUp(1), "2nd element only from level 3");
            Assert.IsTrue(_elements.TryLevelUp(0), "the point can still raise the unlocked one");

            ReachLevel(3);
            Assert.IsTrue(_elements.TryLevelUp(1));
            Assert.AreEqual(5, _elements.NextUnlockLevel, "next unlock at level 5");
        }

        [Test]
        public void ThirdUnlock_WaitsForLevel5() {
            ReachLevel(4);   // 4 points
            _elements.TryLevelUp(0);
            _elements.TryLevelUp(1);
            Assert.IsFalse(_elements.TryLevelUp(2));
            ReachLevel(5);
            Assert.IsTrue(_elements.TryLevelUp(2));
            Assert.AreEqual(0, _elements.NextUnlockLevel, "all unlocked");
        }

        [Test]
        public void ElementLevel_IsCappedAtMax_PointsAreKept() {
            ReachLevel(7);   // 7 points
            for (int i = 0; i < 6; i++) _elements.TryLevelUp(0);

            Assert.AreEqual(5, _elements.Level(0));
            Assert.AreEqual(2, _level.SkillPoints, "unspent points are kept");
        }

        [Test]
        public void NoPoint_NoLevelUp() {
            _elements.TryLevelUp(0);
            Assert.IsFalse(_elements.TryLevelUp(0));
            Assert.AreEqual(1, _elements.Level(0));
        }
    }
}
