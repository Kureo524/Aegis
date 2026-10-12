using System.Collections.Generic;
using Features.Characters;
using Features.Combat;
using Features.Events;
using Features.Movement;
using Features.Stats;
using Systems.EventBusses;
using Systems.Pooling;
using UnityEngine;
using UnityEngine.AI;

namespace Features.Spells {
    /// <summary>
    /// Applies a spell's effect once its cast time is over (design doc §7.5–7.6). Purely data
    /// driven: what happens follows the definition's Targeting and Kind, never the spell's name.
    /// Game scope service, used by every SpellCaster (player now, enemies in phase 6).
    ///
    /// - Self: buffs on the caster.
    /// - Skillshot: straight projectile (first enemy hit, or all with Pierce).
    /// - GroundPoint: every enemy in Radius around the point; Blink teleports the caster there
    ///   (or to the farthest walkable spot before it).
    /// - Cone: every enemy in the sector.
    /// - UnitTarget: homing projectile to the chosen enemy.
    /// Damage = Scaling(element levels) + attack stat × PowerRatio; Magic vs MagicResist, Physical vs
    /// Armor. Spells cannot crit (no SpellCrit capability yet, §5.6). Buffs are applied to the caster
    /// whatever the targeting.
    /// </summary>
    public sealed class SpellExecutor {
        private const float VfxDuration = 0.45f;

        private readonly ProjectileLauncher _projectiles;
        private readonly PrefabPool _pool;
        private readonly SpellSettings _settings;
        private readonly IEventBus _eventBus;
        private readonly Collider[] _hits = new Collider[64];
        private readonly List<Character> _targets = new();

        public SpellExecutor(ProjectileLauncher projectiles, PrefabPool pool, SpellSettings settings, IEventBus eventBus) {
            _projectiles = projectiles;
            _pool = pool;
            _settings = settings;
            _eventBus = eventBus;
        }

        /// <summary>
        /// Landing spot of a blink from <paramref name="origin"/> towards <paramref name="point"/>:
        /// the point itself if walkable (walls in between do not matter), otherwise the farthest
        /// walkable spot back along the line. False if that is barely away from the origin.
        /// </summary>
        public bool TryGetBlinkPoint(Vector3 origin, Vector3 point, out Vector3 walkable) {
            float step = Mathf.Max(0.1f, _settings.BlinkTolerance);
            Vector3 back = origin - point;
            back.y = 0f;
            float length = back.magnitude;
            Vector3 dir = length > 0f ? back / length : Vector3.zero;

            for (float d = 0f; d < length; d += step) {
                if (!NavMesh.SamplePosition(point + dir * d, out NavMeshHit hit, _settings.BlinkTolerance, NavMesh.AllAreas)) continue;
                Vector3 flat = hit.position - origin;
                flat.y = 0f;
                if (flat.magnitude < step) break;   // only the caster's own spot left
                walkable = hit.position;
                return true;
            }
            walkable = origin;
            return false;
        }

