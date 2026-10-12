using System.Collections.Generic;
using Features.Combat;
using Features.Stats;
using NUnit.Framework;

namespace Game.Tests.EditMode {
    public class StatusEffectsTests {
        private StatController _stats;
        private TimedModifiers _buffs;
        private List<DamageInfo> _hits;
        private StatusEffects _status;

        [SetUp]
        public void SetUp() {
            _stats = new StatController();
            _stats.Init(null);
            _stats.Get(StatType.MoveSpeed).BaseValue = 5f;
            _buffs = new TimedModifiers();
            _hits = new List<DamageInfo>();
            _status = new StatusEffects(_stats, _buffs, info => _hits.Add(info));
        }

        private static StatusApplication Make(StatusKind kind, float duration, float power = 0f, int stacks = 0) =>
            new() { Kind = kind, Duration = duration, Power = power, MaxStacks = stacks };

        [Test]
        public void Stun_BlocksEverything_ThenWearsOff() {
            _status.Apply(Make(StatusKind.Stun, 1f), null, "a");
            Assert.IsTrue(_status.IsStunned);
            Assert.IsFalse(_status.CanMove);
            Assert.IsFalse(_status.CanAttack);
            Assert.IsFalse(_status.CanCast);
            _status.Tick(1.01f);
            Assert.IsFalse(_status.IsStunned);
            Assert.IsTrue(_status.CanMove && _status.CanAttack && _status.CanCast);
        }

        [Test]
        public void Root_OnlyBlocksMoving_Silence_OnlyBlocksSpells() {
            _status.Apply(Make(StatusKind.Root, 1f), null, "a");
            Assert.IsFalse(_status.CanMove);
            Assert.IsTrue(_status.CanAttack);
            Assert.IsTrue(_status.CanCast);
            _status.Tick(2f);
            _status.Apply(Make(StatusKind.Silence, 1f), null, "a");
            Assert.IsTrue(_status.CanMove);
            Assert.IsFalse(_status.CanCast);
        }

        [Test]
        public void Tenacity_ShortensCrowdControl() {
            _stats.Get(StatType.Tenacity).AddModifier(new StatModifier(0.5f, ModifierType.Flat, "boots"));
            _status.Apply(Make(StatusKind.Stun, 2f), null, "a");
            Assert.AreEqual(1f, _status.Remaining(StatusKind.Stun), 1e-4f);
        }

        [Test]
        public void ShorterStun_DoesNotCutALongerOne() {
            _status.Apply(Make(StatusKind.Stun, 2f), null, "a");
            _status.Apply(Make(StatusKind.Stun, 0.5f), null, "b");
            Assert.AreEqual(2f, _status.Remaining(StatusKind.Stun), 1e-4f);
        }

        [Test]
        public void Slow_ReducesMoveSpeed_WithSlowResist_AndTenacity() {
            _stats.Get(StatType.SlowResist).AddModifier(new StatModifier(0.5f, ModifierType.Flat, "x"));
            _stats.Get(StatType.Tenacity).AddModifier(new StatModifier(0.5f, ModifierType.Flat, "x"));
            _status.Apply(Make(StatusKind.Slow, 2f, power: 0.4f), null, "frost");
            Assert.AreEqual(5f * 0.8f, _stats.Value(StatType.MoveSpeed), 1e-4f, "-40 % halved by the slow resist");
            Assert.IsTrue(_status.IsSlowed);
            _buffs.Tick(1.01f);
            _status.Tick(1.01f);
            Assert.AreEqual(5f, _stats.Value(StatType.MoveSpeed), 1e-4f, "2 s halved by the tenacity");
            Assert.IsFalse(_status.IsSlowed);
        }

        [Test]
        public void Slow_IsMultiplicative_OverSpeedBonuses() {
            _stats.Get(StatType.MoveSpeed).AddModifier(new StatModifier(0.2f, ModifierType.PercentAdd, "boots"));
            _status.Apply(Make(StatusKind.Slow, 2f, power: 0.5f), null, "frost");
            Assert.AreEqual(5f * 1.2f * 0.5f, _stats.Value(StatType.MoveSpeed), 1e-4f);
        }

        [Test]
        public void Burn_TicksEveryHalfSecond_StacksUpToMax_ThenEnds() {
            StatusApplication burn = Make(StatusKind.Burn, 2f, power: 10f, stacks: 2);
            _status.Apply(burn, null, "fire");
            _status.Apply(burn, null, "fire");
            _status.Apply(burn, null, "fire");   // capped at 2
            Assert.AreEqual(2, _status.Stacks(StatusKind.Burn));
            Assert.IsTrue(_status.IsBurning);

            _status.Tick(0.5f);
            Assert.AreEqual(1, _hits.Count);
            Assert.AreEqual(10f * 2 * 0.5f, _hits[0].RawAmount, 1e-4f);
            Assert.AreEqual(DamageType.Magic, _hits[0].Type);

            for (int i = 0; i < 6; i++) _status.Tick(0.5f);
            Assert.AreEqual(4, _hits.Count, "4 ticks over 2 s");
            Assert.IsFalse(_status.IsBurning);
        }

        [Test]
        public void Bleed_IsPhysical_AndDifferentSourcesStackSeparately() {
            _status.Apply(Make(StatusKind.Bleed, 1f, power: 4f), null, "blade");
            _status.Apply(Make(StatusKind.Bleed, 1f, power: 6f), null, "augment");
            Assert.AreEqual(2, _status.Stacks(StatusKind.Bleed));
            _status.Tick(0.5f);
            Assert.AreEqual(2, _hits.Count);
            Assert.AreEqual(DamageType.Physical, _hits[0].Type);
        }

        [Test]
        public void DamageOverTime_IgnoresTenacity() {
            _stats.Get(StatType.Tenacity).AddModifier(new StatModifier(0.9f, ModifierType.Flat, "x"));
            _status.Apply(Make(StatusKind.Bleed, 1f, power: 4f), null, "blade");
            _status.Tick(1f);
            Assert.AreEqual(2, _hits.Count);
        }

        [Test]
        public void Changed_RaisedOnStartAndEnd_Clear_RemovesAll() {
            int changes = 0;
            _status.Changed += () => changes++;
            _status.Apply(Make(StatusKind.Root, 1f), null, "a");
            Assert.AreEqual(1, changes);
            _status.Tick(0.5f);
            Assert.AreEqual(1, changes, "nothing new");
            _status.Apply(Make(StatusKind.Burn, 3f, power: 5f), null, "a");
            _status.Clear();
            Assert.IsFalse(_status.IsRooted || _status.IsBurning);
            Assert.AreEqual(3, changes);
        }
    }
}
