using System;
using System.Collections.Generic;
using UnityEngine;

namespace Features.Stats {
    /// <summary>
    /// A single stat (design doc §5.2 / §5.3).
    ///
    /// The final value is cached and recomputed lazily (dirty flag) whenever the base
    /// value or the modifier list changes:
    ///
    ///     Final = (Base + ΣFlat) × (1 + ΣPercentAdd) × Π(1 + PercentMult)
    /// </summary>
    public class Stat {
        private readonly List<StatModifier> _modifiers = new();
        private readonly float _min;
        private readonly float _max;

        private float _baseValue;
        private float _cachedValue;
        private bool _dirty;

        public StatType Type { get; }

        /// <summary>Raised with the new clamped value whenever the stat changes.</summary>
        public event Action<float> Changed;

        public Stat(StatType type, float baseValue = 0f,
                    float min = float.MinValue, float max = float.MaxValue) {
            Type = type;
            _min = min;
            _max = max;
            _baseValue = baseValue;
            _dirty = true;
        }

        public float BaseValue {
            get => _baseValue;
            set {
                if (Mathf.Approximately(_baseValue, value)) return;
                _baseValue = value;
                MarkDirty();
            }
        }

        /// <summary>Final clamped value. Recomputes once if a change happened since last read.</summary>
        public float Value {
            get {
                if (_dirty) Recalculate();
                return _cachedValue;
            }
        }

        public IReadOnlyList<StatModifier> Modifiers => _modifiers;

        public void AddModifier(StatModifier modifier) {
            if (modifier == null) return;
            _modifiers.Add(modifier);
            MarkDirty();
        }

        public bool RemoveModifier(StatModifier modifier) {
            if (modifier == null) return false;
            if (!_modifiers.Remove(modifier)) return false;
            MarkDirty();
            return true;
        }

        /// <summary>Removes every modifier applied by <paramref name="source"/>. Returns how many were removed.</summary>
        public int RemoveAllFromSource(object source) {
            int removed = _modifiers.RemoveAll(m => ReferenceEquals(m.Source, source));
            if (removed > 0) MarkDirty();
            return removed;
        }

        private void MarkDirty() {
            _dirty = true;
            // Force the recompute now so the event carries the fresh value.
            Changed?.Invoke(Value);
        }

        private void Recalculate() {
            float flat = 0f;
            float percentAdd = 0f;
            float percentMult = 1f;

            foreach (StatModifier m in _modifiers) {
                switch (m.Type) {
                    case ModifierType.Flat:
                        flat += m.Value;
                        break;
                    case ModifierType.PercentAdd:
                        percentAdd += m.Value;
                        break;
                    case ModifierType.PercentMult:
                        percentMult *= 1f + m.Value;
                        break;
                }
            }

            float value = (_baseValue + flat) * (1f + percentAdd) * percentMult;
            _cachedValue = Mathf.Clamp(value, _min, _max);
            _dirty = false;
        }
    }
}
