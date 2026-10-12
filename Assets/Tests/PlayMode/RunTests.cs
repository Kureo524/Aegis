using System.Collections;
using Features.Characters;
using Features.Economy;
using Features.Enemies;
using Features.Events;
using Features.Run;
using Features.StateMachines;
using Features.UI.Panels;
using NUnit.Framework;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Game.Tests.PlayMode {
    /// <summary>Phase 7a in the real test scene: waves, quota, rewards, lives, victory and defeat.</summary>
    public class RunTests {
        private static IObjectResolver Root => VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().Container;
        private static IObjectResolver Game => Object.FindFirstObjectByType<Containers.GameLifetimeScope>().Container;
        private static IEventBus Bus => Root.Resolve<IEventBus>();

        [UnityTearDown]
        public IEnumerator TearDown() {
            Time.timeScale = 1f;
            Object.Destroy(VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().gameObject);
            yield return null;
        }

        private static IEnumerator LoadRun() {
            yield return SceneManager.LoadSceneAsync("TestScene", LoadSceneMode.Single);
            yield return null;
        }

        private static IEnumerator WaitFor(System.Func<bool> condition, float timeout = 15f) {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(condition(), "Timed out");
        }

        private static Character Player => Game.Resolve<EnemyDirector>().Player;

        private static void MakePlayerInvulnerable() =>
            Player.Stats.Get(Features.Stats.StatType.MaxHealth).AddModifier(
                new Features.Stats.StatModifier(1e7f, Features.Stats.ModifierType.Flat, "test"));

        private static Character SpawnAndKill(HordeSpawner spawner) {
            HordeSpawnEntry entry = Game.Resolve<RunDirector>().CurrentWave.SpawnTable[0];
            Character enemy = spawner.Spawn(entry.Prefab, entry.Definition, Player.transform.position + new Vector3(-12f, 0f, 0f));
            enemy.TakeDamage(new Features.Combat.DamageInfo(1e7f, Features.Combat.DamageType.True, false, false, Player));
            return enemy;
        }

        [UnityTest]
        public IEnumerator Wave_QuotaThenTimer_OpensTheRewards_ThenTheNextCountdown() {
            yield return LoadRun();
            var run = Game.Resolve<RunDirector>();
            var spawner = Game.Resolve<HordeSpawner>();
            var wallet = Game.Resolve<Wallet>();
            Assert.AreEqual(RunState.PreWave, run.State, "a run starts with a countdown");
            Assert.IsFalse(spawner.Spawning);

            Bus.Publish(new SkipWaveRequestedEvent());   // end of the countdown
            yield return null;
            Assert.AreEqual(RunState.Combat, run.State);
            Assert.AreEqual(1, run.WaveNumber);
            Assert.IsTrue(spawner.Spawning);

            int quota = run.CurrentWave.KillQuota;
            for (int i = 0; i < quota; i++) SpawnAndKill(spawner);
            Assert.AreEqual(quota, run.Kills);
            Assert.IsTrue(run.QuotaReached);
            Assert.AreEqual(RunState.Combat, run.State, "quota alone does not end the wave: the timer must run out");

            int xp = Player.Level.Xp, gold = wallet.Gold;
            Character extra = SpawnAndKill(spawner);
            Assert.AreEqual(xp, Player.Level.Xp, "no more XP once the quota is reached");
            Assert.AreEqual(gold + ((EnemyDefinition)extra.Definition).GoldReward, wallet.Gold, "gold still");

            WaveRewardEvent? reward = null;
            using (Bus.Subscribe<WaveRewardEvent>(e => reward = e)) {
                Bus.Publish(new SkipWaveRequestedEvent());   // timer over
            }
            Assert.AreEqual(RunState.Reward, run.State);
            Assert.AreEqual(0f, Time.timeScale, "end-of-wave screens pause the game");
            Assert.AreEqual(0, spawner.Alive, "remaining enemies vanish");
            Assert.IsTrue(reward.HasValue);
            Assert.AreEqual(RewardKind.Augment, reward.Value.Rewards[0], "wave 1 → augment (schedule prelude)");
            yield return null;
            var augments = Game.Resolve<Features.Augments.AugmentController>();
            Assert.AreEqual(AugmentDraftPhase.Choose, augments.Phase, "wave 1 → augment draft");
            Assert.AreEqual(3, augments.Choices.Count);
            Assert.IsTrue(Object.FindFirstObjectByType<AugmentPanel>().IsOpen);

            augments.Pick(0);
            Assert.AreEqual(1, augments.Owned.Count, "augment taken");
            Assert.AreEqual(RunState.PreWave, run.State);
            Assert.AreEqual(2, run.WaveNumber);
            Assert.AreEqual(1f, Time.timeScale);
        }

        // Closes every end-of-wave screen in turn (augment, anvil, shop)
        private static void FinishRewards() => FinishRewardsFor(Game);

        internal static void FinishRewardsFor(IObjectResolver game) {
            var run = game.Resolve<RunDirector>();
            var shop = game.Resolve<ShopService>();
            for (int guard = 0; guard < 10 && run.State == RunState.Reward; guard++) {
                if (shop.AnvilOpen) shop.Choose(0);
                else if (shop.IsOpen) shop.Close();
                else {
                    var augments = game.Resolve<Features.Augments.AugmentController>();
                    if (augments.Phase == AugmentDraftPhase.SlotsFull) augments.ChooseFullSlotsOption(FullSlotsOption.SlotOrGold, 0);
                    else augments.Pick(0);
                }
            }
        }

        [UnityTest]
        public IEnumerator FreePrismaticAnvil_AfterWave2_ThenShopsSpendTheGold() {
            yield return LoadRun();
            var run = Game.Resolve<RunDirector>();
            var shop = Game.Resolve<ShopService>();
            var inventory = Game.Resolve<Inventory>();
            var wallet = Game.Resolve<Wallet>();

            for (int wave = 0; wave < 2; wave++) {
                Bus.Publish(new SkipWaveRequestedEvent());
                yield return null;
                Bus.Publish(new SkipWaveRequestedEvent());
                yield return null;
                if (wave == 0) Game.Resolve<Features.Augments.AugmentController>().Pick(0);   // augment
            }
            Assert.IsTrue(shop.AnvilOpen, "wave 2 → free prismatic anvil");
            yield return null;
            Assert.IsTrue(Object.FindFirstObjectByType<AnvilPanel>().IsOpen);
            shop.Choose(0);
            Assert.AreEqual(1, inventory.Items.Count);
            Assert.AreEqual(ItemTier.Prismatic, inventory.Items[0].Tier);
            Assert.AreEqual(RunState.PreWave, run.State, "back to the countdown");

            // Wave 3 → light shop: buy a stat anvil with the gold
            Bus.Publish(new SkipWaveRequestedEvent());
            yield return null;
            Bus.Publish(new SkipWaveRequestedEvent());
            yield return null;
            Assert.IsTrue(shop.IsOpen && !shop.IsFull, "wave 3 → light shop");
            yield return null;
            Assert.IsTrue(Object.FindFirstObjectByType<ShopPanel>().IsOpen);
            wallet.Add(100);
            float hp = Player.Stats.Value(Features.Stats.StatType.MaxHealth);
            var statAnvil = Game.Resolve<ShopCatalog>().StatAnvils[0];
            Bus.Publish(new ShopBuyRequestedEvent(statAnvil));
            Assert.IsTrue(shop.AnvilOpen);
            shop.Choose(0);
            Assert.AreEqual(100 - statAnvil.Cost, wallet.Gold);
            Bus.Publish(new ShopCloseRequestedEvent());
            Assert.AreEqual(RunState.PreWave, run.State);
            Assert.IsTrue(Player.Stats.Value(Features.Stats.StatType.MaxHealth) >= hp, "stats kept");
        }

        [UnityTest]
        public IEnumerator Overtime_CostsLives_ThenGameOver_ThenReplayRestartsTheStage() {
            yield return LoadRun();
            var run = Game.Resolve<RunDirector>();
            var machine = Root.Resolve<GameFlowStateMachine>();
            MakePlayerInvulnerable();
            int lives = run.Lives;

            Bus.Publish(new SkipWaveRequestedEvent());
            yield return null;
            Time.timeScale = 40f;   // a wave timer in about a second
            yield return WaitFor(() => run.State == RunState.Overtime);
            Assert.AreEqual(lives - 1, run.Lives, "timer over without the quota: one life lost at once");
            Time.timeScale = 40f;   // TimeScaleService resets it on every run state change

            yield return WaitFor(() => machine.CurrentState == GameState.GameOver, 20f);
            Assert.AreEqual(0, run.Lives);
            Assert.AreEqual(0f, Time.timeScale, "frozen on the defeat screen");
            yield return null;
            Assert.IsTrue(Object.FindFirstObjectByType<RunEndPanel>().IsOpen);
            Assert.AreEqual("Défaite", Object.FindFirstObjectByType<RunEndPanel>().Title);

            Bus.Publish(new PlayRequestedEvent());   // "Rejouer"
            yield return WaitFor(() => machine.CurrentState == GameState.Game);
            yield return null;
            Assert.AreEqual(1f, Time.timeScale);
            var replay = Game.Resolve<RunDirector>();
            Assert.AreEqual(1, replay.WaveNumber, "back to wave 1");
            Assert.AreEqual(lives, replay.Lives);
        }

        [UnityTest]
        public IEnumerator PlayerDeath_IsAnInstantGameOver() {
            yield return LoadRun();
            var machine = Root.Resolve<GameFlowStateMachine>();
            Bus.Publish(new SkipWaveRequestedEvent());
            yield return null;
            Player.TakeDamage(new Features.Combat.DamageInfo(1e7f, Features.Combat.DamageType.True, false, false, null));
            yield return WaitFor(() => machine.CurrentState == GameState.GameOver);
            Assert.AreEqual(Game.Resolve<RunDirector>().Lives, Game.Resolve<StageDefinition>().Lives, "no life involved: the run is over");
            yield return null;
            Assert.IsTrue(Object.FindFirstObjectByType<RunEndPanel>().IsOpen);
            Assert.AreEqual("Défaite", Object.FindFirstObjectByType<RunEndPanel>().Title);
        }

        [UnityTest]
        public IEnumerator ClearingEveryWave_IncludingTheBonusWaves_WinsTheStage() {
            yield return LoadRun();
            var run = Game.Resolve<RunDirector>();
            var machine = Root.Resolve<GameFlowStateMachine>();
            var stage = Game.Resolve<StageDefinition>();

            for (int wave = 0; wave < stage.TotalWaves; wave++) {
                Bus.Publish(new SkipWaveRequestedEvent());   // countdown
                yield return null;
                Assert.AreEqual(RunState.Combat, run.State, $"wave {wave + 1} started");
                Bus.Publish(new SkipWaveRequestedEvent());   // quota + timer
                yield return null;
                if (wave < stage.TotalWaves - 1) FinishRewards();
            }
            yield return WaitFor(() => machine.CurrentState == GameState.Win);
            yield return null;
            var panel = Object.FindFirstObjectByType<RunEndPanel>();
            Assert.IsTrue(panel.IsOpen);
            Assert.AreEqual("Victoire !", panel.Title, "the victory screen, not the defeat one");
        }
    }
}
