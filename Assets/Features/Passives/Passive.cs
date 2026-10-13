using System;
using System.Collections.Generic;
using Features.Characters;
using Features.Combat;
using Features.Economy;
using Features.Spells;
using Features.Stats;
using UnityEngine;

namespace Features.Passives {
    /// <summary>
    /// Runtime of a <see cref="PassiveDefinition"/> on one character (design doc §8.1). Every effect
    /// goes through the existing systems: StatModifiers with this instance as Source, capabilities,
    /// TimedModifiers, DamageInfo. Loot drops are requested through <see cref="LootDropped"/>.
    /// </summary>
    public sealed class Passive {
        private const float NearbyInterval = 0.25f;

        private readonly Character _owner;
        private readonly List<(Stat stat, StatModifier modifier)> _modifiers = new();
        private readonly List<(Stat stat, StatModifier modifier)> _stackModifiers = new();
        private readonly List<(Stat stat, StatModifier modifier)> _nearbyModifiers = new();
        private readonly int[] _counters;
        private readonly float[] _readyAt;
        private readonly Collider[] _hits = new Collider[64];
        private readonly HashSet<Character> _seen = new();
        private float _lastStackTime;
        private float _nextNearby;
        private int _nearbyCount = -1;

        public PassiveDefinition Definition { get; }
        public object Source { get; }
        public int Stacks { get; private set; }

        /// <summary>A trigger dropped a loot (kind, where).</summary>
        public event Action<LootKind, Vector3> LootDropped;
        /// <summary>Stacks changed (UI).</summary>
        public event Action<Passive> Changed;

        public Passive(PassiveDefinition definition, Character owner, object source) {
            Definition = definition;
            _owner = owner;
            Source = source ?? this;
            _counters = new int[definition.Triggers.Length];
            _readyAt = new float[definition.Triggers.Length];
        }

        public void Attach() {
            foreach (StatBonus bonus in Definition.Stats) AddModifier(_modifiers, bonus, 1f);
            foreach (Capability capability in Definition.Capabilities) _owner.Capabilities.Grant(capability, this);
            CharacterEvents events = _owner.Events;
            events.BasicAttackHit += OnBasicAttackHit;
            events.SpellCast += OnSpellCast;
            events.Kill += OnKill;
            events.DamageTaken += OnDamageTaken;
            events.SpellHit += OnSpellHit;
        }

        public void Detach() {
            RemoveAll(_modifiers);
            RemoveAll(_stackModifiers);
            RemoveAll(_nearbyModifiers);
            foreach (Capability capability in Definition.Capabilities) _owner.Capabilities.Revoke(capability, this);
            CharacterEvents events = _owner.Events;
            events.BasicAttackHit -= OnBasicAttackHit;
            events.SpellCast -= OnSpellCast;
            events.Kill -= OnKill;
            events.DamageTaken -= OnDamageTaken;
            events.SpellHit -= OnSpellHit;
        }

        public void Tick() {
            StackSettings s = Definition.Stacks;
            if (Stacks > 0 && s.Reset == ResetPolicy.AfterDuration && Time.time - _lastStackTime >= s.Duration) ResetStacks();

            NearbyEnemiesBonus nearby = Definition.NearbyEnemies;
            if (nearby.Radius > 0f && Time.time >= _nextNearby) {
                _nextNearby = Time.time + NearbyInterval;
                UpdateNearby(nearby);
            }
        }

        /// <summary>End of a wave: EndOfWave stacks fall off.</summary>
        public void OnWaveEnded() {
            if (Definition.Stacks.Reset == ResetPolicy.EndOfWave) ResetStacks();
        }

        private void OnBasicAttackHit(Character target, float damage) {
            if (Definition.Stacks.Source == StackSource.PerBasicAttack) AddStack();
            Fire(PassiveEvent.BasicAttackHit, target);
        }

        private void OnSpellCast(SpellDefinition spell) {
            if (Definition.Stacks.Source == StackSource.PerSpellCast) AddStack();
            Fire(PassiveEvent.SpellCast, null);
        }

        private void OnKill(Character victim) {
            if (Definition.Stacks.Source == StackSource.PerKill) AddStack();
            Fire(PassiveEvent.Kill, victim);
        }

        private void OnDamageTaken(float damage) => Fire(PassiveEvent.DamageTaken, null);

        private void OnSpellHit(Character target, float damage) {
            if (Definition.Stacks.Source == StackSource.PerSpellHit) AddStack();
            Fire(PassiveEvent.SpellHit, target);
        }

