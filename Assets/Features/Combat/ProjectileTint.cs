using UnityEngine;

namespace Features.Combat {
    /// <summary>Colors a projectile (sprite + trail) after the element of the spell that fired it.</summary>
    public static class ProjectileTint {
        public static void Apply(SpriteRenderer sprite, TrailRenderer trail, Color color) {
            if (sprite != null) sprite.color = color;
            if (trail == null) return;
            trail.startColor = new Color(color.r, color.g, color.b, 0.9f);
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }
    }
}
