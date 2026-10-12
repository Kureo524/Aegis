using System.Collections;
using Features.Characters;
using Features.Economy;
using Features.Enemies;
using Features.Events;
using NUnit.Framework;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Game.Tests.PlayMode {
    /// <summary>Phase 6 in the real test scene: horde AI, flow field, elite special spell, rewards.</summary>
    public class EnemyTests {
        private static IObjectResolver Root => VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().Container;
        private static IObjectResolver Game => Object.FindFirstObjectByType<Containers.GameLifetimeScope>().Container;

        [UnityTearDown]
        public IEnumerator TearDown() {
            Object.Destroy(VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().gameObject);
            yield return null;
        }

        private static Character Player => Game.Resolve<EnemyDirector>().Player;

        // Enemies of the test scene's spawn table, by asset name (Enemy_Goblin…)
        private static Character SpawnEnemy(string assetName, Vector3 position) {
            foreach (HordeSpawnEntry entry in Game.Resolve<HordeSettings>().SpawnTable) {
                if (entry.Definition.name == assetName) return Game.Resolve<HordeSpawner>().Spawn(entry.Prefab, entry.Definition, position);
            }
            Assert.Fail($"{assetName} not in the horde spawn table");
            return null;
        }

        private static float FlatDistance(Character a, Character b) {
            Vector3 d = a.transform.position - b.transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        [UnityTest]
        public IEnumerator Goblin_ChasesThePlayer_AndHitsIt() {
            yield return GameFlowSmokeTests.LoadGameScene();
            var goblin = SpawnEnemy("Enemy_Goblin", Player.transform.position + new Vector3(-7f, 0f, -2f));
            float hp = Player.Health.Current;

            yield return new WaitForSeconds(4f);
            Assert.Less(FlatDistance(goblin, Player), 2f, "came to the player");
            Assert.Less(Player.Health.Current, hp, "and hit it");
        }

        [UnityTest]
        public IEnumerator Goblin_FollowsTheFlowField_AroundAWall() {
            yield return GameFlowSmokeTests.LoadGameScene();
            // Wall_Front spans x -5..5 at z 5.5..6.5, the player stands at the origin
            var goblin = SpawnEnemy("Enemy_Goblin", new Vector3(0f, 0f, 9f));
            yield return new WaitForSeconds(7f);
            Assert.Less(FlatDistance(goblin, Player), 2f, "went around the wall");
        }

        [UnityTest]
        public IEnumerator Archer_StopsAtItsRange() {
            yield return GameFlowSmokeTests.LoadGameScene();
            var archer = SpawnEnemy("Enemy_SkeletonArcher", Player.transform.position + new Vector3(-12f, 0f, -3f));
            float range = archer.Stats.Value(Features.Stats.StatType.AttackRange);
            float hp = Player.Health.Current;

            yield return new WaitForSeconds(5f);
            float distance = FlatDistance(archer, Player);
            Assert.LessOrEqual(distance, range + Player.BodyRadius + 0.3f, "in range");
            Assert.Greater(distance, range - 1.5f, "but keeps its distance");
            Assert.Less(Player.Health.Current, hp, "shooting");
        }

        [UnityTest]
        public IEnumerator Brute_CastsItsSpecialSpell_WhenItsManaIsFull() {
            yield return GameFlowSmokeTests.LoadGameScene();
            var brute = SpawnEnemy("Enemy_Brute", Player.transform.position + new Vector3(-2.5f, 0f, 0f));
            var definition = (EnemyDefinition)brute.Definition;
            Assert.AreEqual(definition.StartMana, brute.Mana.Current, 0.01f, "TFT mana starts at StartMana");

            string cast = null;
            using (Root.Resolve<IEventBus>().Subscribe<SpellCastEvent>(e => { if (!e.ByPlayer) cast = e.Spell.DisplayName; })) {
                brute.Mana.Set(brute.Mana.Max);
                yield return new WaitForSeconds(definition.SpecialSpell.CastTime + 0.5f);
            }
            Assert.AreEqual(definition.SpecialSpell.DisplayName, cast, "special spell cast at full mana");
            Assert.Less(brute.Mana.Current, brute.Mana.Max * 0.5f, "mana back to its start value");
        }

        [UnityTest]
        public IEnumerator Kill_GivesXpAndGold_ThenTheBodyGoesBackToThePool() {
            yield return GameFlowSmokeTests.LoadGameScene();
            var goblin = SpawnEnemy("Enemy_Goblin", Player.transform.position + new Vector3(-10f, 0f, 0f));
            var definition = (EnemyDefinition)goblin.Definition;
            var wallet = Game.Resolve<Wallet>();
            var spawner = Game.Resolve<HordeSpawner>();
            int xp = Player.Level.Xp, gold = wallet.Gold;

            goblin.TakeDamage(new Features.Combat.DamageInfo(100000f, Features.Combat.DamageType.True, false, false, Player));
            Assert.AreEqual(gold + definition.GoldReward, wallet.Gold);
            Assert.AreEqual(xp + definition.XpReward, Player.Level.Xp);
            Assert.AreEqual(0, spawner.Alive);

            yield return new WaitForSeconds(Game.Resolve<HordeSettings>().DespawnDelay + 0.2f);
            Assert.IsFalse(goblin.gameObject.activeSelf, "released to the pool");

            var reused = SpawnEnemy("Enemy_Goblin", Player.transform.position + new Vector3(-10f, 0f, 0f));
            Assert.AreSame(goblin, reused, "pooled instance reused");
            Assert.IsTrue(reused.IsAlive);
            Assert.AreEqual(reused.Health.Max, reused.Health.Current);
        }

        [UnityTest]
        public IEnumerator PlayerDeath_ClearsTheHorde_ThenThePlayerRespawns() {
            yield return GameFlowSmokeTests.LoadGameScene();
            var spawner = Game.Resolve<HordeSpawner>();
            for (int i = 0; i < 5; i++) spawner.TrySpawn(Player.transform.position);
            Assert.Greater(spawner.Alive, 0);

            Player.TakeDamage(new Features.Combat.DamageInfo(100000f, Features.Combat.DamageType.True, false, false, null));
            Assert.IsFalse(Player.IsAlive);
            Assert.AreEqual(0, spawner.Alive, "horde cleared");

            yield return new WaitForSeconds(Root.Resolve<Features.Flow.GameFlowSettings>().PlayerRespawnDelay + 0.3f);
            Assert.IsTrue(Player.IsAlive, "respawned");
            Assert.AreEqual(Player.Health.Max, Player.Health.Current);
        }

        [UnityTest]
        public IEnumerator ContinuousSpawn_KeepsUnderMaxAlive() {
            yield return GameFlowSmokeTests.LoadGameScene();
            var spawner = Game.Resolve<HordeSpawner>();
            var settings = Game.Resolve<HordeSettings>();
            float closest = float.MaxValue;
            int seen = 0;
            using (Root.Resolve<IEventBus>().Subscribe<HordeStatusEvent>(e => {
                if (e.Alive <= seen) return;
                seen = e.Alive;
                Vector3 d = spawner.AliveEnemies[e.Alive - 1].transform.position - Player.transform.position;
                d.y = 0f;
                closest = Mathf.Min(closest, d.magnitude);
            })) {
                spawner.SetSpawning(true);
                yield return new WaitForSeconds(settings.SpawnInterval * 3.5f);
            }
            Assert.GreaterOrEqual(spawner.Alive, 2, "spawning");
            Assert.LessOrEqual(spawner.Alive, settings.MaxAlive);
            Assert.Greater(closest, settings.MinSpawnDistance - 1.6f, "spawned on the ring, away from the player");
        }
    }
}
