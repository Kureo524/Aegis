using Features.Combat;
using UnityEngine;

namespace Features.Spells {
    /// <summary>Global spell rules and effect prefabs (design doc §7.5, §7.7). Registered in the root.</summary>
    [CreateAssetMenu(menuName = "Game/Settings/Spell Settings", fileName = "SpellSettings")]
    public class SpellSettings : ScriptableObject {
        [Header("Casting")]
        [Tooltip("Blink: how far from the aimed point a walkable spot may be (NavMesh sample).")]
        [SerializeField] private float blinkTolerance = 0.5f;

        [Header("Effect prefabs")]
        [SerializeField] private Projectile homingProjectile;
        [SerializeField] private SkillshotProjectile skillshotProjectile;
        [SerializeField] private SpellVfx shapeVfx;

        public float BlinkTolerance => blinkTolerance;
        public Projectile HomingProjectile => homingProjectile;
        public SkillshotProjectile SkillshotProjectile => skillshotProjectile;
        public SpellVfx ShapeVfx => shapeVfx;
    }
}
