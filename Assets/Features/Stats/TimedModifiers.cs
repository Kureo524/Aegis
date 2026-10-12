using System.Collections.Generic;

namespace Features.Stats {
    /// <summary>
    /// Stat modifiers that expire (design doc §5.4): buffs, debuffs, slows. Applying again from the
    /// same source on the same stat refreshes it (replaces value, restarts the timer) instead of
    /// stacking. Owned by Character, ticked every frame.
    /// </summary>
    public sealed class TimedModifiers {
        private struct Entry {
            public Stat Stat;
            public StatModifier Modifier;
            public float Remaining;
        }

        private readonly List<Entry> _entries = new();

        public int Count => _entries.Count;

        public void Apply(Stat stat, float value, ModifierType type, object source, float duration) {
            if (stat == null || duration <= 0f) return;
            RemoveExisting(stat, source);

            var modifier = new StatModifier(value, type, source);
            stat.AddModifier(modifier);
            _entries.Add(new Entry { Stat = stat, Modifier = modifier, Remaining = duration });
        }

        public void Tick(float deltaTime) {
            for (int i = _entries.Count - 1; i >= 0; i--) {
                Entry e = _entries[i];
                e.Remaining -= deltaTime;
                if (e.Remaining > 0f) {
                    _entries[i] = e;
                    continue;
                }
                e.Stat.RemoveModifier(e.Modifier);
                _entries.RemoveAt(i);
            }
        }

        public void Clear() {
            foreach (Entry e in _entries) e.Stat.RemoveModifier(e.Modifier);
            _entries.Clear();
        }

        private void RemoveExisting(Stat stat, object source) {
            for (int i = _entries.Count - 1; i >= 0; i--) {
                if (_entries[i].Stat != stat || !ReferenceEquals(_entries[i].Modifier.Source, source)) continue;
                stat.RemoveModifier(_entries[i].Modifier);
                _entries.RemoveAt(i);
            }
        }
    }
}