        public void Execute(Character caster, SpellDefinition spell, CastContext ctx, int levelsAboveOne) {
            if (caster == null || !caster.IsAlive) return;
            Vector3 origin = caster.transform.position;
            DamageInfo damage = BuildDamage(caster, spell, levelsAboveOne);

            switch (spell.Targeting) {
                case TargetingType.Self:
                    Ring(origin, Mathf.Max(spell.Radius, 1f), spell.Color);
                    break;

                case TargetingType.Skillshot: {
                    Vector3 muzzle = origin + Vector3.up * (caster.Definition.BodySize.y * 0.6f);
                    SkillshotProjectile projectile = _pool.Get(_settings.SkillshotProjectile);
                    projectile.Launch(muzzle, ctx.Direction, spell.Range, spell.ProjectileSpeed, spell.Radius, spell.Pierce,
                                      caster, damage, spell.Color, null, _pool.Release);
                    break;
                }

                case TargetingType.GroundPoint:
                    if (spell.Blink) {
                        if (TryGetBlinkPoint(origin, ctx.TargetPoint, out Vector3 destination)) {
                            Ring(origin, 0.8f, spell.Color);
                            caster.GetComponent<CharacterMovement>().Teleport(destination);
                            Ring(destination, 0.8f, spell.Color);
                        }
                        break;
                    }
                    if (spell.DealsDamage) HitAll(caster, damage, InCircle(caster, ctx.TargetPoint, spell.Radius));
                    Ring(ctx.TargetPoint, spell.Radius, spell.Color);
                    break;

                case TargetingType.Cone:
                    if (spell.DealsDamage) HitAll(caster, damage, InCone(caster, origin, ctx.Direction, spell.Range, spell.ConeAngle));
                    SpellVfx cone = _pool.Get(_settings.ShapeVfx);
                    cone.PlayCone(origin, ctx.Direction, spell.Range, spell.ConeAngle, spell.Color, VfxDuration, _pool.Release);
                    break;

                case TargetingType.UnitTarget:
                    if (ctx.TargetUnit != null && ctx.TargetUnit.IsAlive) {
                        Vector3 muzzle = origin + Vector3.up * (caster.Definition.BodySize.y * 0.6f);
                        _projectiles.Launch(_settings.HomingProjectile, muzzle, ctx.TargetUnit, damage, spell.ProjectileSpeed, null, spell.Color);
                    }
                    break;
            }

            ApplyBuffs(caster, spell);
            _eventBus.Publish(new SpellCastEvent(spell, caster.IsPlayer));
        }

        private static DamageInfo BuildDamage(Character caster, SpellDefinition spell, int levelsAboveOne) {
            StatController stats = caster.Stats;
            float raw = SpellMath.RawDamage(spell, levelsAboveOne, stats.Value(StatType.MagicAttack), stats.Value(StatType.PhysicalAttack));
            DamageType type = spell.Kind == SpellKind.Physical ? DamageType.Physical : DamageType.Magic;
            return new DamageInfo(raw, type, isCrit: false, isBasicAttack: false, caster,
                stats.Value(StatType.ArmorPenFlat), stats.Value(StatType.ArmorPenPercent),
                stats.Value(StatType.MagicPenFlat), stats.Value(StatType.MagicPenPercent));
        }

        private static void ApplyBuffs(Character caster, SpellDefinition spell) {
            foreach (BuffEffect buff in spell.Buffs) {
                caster.Buffs.Apply(caster.Stats.Get(buff.Stat), buff.Value, buff.Type, spell, buff.Duration);
            }
        }

        private static void HitAll(Character caster, DamageInfo damage, List<Character> targets) {
            foreach (Character target in targets) target.TakeDamage(damage);
        }

        private List<Character> InCircle(Character caster, Vector3 center, float radius) {
            _targets.Clear();
            int count = Physics.OverlapSphereNonAlloc(center, radius + 2f, _hits);
            for (int i = 0; i < count; i++) {
                Character c = _hits[i].GetComponentInParent<Character>();
                if (!SpellMath.IsValidTarget(caster, c) || _targets.Contains(c)) continue;
                Vector3 d = c.transform.position - center;
                d.y = 0f;
                if (d.magnitude - c.BodyRadius <= radius) _targets.Add(c);
            }
            return _targets;
        }

        private List<Character> InCone(Character caster, Vector3 origin, Vector3 direction, float range, float angle) {
            _targets.Clear();
            int count = Physics.OverlapSphereNonAlloc(origin, range + 2f, _hits);
            for (int i = 0; i < count; i++) {
                Character c = _hits[i].GetComponentInParent<Character>();
                if (!SpellMath.IsValidTarget(caster, c) || _targets.Contains(c)) continue;
                if (SpellMath.IsInCone(origin, direction, range, angle, c.transform.position, c.BodyRadius)) _targets.Add(c);
            }
            return _targets;
        }

        private void Ring(Vector3 center, float radius, Color color) {
            SpellVfx ring = _pool.Get(_settings.ShapeVfx);
            ring.PlayRing(center, radius, color, VfxDuration, _pool.Release);
        }
    }
}
