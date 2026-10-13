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
        /// <summary>Straight projectile towards the cursor (Radius = its half width).</summary>
        Skillshot,
        /// <summary>Area at the cursor, clamped to Range (Radius).</summary>
        GroundPoint,
        /// <summary>Sector in front of the caster (Range long, ConeAngle wide).</summary>
        Cone,
        /// <summary>Enemy under the cursor, within Range (homing projectile).</summary>
        UnitTarget,
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
    /// The projectile marks instead of dealing damage (Ezreal W, §7.6): the caster's next basic
    /// attack or other spell on the target detonates the mark for this spell's damage and gives
    /// back ManaRefund (per rank).
    /// </summary>
    [Serializable]
    public struct MarkEffect {
        public bool Enabled;
        [Min(0f)] public float Duration;
        public float[] ManaRefund;
    }

    /// <summary>
    /// One spell (design doc §7.2, §7.5–7.7): a champion ability (Q W E R) with ranks, a summoner
    /// spell (D F) or an enemy special. Everything is data: the effect is chosen by
    /// <see cref="Targeting"/> and <see cref="Kind"/> and executed by SpellExecutor.
    /// Per-rank values are arrays indexed by rank − 1 (the last entry is used beyond the array).
    /// Read-only at runtime: ranks and cooldowns live in the casters.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Spells/Spell", fileName = "Spell_")]
    public class SpellDefinition : ScriptableObject {
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite icon;
        [Tooltip("VFX / indicator / projectile color.")]
        [SerializeField] private Color color = Color.white;

        [Header("Ranks")]
        [Tooltip("Champion level needed for each rank (Q W E: 1 3 5 7 9; R: 6 11 16). Empty = one rank, always available.")]
        [SerializeField] private int[] rankUnlockLevels = Array.Empty<int>();
        [Tooltip("Base damage per rank.")]
        [SerializeField] private float[] baseDamage = { 0f };
        [Tooltip("Cooldown per rank, in seconds (reduced by AbilityHaste).")]
        [SerializeField] private float[] cooldowns = { 0f };
        [Tooltip("Mana cost per rank.")]
        [SerializeField] private float[] manaCosts = { 0f };

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

        [Header("Effect")]
        [SerializeField] private SpellKind kind = SpellKind.Magic;
        [Tooltip("Damage = base (rank) + AdRatio × AD + BonusAdRatio × bonus AD + ApRatio × AP.")]
        [SerializeField] private float adRatio;
        [SerializeField] private float bonusAdRatio;
        [SerializeField] private float apRatio;
        [SerializeField] private BuffEffect[] buffs = Array.Empty<BuffEffect>();
        [Tooltip("Applied to every enemy hit (§5.7).")]
        [SerializeField] private StatusApplication[] statuses = Array.Empty<StatusApplication>();
        [SerializeField] private SelfSustain selfSustain;
        [Tooltip("Self: buffs and SelfSustain also go to every ally within Radius (enemy support, §9.6).")]
        [SerializeField] private bool affectsAllies;
        [Tooltip("Skillshot / UnitTarget projectile speed.")]
        [SerializeField, Min(0.1f)] private float projectileSpeed = 18f;
        [Tooltip("Skillshot: goes through enemies instead of stopping on the first one.")]
        [SerializeField] private bool pierce;
        [Tooltip("GroundPoint: teleports the caster to the point (Flash). Lands on the farthest walkable spot of the line.")]
        [SerializeField] private bool blink;
        [Tooltip("GroundPoint: the point is always at full Range in the cursor direction, however close the cursor is (Flash).")]
        [SerializeField] private bool fixedRange;

        [Header("On hit")]
        [Tooltip("Each hit also counts as a basic attack hit for the caster's passives (on-hit effects).")]
        [SerializeField] private bool appliesOnHit;
        [Tooltip("Each hit reduces the caster's ability cooldowns by this many seconds.")]
        [SerializeField, Min(0f)] private float cooldownRefundOnHit;
        [SerializeField] private MarkEffect mark;
        [Tooltip("Blink: then fires a homing bolt (this spell's damage) at the nearest enemy within this radius, a marked one first. 0 = none.")]
        [SerializeField, Min(0f)] private float blinkBoltRadius;

        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public Color Color => color;
        public TargetingType Targeting => targeting;
        public float Range => range;
        public float Radius => radius;
        public float ConeAngle => coneAngle;
        public float CastTime => castTime;
        public bool CanMoveWhileCasting => canMoveWhileCasting;
        public SpellKind Kind => kind;
        public float AdRatio => adRatio;
        public float BonusAdRatio => bonusAdRatio;
        public float ApRatio => apRatio;
        public BuffEffect[] Buffs => buffs;
        public StatusApplication[] Statuses => statuses;
        public SelfSustain SelfSustain => selfSustain;
        public bool AffectsAllies => affectsAllies;
        public float ProjectileSpeed => projectileSpeed;
        public bool Pierce => pierce;
        public bool Blink => blink;
        public bool FixedRange => fixedRange;
        public bool AppliesOnHit => appliesOnHit;
        public float CooldownRefundOnHit => cooldownRefundOnHit;
        public MarkEffect Mark => mark;
        public float BlinkBoltRadius => blinkBoltRadius;

        public bool DealsDamage => kind != SpellKind.Buff;
        public bool NeedsAim => targeting != TargetingType.Self;

        /// <summary>Number of ranks (1 for a spell without rank table).</summary>
        public int MaxRank => Mathf.Max(1, rankUnlockLevels.Length);
        /// <summary>Champion level needed for <paramref name="rank"/> (1-based); 1 without rank table.</summary>
        public int UnlockLevel(int rank) =>
            rankUnlockLevels.Length == 0 ? 1 : rankUnlockLevels[Mathf.Clamp(rank - 1, 0, rankUnlockLevels.Length - 1)];

        public float BaseDamage(int rank) => AtRank(baseDamage, rank);
        public float Cooldown(int rank) => AtRank(cooldowns, rank);
        public float ManaCost(int rank) => AtRank(manaCosts, rank);
        public float MarkManaRefund(int rank) => AtRank(mark.ManaRefund, rank);

        private static float AtRank(float[] values, int rank) =>
            values == null || values.Length == 0 ? 0f : values[Mathf.Clamp(rank - 1, 0, values.Length - 1)];
    }

    /// <summary>Where and at what a spell is cast (design doc §7.5).</summary>
    public struct CastContext {
        public Vector3 Origin;
        public Vector3 Direction;      // normalized, XZ plane
        public Vector3 TargetPoint;    // clamped to the spell range
        public Characters.Character TargetUnit;
    }
}
