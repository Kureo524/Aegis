using System.Collections.Generic;
using Features.Characters;
using UnityEngine;

namespace Features.Combat {
    /// <summary>Which enemy an attack-move picks among those in range (design doc §4.6).
    /// Strategy, so bosses-first, lowest-HP or closest-to-cursor rules can be added later.</summary>
    public interface ITargetSelector {
        Character Select(Character attacker, IReadOnlyList<Character> candidates);
    }

    /// <summary>Default rule: the enemy closest to the attacker.</summary>
    public sealed class NearestTargetSelector : ITargetSelector {
        public Character Select(Character attacker, IReadOnlyList<Character> candidates) {
            Character best = null;
            float bestSqr = float.MaxValue;
            Vector3 origin = attacker.transform.position;
            foreach (Character c in candidates) {
                float sqr = (c.transform.position - origin).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                best = c;
                bestSqr = sqr;
            }
            return best;
        }
    }
}
