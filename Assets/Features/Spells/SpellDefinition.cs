using System;
using Features.Combat;
using Features.Stats;
using UnityEngine;

namespace Features.Spells {
    public enum SpellKind { Magic, Physical, Buff }

    /// <summary>How a spell is aimed (design doc §7.5).</summary>
    public enum TargetingType {
        /// <summary>No aim: cast immediately on the caster.</summary>
        Self,
        /// <summary>Straight projectile towards the cursor (Width = its width).</summary>
        Skillshot,
        /// <summary>Area at the cursor, clamped to Range (Radius).</summary>
        GroundPoint,
        /// <summary>Sector in front of the caster (Range long, ConeAngle wide).</summary>
        Cone,
        /// <summary>Enemy under the cursor, within Range (homing projectile).</summary>
        UnitTarget,
    }

    [Serializable]
    public struct ElementCount {
        public ElementDefinition Element;
        [Min(1)] public int Count;
    }

    /// <summary>value = Base + PerElementLevel × Σ (level − 1) over the DISTINCT elements of the recipe (§7.6).</summary>
    [Serializable]
    public struct SpellScaling {
        public float Base;
        public float PerElementLevel;

        public float Evaluate(int sumOfLevelsAboveOne) => Base + PerElementLevel * sumOfLevelsAboveOne;
    }

    /// <summary>Temporary stat change applied by a Buff spell (§7.6, §5.4).</summary>
    [Serializable]
    public struct BuffEffect {
        public StatType Stat;
        public ModifierType Type;
        public float Value;
        [Min(0f)] public float Duration;
    }

    /// <summary>Heal and / or shield the caster gets when the spell goes off (§6).</summary>
    [Serializable]
    public struct SelfSustain {
        public float Heal;
        public float Shield;
        [Tooltip("+ MagicAttack × this, on both.")]
        public float PowerRatio;
        [Min(0f)] public float ShieldDuration;

        public bool IsEmpty => Heal <= 0f && Shield <= 0f && PowerRatio <= 0f;
    }

    /// <summary>
    /// One spell (design doc §7.2, §7.5–7.7). Elemental spells are identified by their recipe; a
    /// signature ability (F) has no recipe. Everything is data: the effect is chosen by
    /// <see cref="Targeting"/> and <see cref="Kind"/>, executed by SpellExecutor.
    /// Read-only at runtime: cooldowns live in the casters.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Spells/Spell", fileName = "Spell_")]
    public class SpellDefinition : ScriptableObject {
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [Tooltip("VFX / indicator / projectile color.")]
        [SerializeField] private Color color = Color.white;
        [SerializeField] private ElementCount[] recipe = Array.Empty<ElementCount>();

        [Header("Aim")]
        [SerializeField] private TargetingType targeting = TargetingType.Skillshot;
        [Tooltip("Max distance of the cast (point, projectile travel, cone length).")]
        [SerializeField, Min(0f)] private float range = 8f;
        [Tooltip("GroundPoint / Self: area radius. Skillshot: half width. Cone: unused.")]
        [SerializeField, Min(0f)] private float radius = 1f;
        [SerializeField, Range(1f, 180f)] private float coneAngle = 60f;

        [Header("Cast")]
        [Tooltip("Windup before the effect, in seconds.")]
        [SerializeField, Min(0f)] private float castTime = 0.2f;
        [SerializeField] private bool canMoveWhileCasting;
        [Tooltip("0 = SpellSettings.CooldownByStacks (elemental) — signature abilities must set it.")]
        [SerializeField, Min(0f)] private float cooldownOverride;

        [Header("Effect")]
        [SerializeField] private SpellKind kind = SpellKind.Magic;
        [Tooltip("Damage: Scaling + attack stat (MagicAttack / PhysicalAttack) × PowerRatio.")]
        [SerializeField] private float powerRatio = 0.5f;
        [SerializeField] private SpellScaling scaling = new() { Base = 50f, PerElementLevel = 10f };
        [SerializeField] private BuffEffect[] buffs = Array.Empty<BuffEffect>();
        [Tooltip("Applied to every enemy hit (§5.7).")]
        [SerializeField] private StatusApplication[] statuses = Array.Empty<StatusApplication>();
        [SerializeField] private SelfSustain selfSustain;
        [Tooltip("Skillshot / UnitTarget projectile speed.")]
        [SerializeField, Min(0.1f)] private float projectileSpeed = 18f;
        [Tooltip("Skillshot: goes through enemies instead of stopping on the first one.")]
        [SerializeField] private bool pierce;
        [Tooltip("GroundPoint: teleports the caster to the point (Flash). Lands on the farthest walkable spot of the line.")]
        [SerializeField] private bool blink;
        [Tooltip("GroundPoint: the point is always at full Range in the cursor direction, however close the cursor is (Flash).")]
        [SerializeField] private bool fixedRange;

        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public Color Color => color;
        public ElementCount[] Recipe => recipe;
        public TargetingType Targeting => targeting;
        public float Range => range;
        public float Radius => radius;
        public float ConeAngle => coneAngle;
        public float CastTime => castTime;
        public bool CanMoveWhileCasting => canMoveWhileCasting;
        public float CooldownOverride => cooldownOverride;
        public SpellKind Kind => kind;
        public float PowerRatio => powerRatio;
        public SpellScaling Scaling => scaling;
        public BuffEffect[] Buffs => buffs;
        public StatusApplication[] Statuses => statuses;
        public SelfSustain SelfSustain => selfSustain;
        public float ProjectileSpeed => projectileSpeed;
        public bool Pierce => pierce;
        public bool Blink => blink;
        public bool FixedRange => fixedRange;

        public bool DealsDamage => kind != SpellKind.Buff;
        public bool NeedsAim => targeting != TargetingType.Self;

        public int TotalStacks {
            get {
                int total = 0;
                foreach (ElementCount e in recipe) total += e.Count;
                return total;
            }
        }
    }

    /// <summary>Where and at what a spell is cast (design doc §7.5).</summary>
    public struct CastContext {
        public Vector3 Origin;
        public Vector3 Direction;      // normalized, XZ plane
        public Vector3 TargetPoint;    // clamped to the spell range
        public Characters.Character TargetUnit;
    }
}
