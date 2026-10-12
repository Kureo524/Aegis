using System;
using System.Collections.Generic;
using Features.Stats;

namespace Features.Combat {
    /// <summary>
    /// Hit points of a character (design doc §6). Max is the MaxHealth stat: when it grows (level,
    /// item) current HP grows by the same amount, LoL style; when it shrinks, current is clamped.
    /// Shields (§6 step 5) absorb damage before HP, the one expiring first is used first; they
    /// fade out when their duration is over (<see cref="Tick"/>).
    /// Pure C#, owned by Character.
    /// </summary>
    public sealed class Health {
        private readonly Stat _max;
        private float _lastMax;
        private readonly List<(float amount, float remaining)> _shields = new();

        public float Current { get; private set; }
        public float Max => _max.Value;
        public float Ratio => Max > 0f ? Current / Max : 0f;
        public bool IsDead { get; private set; }
        /// <summary>Total of the active shields.</summary>
        public float Shield { get; private set; }

        /// <summary>(current, max). Also raised when the shield changes.</summary>
        public event Action<float, float> Changed;
        public event Action Died;

        public Health(Stat maxHealth) {
            _max = maxHealth ?? throw new ArgumentNullException(nameof(maxHealth));
            _lastMax = Max;
            Current = Max;
            _max.Changed += OnMaxChanged;
        }

        /// <summary>
        /// Removes shield then HP. Returns the damage actually taken, shield included (never more
        /// than what was left).
        /// </summary>
        public float TakeDamage(float amount) {
            if (IsDead || amount <= 0f) return 0f;
            float absorbed = AbsorbWithShields(amount);
            float taken = Math.Min(amount - absorbed, Current);
            Current -= taken;
            Changed?.Invoke(Current, Max);
            taken += absorbed;

            if (Current <= 0f) {
                Current = 0f;
                IsDead = true;
                _shields.Clear();
                Shield = 0f;
                Died?.Invoke();
            }
            return taken;
        }

        /// <summary>Adds HP up to max. Returns the amount actually healed. No effect when dead.</summary>
        public float Heal(float amount) {
            if (IsDead || amount <= 0f || Current >= Max) return 0f;
            float healed = Math.Min(amount, Max - Current);
            Current += healed;
            Changed?.Invoke(Current, Max);
            return healed;
        }

        /// <summary>Adds a shield of <paramref name="amount"/> for <paramref name="duration"/> seconds. No effect when dead.</summary>
        public void AddShield(float amount, float duration) {
            if (IsDead || amount <= 0f || duration <= 0f) return;
            _shields.Add((amount, duration));
            Shield += amount;
            Changed?.Invoke(Current, Max);
        }

        /// <summary>Shields wear off.</summary>
        public void Tick(float deltaTime) {
            if (_shields.Count == 0) return;
            bool expired = false;
            for (int i = _shields.Count - 1; i >= 0; i--) {
                (float amount, float remaining) = _shields[i];
                remaining -= deltaTime;
                if (remaining > 0f) {
                    _shields[i] = (amount, remaining);
                    continue;
                }
                _shields.RemoveAt(i);
                expired = true;
            }
            if (!expired) return;
            RecomputeShield();
            Changed?.Invoke(Current, Max);
        }

        public void ClearShields() {
            if (_shields.Count == 0) return;
            _shields.Clear();
            Shield = 0f;
            Changed?.Invoke(Current, Max);
        }

        /// <summary>Back to life at full HP, without shield.</summary>
        public void Revive() {
            IsDead = false;
            _shields.Clear();
            Shield = 0f;
            Current = Max;
            Changed?.Invoke(Current, Max);
        }

        // The shield that expires first takes the hit first
        private float AbsorbWithShields(float amount) {
            if (_shields.Count == 0) return 0f;
            _shields.Sort((a, b) => a.remaining.CompareTo(b.remaining));
            float absorbed = 0f;
            while (_shields.Count > 0 && absorbed < amount) {
                (float shield, float remaining) = _shields[0];
                float used = Math.Min(shield, amount - absorbed);
                absorbed += used;
                if (used >= shield) _shields.RemoveAt(0);
                else _shields[0] = (shield - used, remaining);
            }
            RecomputeShield();
            return absorbed;
        }

        private void RecomputeShield() {
            float total = 0f;
            foreach ((float amount, float _) in _shields) total += amount;
            Shield = total;
        }

        /// <summary>Stops listening to the MaxHealth stat (character re-initialized).</summary>
        public void Detach() => _max.Changed -= OnMaxChanged;

        private void OnMaxChanged(float newMax) {
            float delta = newMax - _lastMax;
            _lastMax = newMax;
            if (!IsDead && delta > 0f) Current += delta;
            if (Current > newMax) Current = newMax;
            Changed?.Invoke(Current, Max);
        }
    }
}