        private void Fire(PassiveEvent happened, Character target) {
            PassiveTrigger[] triggers = Definition.Triggers;
            for (int i = 0; i < triggers.Length; i++) {
                PassiveTrigger t = triggers[i];
                if (t.On != happened) continue;
                if (t.HealthBelow > 0f && (_owner.Health == null || _owner.Health.Ratio > t.HealthBelow)) continue;
                if (Time.time < _readyAt[i]) continue;
                if (++_counters[i] < Mathf.Max(1, t.EveryN)) continue;
                _counters[i] = 0;
                if (UnityEngine.Random.value > t.Chance) continue;
                if (t.Cooldown > 0f) _readyAt[i] = Time.time + t.Cooldown;
                Apply(t, target);
            }
        }

        private void Apply(in PassiveTrigger t, Character target) {
            switch (t.Effect) {
                case PassiveEffectKind.TimedStats:
                    if (t.Stats == null) break;
                    foreach (StatBonus bonus in t.Stats) {
                        Stat stat = _owner.Stats.Get(bonus.Stat);
                        if (stat != null) _owner.Buffs.Apply(stat, bonus.Value, bonus.Type, this, t.Duration);
                    }
                    break;
                case PassiveEffectKind.BonusDamage:
                    if (target == null || !target.IsAlive) break;
                    float raw = t.Damage + t.DamageRatio * _owner.Stats.Value(t.ScalingStat);
                    StatController s = _owner.Stats;
                    target.TakeDamage(new DamageInfo(raw, t.DamageType, false, false, _owner,
                        s.Value(StatType.ArmorPenFlat), s.Value(StatType.ArmorPenPercent),
                        s.Value(StatType.MagicPenFlat), s.Value(StatType.MagicPenPercent)));
                    break;
                case PassiveEffectKind.DropLoot:
                    LootDropped?.Invoke(t.Loot, target != null ? target.transform.position : _owner.transform.position);
                    break;
                case PassiveEffectKind.ApplyStatus:
                    if (target != null && target.IsAlive && _owner.IsHostileTo(target)) target.ApplyStatus(t.Status, _owner, this);
                    break;
                case PassiveEffectKind.Heal:
                    _owner.ReceiveHeal(Amount(t), _owner);
                    break;
                case PassiveEffectKind.Shield:
                    _owner.ReceiveShield(Amount(t), t.Duration, _owner);
                    break;
            }
        }

        private float Amount(in PassiveTrigger t) => t.Amount + t.AmountRatio * _owner.Stats.Value(t.AmountStat);

        private void AddStack() {
            StackSettings s = Definition.Stacks;
            _lastStackTime = Time.time;
            if (s.Max > 0 && Stacks >= s.Max) return;
            Stacks++;
            ApplyStacks();
        }

        private void ResetStacks() {
            if (Stacks == 0) return;
            Stacks = 0;
            ApplyStacks();
        }

        private void ApplyStacks() {
            RemoveAll(_stackModifiers);
            if (Stacks > 0 && Definition.Stacks.PerStack != null)
                foreach (StatBonus bonus in Definition.Stacks.PerStack) AddModifier(_stackModifiers, bonus, Stacks);
            Changed?.Invoke(this);
        }

        private void UpdateNearby(in NearbyEnemiesBonus nearby) {
            int count = Physics.OverlapSphereNonAlloc(_owner.transform.position, nearby.Radius, _hits);
            _seen.Clear();
            for (int i = 0; i < count; i++) {
                Character c = _hits[i].GetComponentInParent<Character>();
                if (c != null && c.IsAlive && _owner.IsHostileTo(c)) _seen.Add(c);
            }
            int enemies = nearby.Max > 0 ? Mathf.Min(_seen.Count, nearby.Max) : _seen.Count;
            if (enemies == _nearbyCount) return;
            _nearbyCount = enemies;
            RemoveAll(_nearbyModifiers);
            if (enemies > 0 && nearby.PerEnemy != null)
                foreach (StatBonus bonus in nearby.PerEnemy) AddModifier(_nearbyModifiers, bonus, enemies);
            Changed?.Invoke(this);
        }

        /// <summary>Current value of the nearby-enemies bonus (UI).</summary>
        public int NearbyEnemies => Mathf.Max(0, _nearbyCount);

        private void AddModifier(List<(Stat, StatModifier)> into, StatBonus bonus, float times) {
            Stat stat = _owner.Stats.Get(bonus.Stat);
            if (stat == null) return;
            var modifier = new StatModifier(bonus.Value * times, bonus.Type, Source);
            stat.AddModifier(modifier);
            into.Add((stat, modifier));
        }

        private static void RemoveAll(List<(Stat stat, StatModifier modifier)> list) {
            foreach ((Stat stat, StatModifier modifier) in list) stat.RemoveModifier(modifier);
            list.Clear();
        }
    }
}
