using Features.Characters;
using UnityEngine;

namespace Features.Combat {
    /// <summary>
    /// Finds the enemy the player points at (design doc §4.3): the one under the cursor, else the
    /// closest one within a tolerance radius around the pointed ground point (thin sprites are hard
    /// to click). Buffers are passed in so nothing is allocated per click.
    /// </summary>
    public static class TargetPicker {
        public static Character EnemyUnderCursor(Character self, Ray ray, Vector3 groundPoint, float pickRadius,
                                                 RaycastHit[] rayHits, Collider[] overlapHits) {
            Character best = null;
            float bestDistance = float.MaxValue;

            int count = Physics.RaycastNonAlloc(ray, rayHits, 500f);
            for (int i = 0; i < count; i++) {
                Character c = rayHits[i].collider.GetComponentInParent<Character>();
                if (!IsEnemy(self, c) || rayHits[i].distance >= bestDistance) continue;
                best = c;
                bestDistance = rayHits[i].distance;
            }
            return best != null ? best : ClosestEnemy(self, groundPoint, pickRadius, overlapHits);
        }

        public static Character ClosestEnemy(Character self, Vector3 point, float radius, Collider[] overlapHits) {
            Character best = null;
            float bestDistance = float.MaxValue;
            int count = Physics.OverlapSphereNonAlloc(point, radius, overlapHits);
            for (int i = 0; i < count; i++) {
                Character c = overlapHits[i].GetComponentInParent<Character>();
                if (!IsEnemy(self, c)) continue;
                float d = Vector3.Distance(point, c.transform.position);
                if (d >= bestDistance) continue;
                best = c;
                bestDistance = d;
            }
            return best;
        }

        private static bool IsEnemy(Character self, Character c) => c != null && c.IsAlive && self.IsHostileTo(c);
    }
}
