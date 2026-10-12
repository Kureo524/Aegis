using Features.Run;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode {
    public class WaveRewardScheduleTests {
        [Test]
        public void Prelude_ThenLoop_ThenBonusWaves() {
            var schedule = ScriptableObject.CreateInstance<WaveRewardSchedule>();   // defaults = design doc §9.3
            RewardKind[] expected = {
                RewardKind.Augment, RewardKind.FreePrismaticAnvil,
                RewardKind.LightShop, RewardKind.Augment, RewardKind.FullShop,
                RewardKind.LightShop, RewardKind.Augment, RewardKind.FullShop,
            };
            for (int i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], schedule.AfterWave(i)[0], $"after wave {i + 1}");

            CollectionAssert.AreEqual(new[] { RewardKind.FullShop }, schedule.AfterBonusWave(0));
            CollectionAssert.AreEqual(new[] { RewardKind.FullShop, RewardKind.Augment }, schedule.AfterBonusWave(1), "augment every 2 bonus waves");
            Object.DestroyImmediate(schedule);
        }
    }
}
