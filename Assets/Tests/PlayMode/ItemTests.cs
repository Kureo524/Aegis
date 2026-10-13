using System.Collections;
using System.Linq;
using Features.Characters;
using Features.Combat;
using Features.Economy;
using Features.Enemies;
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
    /// <summary>The LoL items in the real test scene: adaptive force and the passive bricks they use.</summary>
    public class ItemTests {
        private static IObjectResolver Game => Object.FindFirstObjectByType<Containers.GameLifetimeScope>().Container;
        private static Character Player => Game.Resolve<EnemyDirector>().Player;

        [UnityTearDown]
        public IEnumerator TearDown() {
            Time.timeScale = 1f;
            Object.Destroy(VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().gameObject);
            yield return null;
        }

        private static ItemDefinition Item(string id) {
            ItemDefinition item = Game.Resolve<ShopCatalog>().Items.FirstOrDefault(i => i.name == "Item_" + id);
            Assert.IsNotNull(item, id);
            return item;
        }

        private static void Equip(string id) {
            ItemDefinition item = Item(id);
            foreach (StatBonus b in item.Stats) Player.Stats.Get(b.Stat).AddModifier(b.ToModifier(item));
            Player.Passives.Add(item.Passive, item);
        }

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

        private static IEnumerator Until(System.Func<bool> condition, float seconds = 3f) {
            float end = Time.time + seconds;
            while (!condition() && Time.time < end) yield return null;
        }

        private static void Hit(Character target, float amount = 1f) =>
            Player.Events.RaiseBasicAttackHit(target, target.TakeDamage(new DamageInfo(amount, DamageType.True, false, true, Player)));

        [UnityTest]
        public IEnumerator Catalog_IsLoLItems_PrismaticsHaveAdaptiveForce_EveryItemHasItsOwnPassive() {
            yield return GameFlowSmokeTests.LoadGameScene();
            var items = Game.Resolve<ShopCatalog>().Items;
            Assert.Greater(items.Count, 50);
            Assert.IsTrue(items.All(i => i.Passive != null && !string.IsNullOrEmpty(i.Passive.Description)), "stats + own passive");
            Assert.IsTrue(items.Where(i => i.Tier == ItemTier.Prismatic).All(i => i.Stats.Any(s => s.Stat == StatType.AdaptiveForce)));
            Assert.AreEqual(6, items.Count(i => i.UniqueGroup == "Bottes"));
        }

        [UnityTest]
        public IEnumerator AdaptiveForce_IsAd_ThenAp_WhenBonusApIsHigher() {
            yield return GameFlowSmokeTests.LoadGameScene();
            float ad = Player.Stats.Value(StatType.PhysicalAttack), ap = Player.Stats.Value(StatType.MagicAttack);
            Player.Stats.Get(StatType.AdaptiveForce).AddModifier(new StatModifier(50f, ModifierType.Flat, "test"));
            Assert.AreEqual(ad + 30f, Player.Stats.Value(StatType.PhysicalAttack), 1e-3f, "Ezreal: 50 force = 30 AD");
            Assert.AreEqual(ap, Player.Stats.Value(StatType.MagicAttack), 1e-3f);

            Player.Stats.Get(StatType.MagicAttack).AddModifier(new StatModifier(40f, ModifierType.Flat, "rabadon"));
            Assert.AreEqual(ad, Player.Stats.Value(StatType.PhysicalAttack), 1e-3f, "more bonus AP than AD: it switches");
            Assert.AreEqual(ap + 40f + 50f, Player.Stats.Value(StatType.MagicAttack), 1e-3f, "50 force = 50 AP");
            Assert.IsTrue(Player.AdaptiveForceIsMagic);
        }

        [UnityTest]
        public IEnumerator RunaansHurricane_BoltsHitTheEnemiesAround() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Equip("RunaansHurricane");
            Character target = Spawn("Enemy_Goblin", new Vector3(3f, 0f, 0f));
            Character a = Spawn("Enemy_Goblin", new Vector3(4.5f, 0f, 1.5f));
            Character b = Spawn("Enemy_Goblin", new Vector3(4.5f, 0f, -1.5f));
            Hit(target);
            yield return Until(() => a.Health.Current < a.Health.Max && b.Health.Current < b.Health.Max);
            Assert.Less(a.Health.Current, a.Health.Max, "bolt 1");
            Assert.Less(b.Health.Current, b.Health.Max, "bolt 2");
        }

        [UnityTest]
        public IEnumerator TitanicHydra_CleavesAroundTheTarget() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Equip("TitanicHydra");
            Character target = Spawn("Enemy_Goblin", new Vector3(3f, 0f, 0f));
            Character next = Spawn("Enemy_Goblin", new Vector3(4f, 0f, 0.5f));
            Hit(target);
            Assert.Less(next.Health.Current, next.Health.Max, "the cleave hits the neighbour");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrinityForce_ASpellChargesTheNextAttack() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Equip("TrinityForce");
            Passive spellblade = Player.Passives.All.Last();
            Character target = Spawn("Enemy_Brute", new Vector3(3f, 0f, 0f));
            float hp = target.Health.Current;
            Hit(target);
            Assert.AreEqual(hp - 1f, target.Health.Current, 0.01f, "not charged: just the hit");
            Player.Events.RaiseSpellCast(Player.Definition.Abilities[0]);
            Assert.IsTrue(spellblade.IsCharged);
            hp = target.Health.Current;
            Hit(target);
            Assert.Less(target.Health.Current, hp - 50f, "charged: 200 % AD more");
            Assert.IsFalse(spellblade.IsCharged, "used up");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SunfireAegis_BurnsWhatStandsNextToYou() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Equip("SunfireAegis");
            Character near = Spawn("Enemy_Goblin", new Vector3(1.5f, 0f, 0f));
            Character far = Spawn("Enemy_Goblin", new Vector3(8f, 0f, 0f));
            yield return Until(() => near.Health.Current < near.Health.Max, 2.5f);
            Assert.Less(near.Health.Current, near.Health.Max);
            Assert.AreEqual(far.Health.Max, far.Health.Current, 1e-3f, "out of the aura");
        }

        [UnityTest]
        public IEnumerator Thornmail_HurtsTheAttacker() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Equip("Thornmail");
            Character goblin = Spawn("Enemy_Goblin", new Vector3(2f, 0f, 0f));
            Player.TakeDamage(new DamageInfo(10f, DamageType.Physical, false, true, goblin));
            Assert.Less(goblin.Health.Current, goblin.Health.Max);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArchangelsStaff_TurnsBonusManaIntoAbilityPower() {
            yield return GameFlowSmokeTests.LoadGameScene();
            float ap = Player.Stats.Value(StatType.MagicAttack);
            Equip("ArchangelsStaff");
            yield return new WaitForSeconds(0.4f);   // recomputed a few times per second
            Assert.AreEqual(ap + 70f + 6f, Player.Stats.Value(StatType.MagicAttack), 0.5f, "70 + 1 % of 600 bonus mana");
        }

        [UnityTest]
        public IEnumerator BladeOfTheRuinedKing_HitsForAShareOfCurrentHealth() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Equip("BladeOfTheRuinedKing");
            Character brute = Spawn("Enemy_Brute", new Vector3(3f, 0f, 0f));
            float before = brute.Health.Current;
            Hit(brute);
            float bonus = before - 1f - brute.Health.Current;
            Assert.Greater(bonus, before * 0.06f * 0.5f, "6 % of current HP, minus armor");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Boots_OnlyOnePair() {
            yield return GameFlowSmokeTests.LoadGameScene();
            var inventory = Game.Resolve<Inventory>();
            Assert.IsTrue(inventory.TryAdd(Item("BerserkersGreaves")));
            Assert.IsFalse(inventory.CanAdd(Item("SorcerersShoes")), "one pair of boots");
            Assert.IsTrue(inventory.CanAdd(Item("InfinityEdge")));
        }
    }
}
