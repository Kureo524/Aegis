using Features.Combat;
using UnityEngine;

namespace Features.Spells {
    /// <summary>Global spell rules and balance (design doc §7.2, §7.3, §7.7, §7.8). Registered in the root.</summary>
    [CreateAssetMenu(menuName = "Game/Settings/Spell Settings", fileName = "SpellSettings")]
    public class SpellSettings : ScriptableObject {
        [Header("Element selection")]
        [SerializeField] private int maxTotalStacks = 3;
        [SerializeField] private int maxStacksPerElement = 3;

        [Header("Mana cost")]
        [Tooltip("Multiplier by total number of stacks: index 0 = 1 stack, 1 = 2 stacks…")]
        [SerializeField] private float[] costCurve = { 1f, 1.5f, 2.25f };

        [Header("Element levels")]
        [SerializeField] private int maxElementLevel = 5;

        [Header("Casting")]
        [Tooltip("Shared cooldown of elemental spells by total stacks of the cast spell: index 0 = 1 stack…")]
        [SerializeField] private float[] cooldownByStacks = { 2f, 3.5f, 5.5f };
        [SerializeField] private bool clearSelectionAfterCast = true;
        [Tooltip("Blink: how far from the aimed point a walkable spot may be (NavMesh sample).")]
        [SerializeField] private float blinkTolerance = 0.5f;

        [Header("Effect prefabs")]
        [SerializeField] private Projectile homingProjectile;
        [SerializeField] private SkillshotProjectile skillshotProjectile;
        [SerializeField] private SpellVfx shapeVfx;

        public int MaxTotalStacks => maxTotalStacks;
        public int MaxStacksPerElement => maxStacksPerElement;
        public int MaxElementLevel => maxElementLevel;
        public bool ClearSelectionAfterCast => clearSelectionAfterCast;
        public float BlinkTolerance => blinkTolerance;
        public Projectile HomingProjectile => homingProjectile;
        public SkillshotProjectile SkillshotProjectile => skillshotProjectile;
        public SpellVfx ShapeVfx => shapeVfx;

        /// <summary>Base cooldown of an elemental spell of <paramref name="totalStacks"/> stacks.</summary>
        public float CooldownByStacks(int totalStacks) {
            if (totalStacks <= 0 || cooldownByStacks.Length == 0) return 0f;
            return cooldownByStacks[Mathf.Min(totalStacks, cooldownByStacks.Length) - 1];
        }

        /// <summary>Cost multiplier for <paramref name="totalStacks"/> stacks (last entry beyond the table).</summary>
        public float CostMultiplier(int totalStacks) {
            if (totalStacks <= 0 || costCurve.Length == 0) return 0f;
            return costCurve[Mathf.Min(totalStacks, costCurve.Length) - 1];
        }

#if UNITY_EDITOR
        private void OnValidate() {
            if (costCurve.Length < maxTotalStacks)
                Debug.LogWarning($"[SpellSettings] {name}: costCurve needs {maxTotalStacks} entries.", this);
        }
#endif
    }
}
