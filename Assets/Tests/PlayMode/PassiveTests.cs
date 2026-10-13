using System.Collections;
using Features.Augments;
using Features.Characters;
using Features.Combat;
using Features.Economy;
using Features.Enemies;
using Features.Events;
using Features.Passives;
using Features.Run;
using Features.Spells;
using Features.Stats;
using NUnit.Framework;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Game.Tests.PlayMode {
    /// <summary>Phase 8 in the real test scene: passives, capabilities, loot.</summary>
    public class PassiveTests {
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

        private static Character Dummy() {
            Character best = null;
            foreach (var c in Object.FindObjectsByType<Character>(FindObjectsSortMode.None))
                if (c != Player && c.IsAlive && (best == null || Dist(c) < Dist(best))) best = c;
            return best;
        }

        private static float Dist(Character c) => (c.transform.position - Player.transform.position).sqrMagnitude;

        private static PassiveDefinition Augment(string name) {
            foreach (AugmentDefinition a in Game.Resolve<AugmentCatalog>().Augments) if (a.name == name) return a.Passive;
            Assert.Fail($"{name} not in the catalog");
            return null;
        }

        private static IEnumerator Hits(BasicAttacker attacker, int count) {
            int hits = 0;
            System.Action<Character, float> onHit = (_, __) => hits++;
            attacker.Hit += onHit;
            float end = Time.realtimeSinceStartup + 15f;
            while (hits < count && Time.realtimeSinceStartup < end) yield return null;
            attacker.Hit -= onHit;
            Assert.GreaterOrEqual(hits, count, "basic attacks landed");
        }

        [UnityTest]
        public IEnumerator EzrealPassive_SpellHitsStackAttackSpeed() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Assert.IsNotNull(Player.Definition.Passive, "Ezreal has his passive");
            Passive passive = null;
            foreach (Passive p in Player.Passives.All) if (p.Definition == Player.Definition.Passive) passive = p;
            Assert.IsNotNull(passive);
            float speed = Player.Stats.Value(StatType.AttackSpeed);
            Character dummy = Dummy();
            SpellDefinition q = Player.Definition.Abilities[0];
            Game.Resolve<SpellExecutor>().Execute(Player, q, Ctx(dummy), 1);
            float end = Time.time + 2f;
            while (passive.Stacks == 0 && Time.time < end) yield return null;
            Assert.AreEqual(1, passive.Stacks, "one stack per spell hit");
            Assert.Greater(Player.Stats.Value(StatType.AttackSpeed), speed * 1.05f, "+10 % attack speed");
        }

        [UnityTest]
        public IEnumerator Frenzy_StacksAttackSpeed_ThenFallsOff() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Passive frenzy = Player.Passives.Add(Augment("Augment_Frenzy"), "test");
            float speed = Player.Stats.Value(StatType.AttackSpeed);
            var attacker = Player.GetComponent<BasicAttacker>();
            Time.timeScale = 4f;
            attacker.Attack(Dummy());
            yield return Hits(attacker, 2);
            Assert.GreaterOrEqual(frenzy.Stacks, 2);
            Assert.Greater(Player.Stats.Value(StatType.AttackSpeed), speed);
            attacker.Cancel();
            yield return new WaitForSeconds(3.5f);   // scaled: well over the 3 s
            Assert.AreEqual(0, frenzy.Stacks, "stacks fall off");
            Assert.AreEqual(speed, Player.Stats.Value(StatType.AttackSpeed), 1e-3f);
        }

        [UnityTest]
        public IEnumerator SpellCrit_LetsSpellsCrit() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Player.Stats.Get(StatType.CritChance).AddModifier(new StatModifier(1f, ModifierType.Flat, "test"));
            var executor = Game.Resolve<SpellExecutor>();
            SpellDefinition fireball = Player.Definition.Abilities[0];   // Ezreal Q
            Character dummy = Dummy();

            bool crit = false;
            using (Bus.Subscribe<DamageDealtEvent>(e => crit |= e.IsCrit && !e.TargetIsPlayer)) {
                executor.Execute(Player, fireball, Ctx(dummy), 1);
                yield return new WaitForSeconds(1f);
            }
            Assert.IsFalse(crit, "no crit without the capability, even at 100 % crit chance");

            Player.Passives.Add(Augment("Augment_JeweledGauntlet"), "test");
            using (Bus.Subscribe<DamageDealtEvent>(e => crit |= e.IsCrit && !e.TargetIsPlayer)) {
                executor.Execute(Player, fireball, Ctx(dummy), 1);
                yield return new WaitForSeconds(1f);
            }
            Assert.IsTrue(crit, "SpellCrit: the spell crits");
        }

        private static CastContext Ctx(Character target) {
            Vector3 origin = Player.transform.position;
            return new CastContext { Origin = origin, Direction = SpellMath.FlatDirection(origin, target.transform.position, Vector3.forward),
                                     TargetPoint = target.transform.position, TargetUnit = target };
        }

        [UnityTest]
        public IEnumerator SummonerSpellLoot_PickedOnContact_FillsD_OnlyOneAtATime() {
            yield return GameFlowSmokeTests.LoadGameScene();
            var abilities = Player.GetComponent<AbilityCaster>();
            Assert.IsNull(abilities.Spell(AbilitySlots.SummonerD), "D starts empty");
            Bus.Publish(new LootDropRequestedEvent(LootKind.SummonerSpell, Player.transform.position));
            yield return null;
            yield return null;
            Assert.IsTrue(abilities.HoldsConsumable, "picked up on contact");
            Assert.IsNotNull(abilities.Spell(AbilitySlots.SummonerD));

            Bus.Publish(new LootDropRequestedEvent(LootKind.SummonerSpell, Player.transform.position));
            yield return null;
            yield return null;
            Assert.AreEqual(1, Game.Resolve<Features.Loot.LootService>().ActiveCount, "a second one stays on the ground");
        }

        [UnityTest]
        public IEnumerator SummonerSpell_UsedOnce_ThenDIsEmptyAgain_FlashUntouched() {
            yield return GameFlowSmokeTests.LoadGameScene();
            var abilities = Player.GetComponent<AbilityCaster>();
            SpellDefinition ghost = System.Array.Find(Game.Resolve<Features.Loot.LootSettings>().SummonerSpells, s => s.Targeting == TargetingType.Self);
            Assert.IsNotNull(ghost, "a Self summoner spell (Ghost) in the loot settings");
            float speed = Player.Stats.Value(StatType.MoveSpeed);

            Assert.IsTrue(abilities.PickUp(ghost));
            Assert.IsTrue(abilities.TryCast(AbilitySlots.SummonerD));   // Self: cast at once
            yield return null;
            Assert.Greater(Player.Stats.Value(StatType.MoveSpeed), speed, "Ghost: faster");
            Assert.IsFalse(abilities.HoldsConsumable, "used up");
            Assert.IsNull(abilities.Spell(AbilitySlots.SummonerD), "D is empty again");
            Assert.AreEqual(0f, abilities.CooldownRemaining(AbilitySlots.SummonerF), "Flash untouched");
        }

        [UnityTest]
        public IEnumerator StatAnvilLoot_OpensAtTheEndOfTheWave_BeforeTheScheduledReward() {
            yield return SceneManager.LoadSceneAsync("TestScene", LoadSceneMode.Single);
            yield return null;
            var run = Game.Resolve<RunDirector>();
            var shop = Game.Resolve<ShopService>();
            Bus.Publish(new SkipWaveRequestedEvent());
            yield return null;
            Assert.AreEqual(RunState.Combat, run.State);

            Bus.Publish(new LootDropRequestedEvent(LootKind.StatAnvil, Player.transform.position));
            yield return null;
            yield return null;
            Assert.IsFalse(shop.AnvilOpen, "not during the wave");

            Bus.Publish(new SkipWaveRequestedEvent());
            Assert.IsTrue(shop.AnvilOpen, "end of the wave: the picked-up anvil first");
            Assert.IsInstanceOf<StatChoiceDefinition>(shop.AnvilChoices[0]);
            shop.Choose(0);
            Assert.AreEqual(AugmentDraftPhase.Choose, Game.Resolve<AugmentController>().Phase, "then the scheduled augment");
        }
    }
}
