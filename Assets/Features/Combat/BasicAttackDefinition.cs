using UnityEngine;

namespace Features.Combat {
    /// <summary>
    /// How a character auto-attacks (design doc §4.5). Range, attack speed and damage come from
    /// the character's stats (AttackRange, AttackSpeed, PhysicalAttack/MagicAttack), so they grow
    /// with levels and modifiers; this asset only holds what stats cannot express.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Basic Attack Definition", fileName = "BasicAttack_")]
    public class BasicAttackDefinition : ScriptableObject {
        [Tooltip("Share of the attack interval spent before the hit lands (0..1).")]
        [SerializeField, Range(0f, 1f)] private float windupPercent = 0.25f;
        [Tooltip("Null = instant melee hit.")]
        [SerializeField] private GameObject projectilePrefab;
        [Tooltip("Multiplier applied to the scaling attack stat.")]
        [SerializeField] private float damageRatio = 1f;
        [SerializeField] private DamageType damageType = DamageType.Physical;

        public float WindupPercent => windupPercent;
        public GameObject ProjectilePrefab => projectilePrefab;
        public bool IsRanged => projectilePrefab != null;
        public float DamageRatio => damageRatio;
        public DamageType DamageType => damageType;
    }
}
