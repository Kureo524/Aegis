namespace Features.Stats {
    /// <summary>
    /// How a <see cref="StatModifier"/> combines into a stat (design doc §5.3).
    /// </summary>
    public enum ModifierType {
        /// <summary>Added before any percentage. ex. +10 attack.</summary>
        Flat,

        /// <summary>All PercentAdd bonuses are summed together. +10% and +20% -> +30%.</summary>
        PercentAdd,

        /// <summary>Multiplied together, reserved for powerful effects. Kept as a factor (1 + value).</summary>
        PercentMult
    }
}
