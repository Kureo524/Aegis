using Features.Characters;
using UnityEngine;

namespace Features.Spells {
    /// <summary>Pure spell formulas and geometry (design doc §6, §7.5–7.7), unit tested.</summary>
    public static class SpellMath {
        /// <summary>cooldown × 100 / (100 + AbilityHaste)</summary>
        public static float CooldownWithHaste(float baseCooldown, float abilityHaste) =>
            baseCooldown * 100f / (100f + Mathf.Max(0f, abilityHaste));

        /// <summary>Point aimed at, kept within <paramref name="range"/> of the origin, on the ground plane.</summary>
        public static Vector3 ClampToRange(Vector3 origin, Vector3 point, float range) {
            Vector3 delta = point - origin;
            delta.y = 0f;
            if (delta.magnitude > range) delta = delta.normalized * range;
            return new Vector3(origin.x + delta.x, origin.y, origin.z + delta.z);
        }

        /// <summary>
        /// Ground point of a cast: the cursor point clamped to <paramref name="range"/>, or with
        /// <paramref name="fixedRange"/> always at full range along <paramref name="direction"/>.
        /// </summary>
        public static Vector3 AimPoint(Vector3 origin, Vector3 point, Vector3 direction, float range, bool fixedRange) =>
            fixedRange ? new Vector3(origin.x + direction.x * range, origin.y, origin.z + direction.z * range)
                       : ClampToRange(origin, point, range);

        public static Vector3 FlatDirection(Vector3 from, Vector3 to, Vector3 fallback) {
            Vector3 d = to - from;
            d.y = 0f;
            return d.sqrMagnitude > 0.0001f ? d.normalized : fallback;
        }

        /// <summary>True if <paramref name="point"/> (minus <paramref name="pointRadius"/>) is inside the cone.</summary>
        public static bool IsInCone(Vector3 origin, Vector3 direction, float range, float angle, Vector3 point, float pointRadius) {
            Vector3 delta = point - origin;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance - pointRadius > range) return false;
            if (distance <= pointRadius) return true;
            return Vector3.Angle(direction, delta) <= angle / 2f;
        }

        /// <summary>Damage of a spell at <paramref name="rank"/>: base + AD, bonus AD and AP ratios (§7.6).</summary>
        public static float RawDamage(SpellDefinition spell, int rank, float attackDamage, float bonusAttackDamage, float abilityPower) =>
            spell.BaseDamage(rank) + spell.AdRatio * attackDamage + spell.BonusAdRatio * bonusAttackDamage + spell.ApRatio * abilityPower;

        public static bool IsValidTarget(Character caster, Character other) => other != null && other.IsAlive && caster.IsHostileTo(other);
    }
}
