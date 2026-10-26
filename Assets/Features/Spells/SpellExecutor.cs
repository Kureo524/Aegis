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
    /// Game scope service, used by every SpellCaster (player and enemies).
    ///
    /// - Self: buffs on the caster.
    /// - Skillshot: straight projectile (first enemy hit, or all with Pierce).
    /// - GroundPoint: every enemy in Radius around the point; Blink teleports the caster there
    ///   (or to the farthest walkable spot before it), then may fire a bolt (BlinkBoltRadius).
    /// - Cone: every enemy in the sector.
    /// - UnitTarget: homing projectile to the chosen enemy.
    /// Damage = base (rank) + AD / bonus AD / AP ratios; Magic vs MagicResist, Physical vs Armor.
    /// On hit: statuses, mark (applied, or detonated by another hit), on-hit effects, cooldown
    /// refund (§7.6). Spells only crit with the SpellCrit capability (§5.6). Buffs and the self heal /
    /// shield are applied to the caster whatever the targeting (and to its allies within Radius with
    /// AffectsAllies); statuses (§5.7) to every enemy hit, which also raises the caster's SpellHit
    /// event (passives).
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

        /// <summary>
        /// Shows where a spell is about to land, for <paramref name="duration"/> (enemy casts, so the
        /// player can dodge, §9.6).
        /// </summary>
        public void Telegraph(SpellDefinition spell, CastContext ctx, float duration) {
            if (duration <= 0f) return;
            // Red: dodge it. Green: a support spell on the caster's side
            Color color = spell.DealsDamage ? new Color(1f, 0.25f, 0.2f, 0.9f) : new Color(0.35f, 1f, 0.45f, 0.9f);
            SpellVfx vfx = _pool.Get(_settings.ShapeVfx);
            switch (spell.Targeting) {
                case TargetingType.Skillshot:
                    vfx.PlayLine(ctx.Origin, ctx.Direction, spell.Range, Mathf.Max(spell.Radius, 0.3f), color, duration, _pool.Release);
                    break;
                case TargetingType.GroundPoint:
                    vfx.PlayRing(ctx.TargetPoint, Mathf.Max(spell.Radius, 0.5f), color, duration, _pool.Release);
                    break;
                case TargetingType.Cone:
                    vfx.PlayCone(ctx.Origin, ctx.Direction, spell.Range, spell.ConeAngle, color, duration, _pool.Release);
                    break;
                case TargetingType.Self:
                    vfx.PlayRing(ctx.Origin, Mathf.Max(spell.Radius, 1f), color, duration, _pool.Release);
                    break;
                default:
                    _pool.Release(vfx);
                    break;
            }
        }

        /// <summary>Shows a charge path (boss) for <paramref name="duration"/>.</summary>
        public void TelegraphLine(Vector3 origin, Vector3 direction, float length, float halfWidth, float duration) {
            SpellVfx vfx = _pool.Get(_settings.ShapeVfx);
            vfx.PlayLine(origin, direction, length, halfWidth, new Color(1f, 0.25f, 0.2f, 0.9f), duration, _pool.Release);
        }

        /// <param name="rank">Rank of the ability (1-based; 0 or 1 for spells without ranks).</param>
        public void Execute(Character caster, SpellDefinition spell, CastContext ctx, int rank) {
            if (caster == null || !caster.IsAlive) return;
            Vector3 origin = caster.transform.position;
            // A marking spell (marking spells) deals its damage when the mark is detonated, not on hit
            DamageInfo damage = spell.Mark.Enabled ? default : BuildDamage(caster, spell, rank);
            System.Action<Character, float> onHit = (target, taken) => OnSpellHit(caster, spell, rank, target, taken);

            switch (spell.Targeting) {
                case TargetingType.Self:
                    // A damaging self spell hits every enemy within Radius of the caster (nova)
                    if (spell.DealsDamage) HitAll(damage, InCircle(caster, origin, spell.Radius), onHit);
                    Ring(origin, Mathf.Max(spell.Radius, 1f), spell.Color);
                    break;

                case TargetingType.Skillshot: {
                    Vector3 muzzle = origin + Vector3.up * (caster.Definition.BodySize.y * 0.6f);
                    SkillshotProjectile projectile = _pool.Get(_settings.SkillshotProjectile);
                    projectile.Launch(muzzle, ctx.Direction, spell.Range, spell.ProjectileSpeed, spell.Radius, spell.Pierce,
                                      caster, damage, spell.Color, onHit, _pool.Release);
                    break;
                }

                case TargetingType.GroundPoint:
                    if (spell.Blink) {
                        if (TryGetBlinkPoint(origin, ctx.TargetPoint, out Vector3 destination)) {
                            Ring(origin, 0.8f, spell.Color);
                            caster.GetComponent<CharacterMovement>().Teleport(destination);
                            caster.Events.RaiseBlink(origin, destination);   // passives (Earthwake, Shadow Runner…)
                            Ring(destination, 0.8f, spell.Color);
                            if (spell.BlinkBoltRadius > 0f) FireBlinkBolt(caster, spell, destination, BuildDamage(caster, spell, rank), onHit);
                            // A damaging leap hits every enemy within Radius of where it lands
                            else if (spell.DealsDamage) {
                                HitAll(damage, InCircle(caster, destination, spell.Radius), onHit);
                                Ring(destination, spell.Radius, spell.Color);
                            }
                        }
                        break;
                    }
                    if (spell.DealsDamage) HitAll(damage, InCircle(caster, ctx.TargetPoint, spell.Radius), onHit);
                    Ring(ctx.TargetPoint, spell.Radius, spell.Color);
                    break;

                case TargetingType.Cone:
                    if (spell.DealsDamage) HitAll(damage, InCone(caster, origin, ctx.Direction, spell.Range, spell.ConeAngle), onHit);
                    SpellVfx cone = _pool.Get(_settings.ShapeVfx);
                    cone.PlayCone(origin, ctx.Direction, spell.Range, spell.ConeAngle, spell.Color, VfxDuration, _pool.Release);
                    break;

                case TargetingType.UnitTarget:
                    if (ctx.TargetUnit != null && ctx.TargetUnit.IsAlive) {
                        Vector3 muzzle = origin + Vector3.up * (caster.Definition.BodySize.y * 0.6f);
                        _projectiles.Launch(_settings.HomingProjectile, muzzle, ctx.TargetUnit, damage, spell.ProjectileSpeed, onHit, spell.Color);
                    }
                    break;
            }

            ApplyBuffs(caster, caster, spell);
            ApplySelfSustain(caster, caster, spell);
            if (spell.AffectsAllies) {
                foreach (Character ally in Allies(caster, origin, Mathf.Max(spell.Radius, 1f)).ToArray()) {
                    ApplyBuffs(caster, ally, spell);
                    ApplySelfSustain(caster, ally, spell);
                }
            }
            _eventBus.Publish(new SpellCastEvent(spell, caster.IsPlayer));
        }

        // Arcane Shift: a homing bolt at the nearest enemy around the landing spot, a marked one first
        private void FireBlinkBolt(Character caster, SpellDefinition spell, Vector3 from, DamageInfo damage, System.Action<Character, float> onHit) {
            Character best = null;
            float bestScore = float.MaxValue;
            foreach (Character c in InCircle(caster, from, spell.BlinkBoltRadius)) {
                Vector3 d = c.transform.position - from;
                d.y = 0f;
                float score = d.magnitude - (c.Status.IsMarkedBy(caster) ? 1000f : 0f);
                if (score < bestScore) {
                    bestScore = score;
                    best = c;
                }
            }
            if (best == null) return;
            Vector3 muzzle = from + Vector3.up * (caster.Definition.BodySize.y * 0.6f);
            _projectiles.Launch(_settings.HomingProjectile, muzzle, best, damage, spell.ProjectileSpeed, onHit, spell.Color);
        }

        /// <summary>
        /// A hit of <paramref name="attacker"/> (basic attack, or a spell other than the marking one)
        /// on a target it marked: the mark goes off for the marking spell's damage and gives back its
        /// mana (marking spells, §7.6).
        /// </summary>
        public static void TryDetonateMark(Character attacker, Character target, SpellDefinition hitBy) {
            if (attacker == null || target == null || !target.IsAlive) return;
            if (!target.Status.TryConsumeMark(attacker, hitBy, out SpellDefinition markSpell, out int rank)) return;
            target.TakeDamage(BuildDamage(attacker, markSpell, rank));
            attacker.Mana.Restore(markSpell.MarkManaRefund(rank));
        }

        private static DamageInfo BuildDamage(Character caster, SpellDefinition spell, int rank) {
            StatController stats = caster.Stats;
            float raw = SpellMath.RawDamage(spell, rank, stats.Value(StatType.PhysicalAttack), caster.BonusStat(StatType.PhysicalAttack),
                                            stats.Value(StatType.MagicAttack));
            DamageType type = spell.Kind == SpellKind.Physical ? DamageType.Physical : DamageType.Magic;
            // Spells only crit with the SpellCrit capability (passive, augment: design doc §5.6)
            bool crit = caster.Capabilities.Has(Passives.Capability.SpellCrit) && Random.value < stats.Value(StatType.CritChance);
            if (crit) raw *= Mathf.Max(1f, stats.Value(StatType.CritDamage));
            return new DamageInfo(raw, type, isCrit: crit, isBasicAttack: false, caster,
                stats.Value(StatType.ArmorPenFlat), stats.Value(StatType.ArmorPenPercent),
                stats.Value(StatType.MagicPenFlat), stats.Value(StatType.MagicPenPercent));
        }

        private static void ApplyBuffs(Character caster, Character target, SpellDefinition spell) {
            foreach (BuffEffect buff in spell.Buffs) {
                target.Buffs.Apply(target.Stats.Get(buff.Stat), buff.Value, buff.Type, spell, buff.Duration);
            }
        }

        private static void HitAll(DamageInfo damage, List<Character> targets, System.Action<Character, float> onHit) {
            // onHit can kill and pool things: iterate over a snapshot
            Character[] snapshot = targets.ToArray();
            foreach (Character target in snapshot) onHit(target, target.TakeDamage(damage));
        }

        private static void OnSpellHit(Character caster, SpellDefinition spell, int rank, Character target, float taken) {
            if (target == null || (taken <= 0f && !target.IsAlive)) return;   // already dead
            foreach (StatusApplication status in spell.Statuses) target.ApplyStatus(status, caster, spell);
            if (caster == null) return;
            if (spell.Mark.Enabled) target.Status.ApplyMark(caster, spell, rank, spell.Mark.Duration);
            else TryDetonateMark(caster, target, spell);
            caster.Events.RaiseSpellHit(target, taken);
            if (spell.AppliesOnHit) caster.Events.RaiseBasicAttackHit(target, taken);
            if (spell.CooldownRefundOnHit > 0f) caster.Events.RaiseCooldownRefund(spell.CooldownRefundOnHit);
        }

        private static void ApplySelfSustain(Character caster, Character target, SpellDefinition spell) {
            SelfSustain s = spell.SelfSustain;
            if (s.IsEmpty) return;
            float bonus = s.PowerRatio * caster.Stats.Value(StatType.MagicAttack);
            if (s.Heal > 0f || (s.PowerRatio > 0f && s.Shield <= 0f)) target.ReceiveHeal(s.Heal + bonus, caster);
            if (s.Shield > 0f) target.ReceiveShield(s.Shield + bonus, s.ShieldDuration, caster);
        }

        // Living allies of the caster (same faction, not itself) within radius
        private List<Character> Allies(Character caster, Vector3 center, float radius) {
            _targets.Clear();
            int count = Physics.OverlapSphereNonAlloc(center, radius + 2f, _hits);
            for (int i = 0; i < count; i++) {
                Character c = _hits[i].GetComponentInParent<Character>();
                if (c == null || c == caster || !c.IsAlive || c.Faction != caster.Faction || _targets.Contains(c)) continue;
                Vector3 d = c.transform.position - center;
                d.y = 0f;
                if (d.magnitude - c.BodyRadius <= radius) _targets.Add(c);
            }
            return _targets;
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
