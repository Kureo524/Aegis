using UnityEngine;

namespace Features.Combat {
    /// <summary>
    /// Damage formulas of design doc §6. Pure functions (no state), unit tested.
    /// </summary>
    public static class DamageCalculator {
        /// <summary>
        /// Resistance left after penetration: max(0, r × (1 − %pen) − flatPen). Penetration never
        /// pushes a resistance below 0, but an already negative one (shred) stays negative.
        /// </summary>
        public static float EffectiveResistance(float resistance, float penFlat, float penPercent) {
            if (resistance <= 0f) return resistance;
            return Mathf.Max(0f, resistance * (1f - penPercent) - penFlat);
        }

        /// <summary>100 / (100 + r) for r ≥ 0, 2 − 100 / (100 − r) for negative r (extra damage).</summary>
        public static float Multiplier(float effectiveResistance) =>
            effectiveResistance >= 0f
                ? 100f / (100f + effectiveResistance)
                : 2f - 100f / (100f - effectiveResistance);

        /// <summary>Final damage of <paramref name="info"/> against the given resistances.</summary>
        public static float Mitigate(in DamageInfo info, float armor, float magicResist) {
            return info.Type switch {
                DamageType.Physical => info.RawAmount * Multiplier(EffectiveResistance(armor, info.ArmorPenFlat, info.ArmorPenPercent)),
                DamageType.Magic => info.RawAmount * Multiplier(EffectiveResistance(magicResist, info.MagicPenFlat, info.MagicPenPercent)),
                _ => info.RawAmount,   // True damage ignores resistances
            };
        }

        /// <summary>Healing earned by the attacker on damage actually dealt (§6 step 6).</summary>
        public static float Sustain(float damageDealt, bool isBasicAttack, float omnivamp, float lifesteal, float healShieldPower) {
            float heal = damageDealt * omnivamp + (isBasicAttack ? damageDealt * lifesteal : 0f);
            return heal * healShieldPower;
        }
    }
}
