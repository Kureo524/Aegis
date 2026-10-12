using System.Collections.Generic;

namespace Features.Stats {
    /// <summary>
    /// Owns every stat of a character and exposes them by <see cref="StatType"/>
    /// (design doc §5.2). Stats are created once in <see cref="Init"/> and never
    /// replaced afterwards, so references handed out stay valid.
    /// </summary>
    public class StatController {
        private readonly Dictionary<StatType, Stat> _stats = new();

        public IReadOnlyDictionary<StatType, Stat> Stats => _stats;

        /// <summary>
        /// Creates every stat from the block and applies the intrinsic clamp rules. Missing stats
        /// default to 0, except the multipliers (HealShieldPower) which default to 1. Safe to call
        /// again to reset a character.
        /// </summary>
        public void Init(StatBlock block) {
            _stats.Clear();

            foreach (StatType type in System.Enum.GetValues(typeof(StatType))) {
                float baseValue = block != null && block.TryGet(type, out float v) ? v : DefaultValue(type);
                (float min, float max) = StatClamp.GetBounds(type);
                _stats[type] = new Stat(type, baseValue, min, max);
            }
        }

        private static float DefaultValue(StatType type) => type == StatType.HealShieldPower ? 1f : 0f;

        public Stat Get(StatType type) {
            return _stats.TryGetValue(type, out Stat stat) ? stat : null;
        }

        /// <summary>Convenience accessor for the final value.</summary>
        public float Value(StatType type) {
            Stat stat = Get(type);
            return stat != null ? stat.Value : 0f;
        }
    }
}
