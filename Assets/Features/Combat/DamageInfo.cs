using Features.Characters;

namespace Features.Combat {
    /// <summary>
    /// One hit, as built by the attacker (design doc §6). Penetration is a snapshot of the
    /// attacker's stats at the time of the hit. Struct: no allocation per hit.
    /// </summary>
    public readonly struct DamageInfo {
        public readonly float RawAmount;
        public readonly DamageType Type;
        public readonly bool IsCrit;
        public readonly bool IsBasicAttack;     // lifesteal only applies to basic attacks
        public readonly Character Source;       // null = environment
        public readonly float ArmorPenFlat, ArmorPenPercent, MagicPenFlat, MagicPenPercent;

        public DamageInfo(float rawAmount, DamageType type, bool isCrit, bool isBasicAttack, Character source,
                          float armorPenFlat = 0f, float armorPenPercent = 0f,
                          float magicPenFlat = 0f, float magicPenPercent = 0f) {
            RawAmount = rawAmount;
            Type = type;
            IsCrit = isCrit;
            IsBasicAttack = isBasicAttack;
            Source = source;
            ArmorPenFlat = armorPenFlat;
            ArmorPenPercent = armorPenPercent;
            MagicPenFlat = magicPenFlat;
            MagicPenPercent = magicPenPercent;
        }

        /// <summary>The same hit with another raw amount (damage modifiers).</summary>
        public DamageInfo WithAmount(float rawAmount) =>
            new(rawAmount, Type, IsCrit, IsBasicAttack, Source, ArmorPenFlat, ArmorPenPercent, MagicPenFlat, MagicPenPercent);
    }
}
