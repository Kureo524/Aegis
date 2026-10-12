namespace Features.Stats {
    /// <summary>
    /// Intrinsic clamp rules per stat (design doc §5.3).
    /// These are game rules rather than per-character balance data, so they live in code.
    /// A <see cref="Stat"/> still accepts explicit bounds, so a settings asset can override
    /// them later without touching this table.
    /// </summary>
    public static class StatClamp {
        public static (float Min, float Max) GetBounds(StatType type) {
            switch (type) {
                case StatType.CritChance: return (0f, 1f);
                case StatType.Tenacity: return (0f, 0.9f);
                case StatType.ManaCostReduction: return (0f, 0.8f);
                case StatType.SlowResist: return (0f, 1f);

                case StatType.MaxHealth:
                case StatType.MaxMana:
                case StatType.HealthRegen:
                case StatType.ManaRegen:
                case StatType.AttackSpeed:
                case StatType.AttackRange:
                case StatType.MoveSpeed:
                case StatType.AbilityHaste:
                    return (0f, float.MaxValue);

                default:
                    return (float.MinValue, float.MaxValue);
            }
        }
    }
}
