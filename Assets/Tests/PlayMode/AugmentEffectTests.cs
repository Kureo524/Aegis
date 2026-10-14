using System.Collections;
using System.Linq;
using Features.Augments;
using Features.Characters;
using Features.Combat;
using Features.Economy;
using Features.Enemies;
using Features.Movement;
using Features.Passives;
using Features.Run;
using Features.Spells;
using Features.Stats;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Game.Tests.PlayMode {
    /// <summary>The augments (design doc §9.4) and the passive bricks they need.</summary>
    public class AugmentEffectTests {
        private static IObjectResolver Game => Object.FindFirstObjectByType<Containers.GameLifetimeScope>().Container;
        private static Character Player => Game.Resolve<EnemyDirector>().Player;

        [UnityTearDown]
        public IEnumerator TearDown() {
            Time.timeScale = 1f;
            Object.Destroy(VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().gameObject);
            yield return null;
        }

        private static AugmentDefinition Augment(string id) {
            AugmentDefinition a = Game.Resolve<AugmentCatalog>().Augments.FirstOrDefault(x => x.name == "Augment_" + id);
            Assert.IsNotNull(a, id);
            return a;
        }

        private static Passive Take(string id) => Player.Passives.Add(Augment(id).Passive, "test");

        private static Character Spawn(string asset, Vector3 offset) {
            foreach (WaveDefinition w in Game.Resolve<StageDefinition>().Waves)
                foreach (HordeSpawnEntry e in w.SpawnTable)
                    if (e.Definition.name == asset) {
                        Character c = Game.Resolve<HordeSpawner>().Spawn(e.Prefab, e.Definition, Player.transform.position + offset);
                        c.GetComponent<EnemyAI>().Suspended = true;
                        return c;
                    }
            Assert.Fail(asset);
            return null;
        }

        private static void Invulnerable() => Player.Stats.Get(StatType.MaxHealth).AddModifier(new StatModifier(1e6f, ModifierType.Flat, "test"));

        [UnityTest]
        public IEnumerator Catalog_HasNoPureStatAugment_AndEveryTier() {
            yield return GameFlowSmokeTests.LoadGameScene();
            var augments = Game.Resolve<AugmentCatalog>().Augments;
            foreach (AugmentDefinition a in augments) {
                PassiveDefinition p = a.Passive;
                bool mechanic = p.Triggers.Length > 0 || p.Stacks.Source != StackSource.None || p.Aura.Radius > 0f || p.NearbyEnemies.Radius > 0f
                                || p.Capabilities.Length > 0 || p.Damage.OutgoingPercent != 0f || p.AbilityHaste(0) + p.AbilityHaste(1) + p.AbilityHaste(2) > 0f;
                Assert.IsTrue(mechanic, $"{a.DisplayName}: not only stats");
                Assert.IsNotNull(a.Icon, $"{a.DisplayName}: icon");
            }
            foreach (RarityTier tier in new[] { RarityTier.Silver, RarityTier.Gold, RarityTier.Prismatic })
                Assert.GreaterOrEqual(augments.Count(a => a.Rarity == tier), 10, tier.ToString());
        }

        [UnityTest]
        public IEnumerator CoupDeGrace_MoreDamageOnLowHealthTargets() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Character brute = Spawn("Enemy_Brute", new Vector3(3f, 0f, 0f));
            var hit = new DamageInfo(100f, DamageType.True, false, false, Player);
            float full = brute.TakeDamage(hit);
            Take("CoupDeGrace");
            Assert.AreEqual(full, brute.TakeDamage(hit), 0.01f, "above 50 %: no bonus");
            brute.TakeDamage(new DamageInfo(brute.Health.Current - brute.Health.Max * 0.4f, DamageType.True, false, false, null));
            Assert.AreEqual(full * 1.2f, brute.TakeDamage(hit), 0.01f, "under 50 %: +20 %");
        }

        [UnityTest]
        public IEnumerator FrostGrip_RootsTheEnemiesAround_Periodically() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Invulnerable();
            Take("FrostGrip");
            Character goblin = Spawn("Enemy_Goblin", new Vector3(2.5f, 0f, 0f));
            Time.timeScale = 3f;
            float end = Time.realtimeSinceStartup + 5f;
            while (!goblin.Status.IsRooted && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(goblin.Status.IsRooted, "rooted by the 6.5 s nova");
        }

        [UnityTest]
        public IEnumerator SpellEdge_SpellHitsApplyOnHitEffects() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Take("SpellEdge");
            Character goblin = Spawn("Enemy_Goblin", new Vector3(3f, 0f, 0f));
            int onHits = 0;
            Player.Events.BasicAttackHit += (_, __) => onHits++;
            Player.Events.RaiseSpellHit(goblin, 10f);
            Assert.AreEqual(1, onHits, "the spell hit counts as an on-hit");
            Player.Events.RaiseSpellHit(goblin, 10f);
            Assert.AreEqual(1, onHits, "once per second");
            yield return null;
        }

        [UnityTest]
        public IEnumerator WellPracticed_HalvesTheQCooldown() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Take("WellPracticed");
            var abilities = Player.GetComponent<AbilityCaster>();
            abilities.RankUp(0);
            var options = VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().Container.Resolve<Features.Options.UserOptions>();
            var previous = options.CastMode;
            options.CastMode = Systems.Inputs.CastMode.Quick;
            try {
                abilities.TryCast(0);   // towards nothing: no refund on hit
                Assert.AreEqual(5f * 100f / 200f, abilities.CooldownRemaining(0), 0.1f, "+100 haste on Q");
            }
            finally {
                options.CastMode = previous;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator QuakeStep_ABlinkExplodesAroundTheLandingSpot() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Take("QuakeStep");
            Character goblin = Spawn("Enemy_Goblin", new Vector3(4.5f, 0f, 0f));
            Player.Events.RaiseBlink(Player.transform.position, Player.transform.position);
            Player.GetComponent<CharacterMovement>().Teleport(goblin.transform.position + new Vector3(-1.5f, 0f, 0f));
            yield return null;
            Player.Events.RaiseBlink(Player.transform.position, Player.transform.position);
            Assert.Less(goblin.Health.Current, goblin.Health.Max, "the nova around the landing spot");
        }

        [UnityTest]
        public IEnumerator ArcLightning_DamageJumpsToTheNearestOtherEnemy() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Take("ArcLightning");
            Character a = Spawn("Enemy_Brute", new Vector3(3f, 0f, 0f));
            Character b = Spawn("Enemy_Brute", new Vector3(5f, 0f, 1f));
            a.TakeDamage(new DamageInfo(200f, DamageType.True, false, false, Player));
            float end = Time.time + 2f;
            while (b.Health.Current >= b.Health.Max && Time.time < end) yield return null;
            Assert.AreEqual(b.Health.Max - 60f, b.Health.Current, 1f, "30 % of 200 as true damage");
        }

        [UnityTest]
        public IEnumerator AllOrNothing_LessHealth_MoreDamage() {
            yield return GameFlowSmokeTests.LoadGameScene();
            float hp = Player.Health.Max;
            Character brute = Spawn("Enemy_Brute", new Vector3(3f, 0f, 0f));
            var hit = new DamageInfo(100f, DamageType.True, false, false, Player);
            float before = brute.TakeDamage(hit);
            Take("AllOrNothing");
            Assert.AreEqual(hp * 0.7f, Player.Health.Max, 1f);
            Assert.AreEqual(before * 1.25f, brute.TakeDamage(hit), 0.01f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EternalMoment_TheUltimateMakesYouInvulnerable() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Take("EternalMoment");
            Player.Events.RaiseSpellCast(Player.Definition.Abilities[0]);
            Assert.AreEqual(0f, Player.Health.Shield, "not on a basic ability");
            Player.Events.RaiseSpellCast(Player.Definition.Abilities[AbilitySlots.Ultimate]);
            float hp = Player.Health.Current;
            Player.TakeDamage(new DamageInfo(5000f, DamageType.True, false, false, null));
            Assert.AreEqual(hp, Player.Health.Current, 1e-3f, "invulnerable");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Corrosion_ShredsArmor_StackByStack() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Take("Corrosion");
            Character brute = Spawn("Enemy_Brute", new Vector3(3f, 0f, 0f));
            float armor = brute.Stats.Value(StatType.Armor);
            for (int i = 0; i < 5; i++) Player.Events.RaiseBasicAttackHit(brute, 1f);
            Assert.AreEqual(armor * 0.9f, brute.Stats.Value(StatType.Armor), 0.5f, "5 stacks: −10 %");
            Assert.IsTrue(brute.Status.IsShredded);
            yield return null;
        }
    }
}
