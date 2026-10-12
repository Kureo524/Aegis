using System;
using System.Collections.Generic;
using Features.Characters;
using Features.Stats;
using UnityEngine;

namespace Features.Combat {
    /// <summary>Crowd control and damage over time (design doc §5.7).</summary>
    public enum StatusKind {
        /// <summary>No move, no attack, no spell. Interrupts a windup or a cast.</summary>
        Stun,
        /// <summary>No move (attacks and spells allowed, Flash too).</summary>
        Root,
        /// <summary>No spell.</summary>
        Silence,
        /// <summary>Move speed × (1 − Power × (1 − SlowResist)).</summary>
        Slow,
        /// <summary>Physical damage over time, stacking.</summary>
        Bleed,
        /// <summary>Magic damage over time, stacking.</summary>
        Burn,
    }

    /// <summary>One status as written in the data (spells, passives, enemy attacks).</summary>
    [Serializable]
    public struct StatusApplication {
        public StatusKind Kind;
        [Tooltip("Seconds. CC and slows are shortened by the target's Tenacity, damage over time is not.")]
        [Min(0f)] public float Duration;
        [Tooltip("Slow: share of move speed removed (0.3 = −30 %). Bleed / Burn: damage per second per stack.")]
        public float Power;
        [Tooltip("Bleed / Burn: + Ratio × the applier's ScalingStat, per second per stack.")]
        public StatType ScalingStat;
        public float Ratio;
        [Tooltip("Bleed / Burn: maximum stacks (each application adds one and refreshes the duration). 0 = 1.")]
        [Min(0)] public int MaxStacks;

        public bool IsCrowdControl => Kind is StatusKind.Stun or StatusKind.Root or StatusKind.Silence;
        public bool IsDamageOverTime => Kind is StatusKind.Bleed or StatusKind.Burn;
    }

    /// <summary>
    /// The statuses currently on a character (design doc §5.7). Pure C#, owned by Character and
    /// ticked every frame with it.
    /// - Stun / Root / Silence: duration × (1 − Tenacity); a new one only lengthens the current one.
    /// - Slow: a PercentMult modifier on MoveSpeed through TimedModifiers (same source = refresh),
    ///   duration × (1 − Tenacity), strength × (1 − SlowResist).
    /// - Bleed / Burn: one entry per (kind, source); each application adds a stack (up to MaxStacks)
    ///   and refreshes the duration. Damage per second is snapshotted from the applier's stats and
    ///   dealt every <see cref="TickInterval"/>s through the usual damage pipeline.
    /// </summary>
    public sealed class StatusEffects {
        public const float TickInterval = 0.5f;

        private sealed class DamageOverTime {
            public StatusKind Kind;
            public object Source;
            public Character Applier;
            public float DamagePerSecond, Remaining, NextTick;
            public int Stacks;
        }

        private readonly StatController _stats;
        private readonly TimedModifiers _buffs;
        private readonly Action<DamageInfo> _takeDamage;
        private readonly float[] _remaining = new float[Enum.GetValues(typeof(StatusKind)).Length];
        private readonly List<DamageOverTime> _dots = new();
        private bool _wasStunned, _wasRooted, _wasSilenced, _wasSlowed, _wasBleeding, _wasBurning;

        /// <param name="takeDamage">How damage over time reaches the owner (Character.TakeDamage).</param>
        public StatusEffects(StatController stats, TimedModifiers buffs, Action<DamageInfo> takeDamage) {
            _stats = stats;
            _buffs = buffs;
            _takeDamage = takeDamage;
        }

        public bool IsStunned => Remaining(StatusKind.Stun) > 0f;
        public bool IsRooted => Remaining(StatusKind.Root) > 0f;
        public bool IsSilenced => Remaining(StatusKind.Silence) > 0f;
        public bool IsSlowed => Remaining(StatusKind.Slow) > 0f;
        public bool IsBleeding => HasDot(StatusKind.Bleed);
        public bool IsBurning => HasDot(StatusKind.Burn);
        public bool CanMove => !IsStunned && !IsRooted;
        public bool CanAttack => !IsStunned;
        public bool CanCast => !IsStunned && !IsSilenced;

        /// <summary>A status started or ended (visuals, interruptions).</summary>
        public event Action Changed;

        public float Remaining(StatusKind kind) => _remaining[(int)kind];

        public int Stacks(StatusKind kind) {
            int total = 0;
            foreach (DamageOverTime d in _dots) if (d.Kind == kind) total += d.Stacks;
            return total;
        }

