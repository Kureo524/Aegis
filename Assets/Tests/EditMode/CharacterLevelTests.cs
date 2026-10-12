using Features.Progression;
using Features.Stats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode {
    public class CharacterLevelTests {
        private ExperienceCurve _curve;
        private StatGrowth _growth;
        private StatController _stats;

        [SetUp]
        public void SetUp() {
            // 5 levels: 100, 200, 300, 400 XP to go up; skill points on levels 1..3
            _curve = ScriptableObject.CreateInstance<ExperienceCurve>();
            var curveSo = new SerializedObject(_curve);
            SerializedProperty xp = curveSo.FindProperty("xpToNextLevel");
            xp.arraySize = 4;
            for (int i = 0; i < 4; i++) xp.GetArrayElementAtIndex(i).intValue = 100 * (i + 1);
            curveSo.FindProperty("lastSkillPointLevel").intValue = 3;
            curveSo.ApplyModifiedPropertiesWithoutUndo();

            _growth = CreateGrowth(GrowthMode.Linear,
                (StatType.PhysicalAttack, 10f, ModifierType.Flat),
                (StatType.AttackSpeed, 0.1f, ModifierType.PercentAdd));

            _stats = new StatController();
            _stats.Init(null);
            _stats.Get(StatType.PhysicalAttack).BaseValue = 50f;
            _stats.Get(StatType.AttackSpeed).BaseValue = 1f;
        }

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(_curve);
            Object.DestroyImmediate(_growth);
        }

        private static StatGrowth CreateGrowth(GrowthMode mode, params (StatType stat, float gain, ModifierType type)[] entries) {
            var growth = ScriptableObject.CreateInstance<StatGrowth>();
            var so = new SerializedObject(growth);
            so.FindProperty("mode").enumValueIndex = (int)mode;
            SerializedProperty list = so.FindProperty("entries");
            list.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++) {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Stat").enumValueIndex = (int)entries[i].stat;
                e.FindPropertyRelative("GainPerLevel").floatValue = entries[i].gain;
                e.FindPropertyRelative("Type").enumValueIndex = (int)entries[i].type;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return growth;
        }

        [Test]
        public void StartsAtLevel1_WithOneSkillPoint_AndNoGrowth() {
            var level = new CharacterLevel(_stats, _growth, _curve);

            Assert.AreEqual(1, level.Level);
            Assert.AreEqual(1, level.SkillPoints);
            Assert.AreEqual(100, level.XpToNext);
            Assert.AreEqual(50f, _stats.Value(StatType.PhysicalAttack), 1e-4f);
        }

        [Test]
        public void ExactThreshold_LevelsUp_AndLeftoverXpCarriesOver() {
            var level = new CharacterLevel(_stats, _growth, _curve);
            int levelUps = 0;
            level.LevelChanged += _ => levelUps++;

            level.AddXp(100);
            Assert.AreEqual(2, level.Level);
            Assert.AreEqual(0, level.Xp);

            level.AddXp(250);   // 200 to reach level 3, 50 left
            Assert.AreEqual(3, level.Level);
            Assert.AreEqual(50, level.Xp);
            Assert.AreEqual(2, levelUps);
        }

        [Test]
        public void BigXpGain_CanSkipSeveralLevels() {
            var level = new CharacterLevel(_stats, _growth, _curve);
            level.AddXp(100 + 200 + 300);

            Assert.AreEqual(4, level.Level);
        }

        [Test]
        public void SkillPoints_UpToLastSkillPointLevel_ThenOverchargePoints() {
            var level = new CharacterLevel(_stats, _growth, _curve);
            level.AddXp(10_000);

            Assert.AreEqual(5, level.Level);
            Assert.IsTrue(level.IsMaxLevel);
            Assert.AreEqual(3, level.SkillPoints, "levels 1, 2, 3");
            Assert.AreEqual(2, level.OverchargePoints, "levels 4, 5");
        }

        [Test]
        public void MaxLevel_IgnoresFurtherXp() {
            var level = new CharacterLevel(_stats, _growth, _curve);
            level.AddXp(10_000);
            level.AddXp(500);

            Assert.AreEqual(5, level.Level);
            Assert.AreEqual(0, level.Xp);
            Assert.AreEqual(0, level.XpToNext);
        }

        [Test]
        public void Growth_IsReplacedEachLevel_NotStacked() {
            var level = new CharacterLevel(_stats, _growth, _curve);
            level.AddXp(100 + 200);   // level 3

            // Linear: 50 + 10 × (3 − 1)
            Assert.AreEqual(70f, _stats.Value(StatType.PhysicalAttack), 1e-4f);
            Assert.AreEqual(1, _stats.Get(StatType.PhysicalAttack).Modifiers.Count);
        }

        [Test]
        public void AttackSpeedGrowth_IsAPercentBonus() {
            var level = new CharacterLevel(_stats, _growth, _curve);
            level.AddXp(100 + 200);   // level 3

            // 1 × (1 + 0.1 × 2)
            Assert.AreEqual(1.2f, _stats.Value(StatType.AttackSpeed), 1e-4f);
        }

        [Test]
        public void LoLGrowth_FollowsTheNonLinearCurve() {
            StatGrowth lol = CreateGrowth(GrowthMode.LoL, (StatType.PhysicalAttack, 10f, ModifierType.Flat));
            var level = new CharacterLevel(_stats, lol, _curve);
            level.AddXp(10_000);   // level 5, n − 1 = 4

            // 50 + 10 × 4 × (0.7025 + 0.0175 × 4)
            Assert.AreEqual(50f + 10f * 4f * (0.7025f + 0.0175f * 4f), _stats.Value(StatType.PhysicalAttack), 1e-3f);
            Object.DestroyImmediate(lol);
        }
    }
}
