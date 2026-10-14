using System.Collections;
using Features.Characters;
using Features.Combat;
using Features.Enemies;
using Features.Run;
using Features.Spells;
using Features.Stats;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Game.Tests.PlayMode {
    /// <summary>Phase 9c enemies in the real test scene: wolf, skeleton mage, goblin shaman, stone golem.</summary>
    public class EnemyContentTests {
        private static IObjectResolver Game => Object.FindFirstObjectByType<Containers.GameLifetimeScope>().Container;
        private static Character Player => Game.Resolve<EnemyDirector>().Player;

        [UnityTearDown]
        public IEnumerator TearDown() {
            Time.timeScale = 1f;
            Object.Destroy(VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().gameObject);
            yield return null;
        }

        // Any enemy of the test stage's waves, by asset name
        private static Character Spawn(string assetName, Vector3 position) {
            StageDefinition stage = Game.Resolve<StageDefinition>();
            foreach (WaveDefinition wave in stage.Waves)
                foreach (HordeSpawnEntry e in wave.SpawnTable)
                    if (e.Definition.name == assetName) return Game.Resolve<HordeSpawner>().Spawn(e.Prefab, e.Definition, position);
            foreach (WaveDefinition wave in stage.BonusWaves)
                foreach (HordeSpawnEntry e in wave.SpawnTable)
                    if (e.Definition.name == assetName) return Game.Resolve<HordeSpawner>().Spawn(e.Prefab, e.Definition, position);
            Assert.Fail($"{assetName} in no wave of the test stage");
            return null;
        }

        private static void MakePlayerInvulnerable() =>
            Player.Stats.Get(StatType.MaxHealth).AddModifier(new StatModifier(1e7f, ModifierType.Flat, "test"));

        private static CastContext At(Character caster, Character target) {
            Vector3 origin = caster.transform.position;
            return new CastContext { Origin = origin, Direction = SpellMath.FlatDirection(origin, target.transform.position, Vector3.forward),
                                     TargetPoint = SpellMath.ClampToRange(origin, target.transform.position, 10f), TargetUnit = target };
        }

        private static IEnumerator Until(System.Func<bool> condition, float seconds) {
            float end = Time.time + seconds;
            while (!condition() && Time.time < end) yield return null;
        }

        [UnityTest]
        public IEnumerator Wolf_BitesMakeThePlayerBleed() {
            yield return GameFlowSmokeTests.LoadGameScene();
            MakePlayerInvulnerable();
            Spawn("Enemy_Wolf", Player.transform.position + new Vector3(2f, 0f, 0f));
            yield return Until(() => Player.Status.IsBleeding, 6f);
            Assert.IsTrue(Player.Status.IsBleeding);
        }

        [UnityTest]
        public IEnumerator SkeletonMage_FrostBolt_IsTelegraphedAndSlows() {
            yield return GameFlowSmokeTests.LoadGameScene();
            MakePlayerInvulnerable();
            Character mage = Spawn("Enemy_SkeletonMage", Player.transform.position + new Vector3(5f, 0f, 0f));
            mage.GetComponent<EnemyAI>().Suspended = true;
            SpellDefinition bolt = ((EnemyDefinition)mage.Definition).SpecialSpell;
            Assert.AreEqual(TargetingType.Skillshot, bolt.Targeting);
            Assert.IsTrue(mage.GetComponent<SpellCaster>().Cast(bolt, At(mage, Player), 0));
            yield return Until(() => Player.Status.IsSlowed, 3f);
            Assert.IsTrue(Player.Status.IsSlowed, "the bolt hits a player standing still, and slows");
        }

        [UnityTest]
        public IEnumerator GoblinShaman_Ward_ShieldsAndSpeedsUpItsAllies_NotThePlayer() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Vector3 far = Player.transform.position + new Vector3(8f, 0f, 0f);
            Character shaman = Spawn("Enemy_GoblinShaman", far);
            Character goblin = Spawn("Enemy_Goblin", far + new Vector3(1.5f, 0f, 0f));
            float speed = goblin.Stats.Value(StatType.AttackSpeed);
            SpellDefinition ward = ((EnemyDefinition)shaman.Definition).SpecialSpell;
            Assert.IsTrue(ward.AffectsAllies);
            Game.Resolve<SpellExecutor>().Execute(shaman, ward, At(shaman, Player), 0);
            Assert.Greater(goblin.Health.Shield, 0f, "the goblin nearby is shielded");
            Assert.Greater(goblin.Stats.Value(StatType.AttackSpeed), speed);
            Assert.Greater(shaman.Health.Shield, 0f, "and the shaman itself");
            Assert.AreEqual(0f, Player.Health.Shield, "never the player");
            yield return null;
        }

        [UnityTest]
        public IEnumerator StoneGolem_Quake_RootsThePlayer() {
            yield return GameFlowSmokeTests.LoadGameScene();
            MakePlayerInvulnerable();
            Character golem = Spawn("Enemy_StoneGolem", Player.transform.position + new Vector3(2.5f, 0f, 0f));
            golem.GetComponent<EnemyAI>().Suspended = true;
            SpellDefinition quake = ((EnemyDefinition)golem.Definition).SpecialSpell;
            Game.Resolve<SpellExecutor>().Execute(golem, quake, At(golem, Player), 0);
            Assert.IsTrue(Player.Status.IsRooted);
            Assert.Greater(golem.Stats.Value(StatType.Tenacity), 0f, "the golem resists crowd control");
            yield return null;
        }

        [UnityTest]
        public IEnumerator NewEnemies_AreInTheStageWaves() {
            yield return GameFlowSmokeTests.LoadGameScene();
            var stage = Game.Resolve<StageDefinition>();
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (WaveDefinition w in stage.Waves) foreach (HordeSpawnEntry e in w.SpawnTable) names.Add(e.Definition.name);
            foreach (WaveDefinition w in stage.BonusWaves) foreach (HordeSpawnEntry e in w.SpawnTable) names.Add(e.Definition.name);
            foreach (string n in new[] { "Enemy_Wolf", "Enemy_SkeletonMage", "Enemy_GoblinShaman", "Enemy_StoneGolem" })
                Assert.IsTrue(names.Contains(n), n);
        }
    }
}
