using Features.Progression;
using Features.Spells;
using Features.Stats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode {
    public class AbilityRanksTests {
        private ExperienceCurve _curve;
        private SpellDefinition[] _kit;
        private CharacterLevel _level;
        private AbilityRanks _ranks;

        [SetUp]
        public void SetUp() {
            // 18 levels, 10 XP each, a skill point on every level (LoL)
            _curve = ScriptableObject.CreateInstance<ExperienceCurve>();
            var so = new SerializedObject(_curve);
            SerializedProperty xp = so.FindProperty("xpToNextLevel");
            xp.arraySize = 17;
            for (int i = 0; i < 17; i++) xp.GetArrayElementAtIndex(i).intValue = 10;
            so.FindProperty("lastSkillPointLevel").intValue = 18;
            so.ApplyModifiedPropertiesWithoutUndo();

            int[] basic = { 1, 3, 5, 7, 9 };
            _kit = new[] {
                SpellTestData.Spell("Q", basic), SpellTestData.Spell("W", basic), SpellTestData.Spell("E", basic),
                SpellTestData.Spell("R", new[] { 6, 11, 16 }),
            };
            var stats = new StatController();
            stats.Init(null);
            _level = new CharacterLevel(stats, null, _curve);
            _ranks = new AbilityRanks(_kit, _level);
        }

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(_curve);
            foreach (SpellDefinition s in _kit) Object.DestroyImmediate(s);
        }

        private void ReachLevel(int level) => _level.AddXp(10 * (level - _level.Level));

        [Test]
        public void Level1_OnePoint_OneBasicAbility() {
            Assert.AreEqual(1, _level.SkillPoints);
            Assert.IsFalse(_ranks.CanRankUp(3), "R waits for level 6");
            Assert.IsTrue(_ranks.TryRankUp(0));
            Assert.AreEqual(1, _ranks.Rank(0));
            Assert.AreEqual(0, _level.SkillPoints);
            Assert.IsFalse(_ranks.TryRankUp(1), "no point left");
            Assert.AreEqual("Aucun point de compétence", _ranks.RefusalReason(1));
        }

        [Test]
        public void BasicRank_NeedsLevel_TwiceTheRankMinusOne() {
            _ranks.TryRankUp(0);
            ReachLevel(2);
            Assert.IsFalse(_ranks.TryRankUp(0), "Q rank 2 needs level 3");
            Assert.AreEqual("Disponible au niveau 3", _ranks.RefusalReason(0));
            ReachLevel(3);
            Assert.IsTrue(_ranks.TryRankUp(0));
            Assert.AreEqual(2, _ranks.Rank(0));
            Assert.AreEqual(1, _level.SkillPoints, "the level 2 point is still there");
        }

        [Test]
        public void Ultimate_AtLevels6_11_16_ThenMax() {
            ReachLevel(5);
            Assert.IsFalse(_ranks.CanRankUp(3));
            ReachLevel(6);
            Assert.IsTrue(_ranks.TryRankUp(3));
            Assert.IsFalse(_ranks.TryRankUp(3), "rank 2 at level 11");
            ReachLevel(11);
            Assert.IsTrue(_ranks.TryRankUp(3));
            ReachLevel(16);
            Assert.IsTrue(_ranks.TryRankUp(3));
            Assert.AreEqual(3, _ranks.Rank(3));
            ReachLevel(18);
            Assert.IsFalse(_ranks.TryRankUp(3));
            Assert.AreEqual("Rang maximum", _ranks.RefusalReason(3));
        }

        [Test]
        public void EighteenPoints_FillTheWholeKit() {
            ReachLevel(18);
            Assert.AreEqual(18, _level.SkillPoints);
            int spent = 0;
            for (int pass = 0; pass < 5; pass++)
                for (int slot = 0; slot < 4; slot++)
                    if (_ranks.TryRankUp(slot)) spent++;
            Assert.AreEqual(18, spent, "5 + 5 + 5 + 3");
            Assert.AreEqual(0, _level.SkillPoints);
        }

        [Test]
        public void Changed_IsRaisedOnRankUp() {
            int changes = 0;
            _ranks.Changed += () => changes++;
            _ranks.TryRankUp(2);
            Assert.AreEqual(1, changes);
        }
    }
}