        /// <param name="applier">Who applies it (damage over time scales on its stats and credits it); may be null.</param>
        /// <param name="source">Identity of the effect (spell, passive): the same source refreshes instead of stacking.</param>
        public void Apply(in StatusApplication status, Character applier, object source) {
            if (status.Duration <= 0f) return;
            float tenacity = Mathf.Clamp01(_stats.Value(StatType.Tenacity));
            switch (status.Kind) {
                case StatusKind.Stun:
                case StatusKind.Root:
                case StatusKind.Silence:
                    Lengthen(status.Kind, status.Duration * (1f - tenacity));
                    break;
                case StatusKind.Slow: {
                    float duration = status.Duration * (1f - tenacity);
                    float strength = Mathf.Clamp01(status.Power) * (1f - Mathf.Clamp01(_stats.Value(StatType.SlowResist)));
                    if (strength <= 0f || duration <= 0f) break;
                    _buffs.Apply(_stats.Get(StatType.MoveSpeed), -strength, ModifierType.PercentMult, source ?? this, duration);
                    Lengthen(StatusKind.Slow, duration);
                    break;
                }
                case StatusKind.Bleed:
                case StatusKind.Burn:
                    ApplyDot(status, applier, source);
                    break;
            }
            NotifyIfChanged();
        }

        public void Tick(float deltaTime) {
            for (int i = 0; i < _remaining.Length; i++) _remaining[i] = Mathf.Max(0f, _remaining[i] - deltaTime);

            for (int i = _dots.Count - 1; i >= 0; i--) {
                DamageOverTime d = _dots[i];
                d.Remaining -= deltaTime;
                d.NextTick -= deltaTime;
                while (d.NextTick <= 0f && d.Remaining > -TickInterval) {
                    d.NextTick += TickInterval;
                    DealTick(d);
                    if (_dots.Count == 0) return;   // the tick killed the owner: everything was cleared
                }
                if (d.Remaining <= 0f) _dots.Remove(d);
                if (i > _dots.Count) i = _dots.Count;
            }
            NotifyIfChanged();
        }

        /// <summary>Everything off (death, new Init). Slows are removed with the TimedModifiers.</summary>
        public void Clear() {
            Array.Clear(_remaining, 0, _remaining.Length);
            _dots.Clear();
            NotifyIfChanged();
        }

        private void Lengthen(StatusKind kind, float duration) {
            if (duration > _remaining[(int)kind]) _remaining[(int)kind] = duration;
        }

        private void ApplyDot(in StatusApplication status, Character applier, object source) {
            float perSecond = status.Power;
            if (applier != null && status.Ratio != 0f) perSecond += status.Ratio * applier.Stats.Value(status.ScalingStat);
            if (perSecond <= 0f) return;

            DamageOverTime dot = null;
            foreach (DamageOverTime d in _dots) {
                if (d.Kind == status.Kind && ReferenceEquals(d.Source, source)) {
                    dot = d;
                    break;
                }
            }
            if (dot == null) {
                dot = new DamageOverTime { Kind = status.Kind, Source = source, NextTick = TickInterval };
                _dots.Add(dot);
            }
            dot.Applier = applier;
            dot.DamagePerSecond = perSecond;
            dot.Remaining = status.Duration;
            dot.Stacks = Mathf.Min(dot.Stacks + 1, Mathf.Max(1, status.MaxStacks));
        }

        private void DealTick(DamageOverTime d) {
            DamageType type = d.Kind == StatusKind.Bleed ? DamageType.Physical : DamageType.Magic;
            float raw = d.DamagePerSecond * d.Stacks * TickInterval;
            _takeDamage?.Invoke(new DamageInfo(raw, type, isCrit: false, isBasicAttack: false, d.Applier));
        }

        private bool HasDot(StatusKind kind) {
            foreach (DamageOverTime d in _dots) if (d.Kind == kind) return true;
            return false;
        }

        private void NotifyIfChanged() {
            bool stunned = IsStunned, rooted = IsRooted, silenced = IsSilenced, slowed = IsSlowed, bleeding = IsBleeding, burning = IsBurning;
            if (stunned == _wasStunned && rooted == _wasRooted && silenced == _wasSilenced && slowed == _wasSlowed
                && bleeding == _wasBleeding && burning == _wasBurning) return;
            _wasStunned = stunned;
            _wasRooted = rooted;
            _wasSilenced = silenced;
            _wasSlowed = slowed;
            _wasBleeding = bleeding;
            _wasBurning = burning;
            Changed?.Invoke();
        }
    }
}
