using System.Collections;
using System.Linq;
using Features.Augments;
using Features.Characters;
using Features.Combat;
using Features.Economy;
using Features.Enemies;
using Features.Movement;
using Features.Passives;
using Features.Spells;
using Features.Stats;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Game.Tests.PlayMode {
    /// <summary>Phase 9b in the real test scene: crowd control, damage over time, heals and shields.</summary>
    public class StatusTests {
        private static IObjectResolver Root => VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().Container;
        private static IObjectResolver Game => Object.FindFirstObjectByType<Containers.GameLifetimeScope>().Container;
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

        private static SpellDefinition Spell(string displayName) {
            SpellDefinition spell = Player.Definition.SpellBook.Spells.FirstOrDefault(s => s.DisplayName == displayName);
            Assert.IsNotNull(spell, displayName);
            return spell;
        }

        private static PassiveDefinition Augment(string name) {
            foreach (AugmentDefinition a in Game.Resolve<AugmentCatalog>().Augments) if (a.name == name) return a.Passive;
            Assert.Fail($"{name} not in the catalog");
            return null;
        }

        private static float Flat(Vector3 a, Vector3 b) {
            Vector3 d = a - b;
            d.y = 0f;
            return d.magnitude;
        }

        private static CastContext Ctx(Character target) {
            Vector3 origin = Player.transform.position;
            return new CastContext { Origin = origin, Direction = SpellMath.FlatDirection(origin, target.transform.position, Vector3.forward),
                                     TargetPoint = target.transform.position, TargetUnit = target };
        }

        [UnityTest]
        public IEnumerator Stun_StopsAttacksAndMoves_ThenTheOrderResumes() {
            yield return GameFlowSmokeTests.LoadGameScene();
            var attacker = Player.GetComponent<BasicAttacker>();
            int hits = 0;
            attacker.Hit += (_, __) => hits++;
            Player.ApplyStatus(new StatusApplication { Kind = StatusKind.Stun, Duration = 1f }, null, "test");
            attacker.Attack(Dummy());
            Vector3 start = Player.transform.position;
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(0, hits, "no attack while stunned");
            Assert.Less(Flat(start, Player.transform.position), 0.05f, "no move while stunned");
            Assert.IsFalse(Player.GetComponent<SpellCaster>().Cast(Spell("Boule de feu"), Ctx(Dummy()), 0), "no spell while stunned");

            float end = Time.time + 5f;
            while (hits == 0 && Time.time < end) yield return null;
            Assert.Greater(hits, 0, "the attack order resumes after the stun");
        }

        [UnityTest]
        public IEnumerator Root_KeepsTheMoveOrder_ForAfterwards() {
            yield return GameFlowSmokeTests.LoadGameScene();
            var movement = Player.GetComponent<CharacterMovement>();
            Player.ApplyStatus(new StatusApplication { Kind = StatusKind.Root, Duration = 0.5f }, null, "test");
            Vector3 start = Player.transform.position;
            movement.SetDestination(start + Vector3.right * 3f);
            yield return new WaitForSeconds(0.4f);
            Assert.Less(Flat(start, Player.transform.position), 0.05f, "rooted");
            yield return new WaitForSeconds(0.6f);
            Assert.Greater(Flat(start, Player.transform.position), 0.5f, "walks once the root ends");
        }

        [UnityTest]
        public IEnumerator Spell_AppliesItsStatuses_ToWhatItHits() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Character dummy = Dummy();
            SpellDefinition thunder = Spell("Foudre");   // unit target, stuns
            Assert.IsTrue(thunder.Statuses.Any(s => s.Kind == StatusKind.Stun));
            Game.Resolve<SpellExecutor>().Execute(Player, thunder, Ctx(dummy), 0);
            float end = Time.time + 2f;
            while (!dummy.Status.IsStunned && Time.time < end) yield return null;
            Assert.IsTrue(dummy.Status.IsStunned, "Foudre stuns its target");
        }

        [UnityTest]
        public IEnumerator Burn_DealsDamageOverTime_CreditedToTheCaster() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Character dummy = Dummy();
            float dealt = 0f;
            System.Action<Character, float, DamageInfo> onDamage = (t, d, info) => { if (t == dummy) dealt += d; };
            Player.Events.DamageDealt += onDamage;
            dummy.ApplyStatus(new StatusApplication { Kind = StatusKind.Burn, Duration = 1f, Power = 40f, MaxStacks = 1 }, Player, "test");
            Assert.IsTrue(dummy.Status.IsBurning);
            yield return new WaitForSeconds(1.2f);
            Player.Events.DamageDealt -= onDamage;
            Assert.Greater(dealt, 0f, "ticks hit the dummy");
            Assert.IsFalse(dummy.Status.IsBurning, "then it ends");
        }

        [UnityTest]
        public IEnumerator BuffSpell_GivesASelfShield_ShownWithTheHp() {
            yield return GameFlowSmokeTests.LoadGameScene();
            SpellDefinition overcharge = Spell("Surcharge");
            Assert.Greater(overcharge.SelfSustain.Shield, 0f);
            Game.Resolve<SpellExecutor>().Execute(Player, overcharge, Ctx(Dummy()), 0);
            Assert.Greater(Player.Health.Shield, overcharge.SelfSustain.Shield - 1f, "shield + power ratio");
            float hp = Player.Health.Current;
            Player.TakeDamage(new DamageInfo(20f, DamageType.True, false, false, null));
            Assert.AreEqual(hp, Player.Health.Current, 1e-3f, "the shield takes the hit");
        }

        [UnityTest]
        public IEnumerator GuardianShield_ShieldsUnderThreshold_OncePerCooldown() {
            yield return GameFlowSmokeTests.LoadGameScene();
            ItemDefinition guardian = Game.Resolve<ShopCatalog>().Items.First(i => i.name == "Item_GuardianShield");
            Player.Passives.Add(guardian.Passive, guardian);
            Player.TakeDamage(new DamageInfo(Player.Health.Max * 0.3f, DamageType.True, false, false, null));
            Assert.AreEqual(0f, Player.Health.Shield, "above 35 %: nothing");
            Player.TakeDamage(new DamageInfo(Player.Health.Max * 0.4f, DamageType.True, false, false, null));
            float shield = Player.Health.Shield;
            Assert.Greater(shield, 150f, "under 35 %: the shield");
            Player.TakeDamage(new DamageInfo(1f, DamageType.True, false, false, null));
            Assert.AreEqual(shield - 1f, Player.Health.Shield, 1e-3f, "on cooldown: no second shield");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Bloodletting_BasicAttacksMakeTheTargetBleed() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Player.Passives.Add(Augment("Augment_Bloodletting"), "test");
            Character dummy = Dummy();
            var attacker = Player.GetComponent<BasicAttacker>();
            Time.timeScale = 3f;
            attacker.Attack(dummy);
            float end = Time.realtimeSinceStartup + 10f;
            while (!dummy.Status.IsBleeding && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(dummy.Status.IsBleeding);
            Assert.AreEqual(new Color(1f, 0.5f, 0.5f), dummy.GetComponentInChildren<CharacterVisual>().Renderer.color, "bleed tint");
        }

        [UnityTest]
        public IEnumerator HealShieldPower_ScalesHealsProduced() {
            yield return GameFlowSmokeTests.LoadGameScene();
            Player.TakeDamage(new DamageInfo(300f, DamageType.True, false, false, null));
            float hp = Player.Health.Current;
            Player.Stats.Get(StatType.HealShieldPower).AddModifier(new StatModifier(0.5f, ModifierType.Flat, "test"));
            Player.ReceiveHeal(100f, Player);
            Assert.AreEqual(hp + 150f, Player.Health.Current, 1f);
        }
    }
}
