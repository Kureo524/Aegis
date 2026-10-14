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
        private readonly List<(Stat stat, StatModifier modifier)> _conversionModifiers = new();
        private readonly List<Character> _around = new();
        private float _chargedUntil = -1f;
        private float _nextAura;
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
        /// <summary>Charged by a Charge effect and not used yet (Spellblade ready: UI).</summary>
        public bool IsCharged => Time.time < _chargedUntil;

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
            events.DamageDealt += OnDamageDealt;
            events.Blink += OnBlink;
            _nextAura = Time.time + Mathf.Max(0.1f, Definition.Aura.Interval);
            for (int i = 0; i < _readyAt.Length; i++)   // periodic triggers wait one interval first
                if (Definition.Triggers[i].On == PassiveEvent.Periodic) _readyAt[i] = Time.time + Mathf.Max(0.5f, Definition.Triggers[i].Cooldown);
        }

        public void Detach() {
            RemoveAll(_modifiers);
            RemoveAll(_stackModifiers);
            RemoveAll(_nearbyModifiers);
            RemoveAll(_conversionModifiers);
            foreach (Capability capability in Definition.Capabilities) _owner.Capabilities.Revoke(capability, this);
            CharacterEvents events = _owner.Events;
            events.BasicAttackHit -= OnBasicAttackHit;
            events.SpellCast -= OnSpellCast;
            events.Kill -= OnKill;
            events.DamageTaken -= OnDamageTaken;
            events.SpellHit -= OnSpellHit;
            events.DamageDealt -= OnDamageDealt;
            events.Blink -= OnBlink;
        }

        public void Tick() {
            StackSettings s = Definition.Stacks;
            if (Stacks > 0 && s.Reset == ResetPolicy.AfterDuration && Time.time - _lastStackTime >= s.Duration) ResetStacks();

            NearbyEnemiesBonus nearby = Definition.NearbyEnemies;
            bool slowTick = Time.time >= _nextNearby;
            if (slowTick) _nextNearby = Time.time + NearbyInterval;
            if (nearby.Radius > 0f && slowTick) UpdateNearby(nearby);
            if (slowTick && Definition.Conversions.Length > 0) UpdateConversions();

            PassiveTrigger[] triggers = Definition.Triggers;
            for (int i = 0; i < triggers.Length; i++) {
                if (triggers[i].On != PassiveEvent.Periodic || Time.time < _readyAt[i]) continue;
                _readyAt[i] = Time.time + Mathf.Max(0.5f, triggers[i].Cooldown);
                Apply(triggers[i], null);
            }

            AuraSettings aura = Definition.Aura;
            if (aura.Radius > 0f && Time.time >= _nextAura) {
                _nextAura = Time.time + Mathf.Max(0.1f, aura.Interval);
                PulseAura(aura);
            }
        }

        // Sunfire-like aura: damage around the owner, optional heal per enemy hit
        private void PulseAura(in AuraSettings aura) {
            float raw = aura.Damage + aura.Ratio * _owner.Stats.Value(aura.ScalingStat);
            int hit = 0;
            foreach (Character enemy in EnemiesAround(_owner.transform.position, aura.Radius, null)) {
                if (raw > 0f) enemy.TakeDamage(Hit(raw, aura.DamageType));
                hit++;
            }
            if (hit > 0 && aura.HealPerEnemy > 0f) _owner.ReceiveHeal(aura.HealPerEnemy * hit, _owner);
            if (hit > 0) _owner.PlayRing(_owner.transform.position, aura.Radius, new Color(1f, 0.55f, 0.25f, 0.35f));
        }

        private void UpdateConversions() {
            RemoveAll(_conversionModifiers);
            foreach (StatConversion c in Definition.Conversions) {
                if (c.From == c.To) continue;
                float from = c.BonusOnly ? _owner.BonusStat(c.From) : _owner.Stats.Value(c.From);
                float value = Mathf.Max(0f, from) * c.Ratio;
                Stat to = _owner.Stats.Get(c.To);
                if (to == null || value == 0f) continue;
                var modifier = new StatModifier(value, ModifierType.Flat, Source);
                to.AddModifier(modifier);
                _conversionModifiers.Add((to, modifier));
            }
        }

        private DamageInfo Hit(float raw, DamageType type) {
            StatController s = _owner.Stats;
            return new DamageInfo(raw, type, false, false, _owner,
                s.Value(StatType.ArmorPenFlat), s.Value(StatType.ArmorPenPercent),
                s.Value(StatType.MagicPenFlat), s.Value(StatType.MagicPenPercent));
        }

        /// <summary>Outgoing damage multiplier of this passive on <paramref name="target"/> (DamageModifiers).</summary>
        public float OutgoingMultiplier(Character target, in DamageInfo info) {
            DamageModifiers d = Definition.Damage;
            if (d.OutgoingPercent == 0f) return 1f;
            if (d.AppliesTo == DamageScope.Abilities && info.IsBasicAttack) return 1f;
            if (d.AppliesTo == DamageScope.BasicAttacks && !info.IsBasicAttack) return 1f;
            if (d.TargetHealthBelow > 0f && (target.Health == null || target.Health.Ratio > d.TargetHealthBelow)) return 1f;
            return 1f + d.OutgoingPercent;
        }

        public float IncomingMultiplier => 1f + Definition.Damage.IncomingPercent;

        // Base + stat ratio + shares of the target's HP (BotRK, Liandry…) + share of the triggering damage
        private float _triggerDamage;

        private float RawDamage(in PassiveTrigger t, Character target) {
            float raw = t.Damage + t.DamageRatio * _owner.Stats.Value(t.ScalingStat) + t.DamageDealtRatio * _triggerDamage;
            if (target != null && target.Health != null)
                raw += t.TargetMaxHealthRatio * target.Health.Max + t.TargetCurrentHealthRatio * target.Health.Current;
            return raw;
        }

        // Living enemies of the owner within radius of center, nearest first, except one
        private List<Character> EnemiesAround(Vector3 center, float radius, Character except) {
            _around.Clear();
            int count = Physics.OverlapSphereNonAlloc(center, radius + 1f, _hits);
            for (int i = 0; i < count; i++) {
                Character c = _hits[i].GetComponentInParent<Character>();
                if (c == null || c == except || !c.IsAlive || !_owner.IsHostileTo(c) || _around.Contains(c)) continue;
                Vector3 d = c.transform.position - center;
                d.y = 0f;
                if (d.magnitude - c.BodyRadius <= radius) _around.Add(c);
            }
            _around.Sort((a, b) => (a.transform.position - center).sqrMagnitude.CompareTo((b.transform.position - center).sqrMagnitude));
            return _around;
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
            _castSpell = spell;
            Fire(PassiveEvent.SpellCast, null);
            _castSpell = null;
        }

        private void OnKill(Character victim) {
            if (Definition.Stacks.Source == StackSource.PerKill) AddStack();
            Fire(PassiveEvent.Kill, victim);
        }

        private void OnDamageTaken(float damage, Character attacker) => Fire(PassiveEvent.DamageTaken, attacker);

        private void OnSpellHit(Character target, float damage) {
            if (Definition.Stacks.Source == StackSource.PerSpellHit) AddStack();
            Fire(PassiveEvent.SpellHit, target);
        }

        private void OnDamageDealt(Character target, float damage, DamageInfo info) {
            if (info.IsCrit && info.IsBasicAttack && Definition.Stacks.Source == StackSource.PerCrit) AddStack();
            _triggerDamage = damage;
            Fire(PassiveEvent.DamageDealt, target);
            _triggerDamage = 0f;
        }

        private void OnBlink(Vector3 from, Vector3 to) => Fire(PassiveEvent.Blink, null);

        private SpellDefinition _castSpell;

        private void Fire(PassiveEvent happened, Character target) {
            PassiveTrigger[] triggers = Definition.Triggers;
            bool consumed = false;   // every charged trigger of this event goes off, then the charge is used
            for (int i = 0; i < triggers.Length; i++) {
                PassiveTrigger t = triggers[i];
                if (t.On != happened || happened == PassiveEvent.Periodic) continue;
                if (t.UltimateOnly && !IsUltimate(_castSpell)) continue;
                if (t.HealthBelow > 0f && (_owner.Health == null || _owner.Health.Ratio > t.HealthBelow)) continue;
                if (Time.time < _readyAt[i]) continue;
                if (t.ConsumesCharge && Time.time >= _chargedUntil) continue;
                if (++_counters[i] < Mathf.Max(1, t.EveryN)) continue;
                _counters[i] = 0;
                if (UnityEngine.Random.value > t.Chance) continue;
                if (t.Cooldown > 0f) _readyAt[i] = Time.time + t.Cooldown;
                consumed |= t.ConsumesCharge;
                Apply(t, target);
            }
            if (consumed) _chargedUntil = -1f;
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
                    target.TakeDamage(Hit(RawDamage(t, target), t.DamageType));
                    break;
                case PassiveEffectKind.Bolts: {
                    Vector3 center = target != null ? target.transform.position : _owner.transform.position;
                    int fired = 0;
                    foreach (Character enemy in EnemiesAround(center, t.Radius, target).ToArray()) {
                        if (fired++ >= Mathf.Max(1, t.Count)) break;
                        _owner.FireBolt(enemy, Hit(RawDamage(t, enemy), t.DamageType), t.BoltColor.a > 0f ? t.BoltColor : Color.white);
                    }
                    break;
                }
                case PassiveEffectKind.Splash:
                    if (target == null) break;
                    foreach (Character enemy in EnemiesAround(target.transform.position, t.Radius, target).ToArray())
                        enemy.TakeDamage(Hit(RawDamage(t, enemy), t.DamageType));
                    break;
                case PassiveEffectKind.ReduceCooldowns:
                    _owner.Events.RaiseCooldownRefund(t.Amount);
                    break;
                case PassiveEffectKind.Charge:
                    _chargedUntil = Time.time + t.Duration;
                    break;
                case PassiveEffectKind.Nova: {
                    Vector3 center = _owner.transform.position;
                    foreach (Character enemy in EnemiesAround(center, t.Radius, null).ToArray()) {
                        float raw = RawDamage(t, enemy);
                        if (raw > 0f) enemy.TakeDamage(Hit(raw, t.DamageType));
                        if (t.Status.Duration > 0f) enemy.ApplyStatus(t.Status, _owner, this);
                    }
                    _owner.PlayRing(center, t.Radius, t.BoltColor.a > 0f ? t.BoltColor : new Color(1f, 0.9f, 0.5f, 0.8f));
                    break;
                }
                case PassiveEffectKind.ProcOnHit:
                    if (target != null && target.IsAlive) _owner.Events.RaiseBasicAttackHit(target, 0f);
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

        // The ultimate is the 4th ability of the owner's definition
        private bool IsUltimate(SpellDefinition spell) =>
            spell != null && _owner.Definition != null && _owner.Definition.Abilities.Count > Spells.AbilitySlots.Ultimate
            && _owner.Definition.Abilities[Spells.AbilitySlots.Ultimate] == spell;

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
