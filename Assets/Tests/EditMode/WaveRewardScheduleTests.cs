using System.Linq;
using Features.Run;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode {
    public class WaveRewardScheduleTests {
        [Test]
        public void Prelude_ThenLoop_ThenBonusWaves_NoShopSteps() {
            var schedule = ScriptableObject.CreateInstance<WaveRewardSchedule>();   // defaults = design doc §9.3
            RewardKind?[] expected = {
                RewardKind.Augment, RewardKind.FreePrismaticAnvil,
                null, RewardKind.Augment, null,
                null, RewardKind.Augment, null,
            };
            for (int i = 0; i < expected.Length; i++) {
                RewardKind? got = schedule.AfterWave(i).Count > 0 ? schedule.AfterWave(i)[0] : null;
                Assert.AreEqual(expected[i], got, $"after wave {i + 1} (null: just the break, the shop is always there)");
            }
            CollectionAssert.IsEmpty(schedule.AfterBonusWave(0));
            CollectionAssert.AreEqual(new[] { RewardKind.Augment }, schedule.AfterBonusWave(1).ToArray(), "augment every 2 bonus waves");
            Object.DestroyImmediate(schedule);
        }
    }
}
