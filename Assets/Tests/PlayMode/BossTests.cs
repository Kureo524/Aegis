using System.Collections;
using Features.Characters;
using Features.Economy;
using Features.Enemies;
using Features.Events;
using Features.Run;
using Features.Stats;
using NUnit.Framework;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Game.Tests.PlayMode {
    /// <summary>Phase 9 boss in the real test scene: items, pattern phases, boss wave.</summary>
    public class BossTests {
        private static IObjectResolver Root => VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().Container;
        private static IObjectResolver Game => Object.FindFirstObjectByType<Containers.GameLifetimeScope>().Container;
        private static IEventBus Bus => Root.Resolve<IEventBus>();
        private static Character Player => Game.Resolve<EnemyDirector>().Player;

        [UnityTearDown]
        public IEnumerator TearDown() {
            Time.timeScale = 1f;
            Object.Destroy(VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().gameObject);
            yield return null;
        }

        private static WaveDefinition BossWave() {
            foreach (WaveDefinition w in Game.Resolve<StageDefinition>().Waves) if (w.HasBoss) return w;
            Assert.Fail("the test stage has a boss wave");
            return null;
        }

        private static void MakePlayerInvulnerable() =>
            Player.Stats.Get(StatType.MaxHealth).AddModifier(new StatModifier(1e7f, ModifierType.Flat, "test"));

        private static void Hit(Character target, float damage) =>
            target.TakeDamage(new Features.Combat.DamageInfo(damage, Features.Combat.DamageType.True, false, false, Player));

        [UnityTest]
        public IEnumerator Boss_WearsItsItems_PlaysItsPattern_ChangesPhaseWithItsHealth() {
            yield return GameFlowSmokeTests.LoadGameScene();
            MakePlayerInvulnerable();
            WaveDefinition wave = BossWave();
            Character boss = Game.Resolve<HordeSpawner>().Spawn(wave.BossPrefab, wave.Boss, Player.transform.position + new Vector3(-6f, 0f, 0f));
            var runner = boss.GetComponent<PatternRunner>();
            Assert.IsTrue(runner.IsBoss);
            Assert.AreEqual(0, runner.Phase);
            yield return null;
            Assert.IsTrue(Object.FindFirstObjectByType<Features.UI.BossHudView>().IsVisible, "boss bar shown");

            float baseAttack = wave.Boss.BaseStats.Get(StatType.PhysicalAttack);
            float itemsAttack = 0f;
            foreach (ItemDefinition item in wave.Boss.Items)
                foreach (StatBonus b in item.Stats) if (b.Stat == StatType.PhysicalAttack) itemsAttack += b.Value;
            Assert.AreEqual(baseAttack + itemsAttack, boss.Stats.Value(StatType.PhysicalAttack), 1e-3f, "item stats on the boss");

            string cast = null;
            using (Bus.Subscribe<SpellCastEvent>(e => { if (!e.ByPlayer) cast = e.Spell.DisplayName; })) {
                yield return new WaitForSeconds(5.5f);   // wait 3 s, then the slam and its cast time
            }
            Assert.IsNotNull(cast, "phase 1 casts its spell");

            Hit(boss, boss.Health.Max * 0.45f);
            Assert.AreEqual(1, runner.Phase, "below 60 %: phase 2");
            Hit(boss, boss.Health.Max * 0.3f);
            Assert.AreEqual(2, runner.Phase, "below 30 %: phase 3");

            bool defeated = false;
            using (Bus.Subscribe<BossDefeatedEvent>(_ => defeated = true)) Hit(boss, boss.Health.Max);
            Assert.IsTrue(defeated);
        }

        [UnityTest]
        public IEnumerator BossWave_EndsOnlyOnceTheBossIsDead() {
            yield return SceneManager.LoadSceneAsync("TestScene", LoadSceneMode.Single);
            yield return null;
            MakePlayerInvulnerable();
            var run = Game.Resolve<RunDirector>();
            var spawner = Game.Resolve<HordeSpawner>();
            WaveDefinition bossWave = BossWave();

            // Skip to the boss wave
            for (int guard = 0; guard < 20 && run.CurrentWave != bossWave; guard++) {
                Bus.Publish(new SkipWaveRequestedEvent());
                yield return null;
                if (run.State == RunState.Combat) Bus.Publish(new SkipWaveRequestedEvent());
                yield return null;
                RunTests.FinishRewardsFor(Game);
            }
            Bus.Publish(new SkipWaveRequestedEvent());   // its countdown
            yield return null;
            Assert.AreEqual(RunState.Combat, run.State);
            Assert.IsTrue(run.BossAlive, "the boss spawned with the wave");
            Character boss = null;
            foreach (Character c in spawner.AliveEnemies) if (c.Definition is BossDefinition) boss = c;
            Assert.IsNotNull(boss);
            // The player fights back on her own: no basic attacks, the boss must outlast the timer at 60x
            var attacker = Player.GetComponent<Features.Combat.BasicAttacker>();
            attacker.Cancel();
            attacker.enabled = false;
            // The random items of the skipped waves must not kill it in 10 min of game time (thorns scale
            // with the test's huge max health)
            var inventory = Game.Resolve<Inventory>();
            while (inventory.Items.Count > 0) inventory.RemoveAt(0);
            boss.Stats.Get(StatType.MaxHealth).AddModifier(new StatModifier(1e6f, ModifierType.Flat, "test"));

            // Quota reached, timer over: the boss still stands → overtime
            HordeSpawnEntry entry = bossWave.SpawnTable[0];
            for (int i = 0; i < bossWave.KillQuota; i++) {
                Character e = spawner.Spawn(entry.Prefab, entry.Definition, Player.transform.position + new Vector3(-12f, 0f, 0f));
                Hit(e, e.Health.Max);   // no overkill: a splash / chain of the random rewards would carry it to the boss
            }
            Assert.IsTrue(run.QuotaReached);
            Time.timeScale = 60f;
            float end = Time.realtimeSinceStartup + 10f;
            while (run.State == RunState.Combat && Time.realtimeSinceStartup < end) yield return null;
            Time.timeScale = 1f;
            Assert.AreEqual(RunState.Overtime, run.State, "the boss is alive: the wave is not over");

            int wave = run.WaveNumber;
            bool ended = false;
            using (Game.Resolve<Systems.EventBusses.IEventBus>().Subscribe<Features.Events.WaveRewardEvent>(_ => ended = true)) Hit(boss, 1e7f);
            Assert.IsTrue(ended, "boss dead + quota: the wave ends");
            Assert.IsTrue(run.State == RunState.Reward || (run.State == RunState.PreWave && run.WaveNumber == wave + 1),
                          "its rewards, or straight to the break when it has none");
        }
    }
}
