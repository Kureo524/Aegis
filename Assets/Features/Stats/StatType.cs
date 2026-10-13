namespace Features.Stats {
    /// <summary>
    /// Every stat a character can own (design doc §5.1).
    /// Optional Vampire-Survivors style stats (PickupRange, ExpGain, Luck, Revival)
    /// will be appended here when needed.
    /// </summary>
    public enum StatType {
        // Vitality
        MaxHealth, HealthRegen, MaxMana, ManaRegen,

        // Offensive
        PhysicalAttack, MagicAttack, AttackSpeed, AttackRange, CritChance, CritDamage,
        ArmorPenFlat, ArmorPenPercent, MagicPenFlat, MagicPenPercent,

        // Defensive
        Armor, MagicResist, Tenacity, SlowResist,

        // Mobility
        MoveSpeed,

        // Sustain
        Omnivamp, Lifesteal, HealShieldPower,

        // Utility
        AbilityHaste, ManaCostReduction,

        // Converted (appended: stat indices are serialized)
        /// <summary>Becomes AD (× 0.6) or AP (× 1), whichever the character has more bonus of (§5.1).</summary>
        AdaptiveForce
    }
}
