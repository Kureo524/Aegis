using System;
using Features.Stats;

namespace Features.Combat {
    /// <summary>
    /// Hit points of a character (design doc §6). Max is the MaxHealth stat: when it grows (level,
    /// item) current HP grows by the same amount, LoL style; when it shrinks, current is clamped.
    /// Pure C#, owned by Character. Shield comes later.
    /// </summary>
    public sealed class Health {
        private readonly Stat _max;
        private float _lastMax;

        public float Current { get; private set; }
        public float Max => _max.Value;
        public float Ratio => Max > 0f ? Current / Max : 0f;
        public bool IsDead { get; private set; }

        /// <summary>(current, max)</summary>
        public event Action<float, float> Changed;
        public event Action Died;

        public Health(Stat maxHealth) {
            _max = maxHealth ?? throw new ArgumentNullException(nameof(maxHealth));
            _lastMax = Max;
            Current = Max;
            _max.Changed += OnMaxChanged;
        }

        /// <summary>Removes HP. Returns the damage actually taken (never more than what was left).</summary>
        public float TakeDamage(float amount) {
            if (IsDead || amount <= 0f) return 0f;
            float taken = Math.Min(amount, Current);
            Current -= taken;
            Changed?.Invoke(Current, Max);

            if (Current <= 0f) {
                Current = 0f;
                IsDead = true;
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

        /// <summary>Back to life at full HP.</summary>
        public void Revive() {
            IsDead = false;
            Current = Max;
            Changed?.Invoke(Current, Max);
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
