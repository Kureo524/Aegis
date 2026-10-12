using System;
using System.Collections.Generic;
using Features.Characters;
using Features.Movement;
using Features.Stats;
using UnityEngine;
using VContainer;

namespace Features.Combat {
    /// <summary>
    /// LoL-style basic attack (design doc §4.5): Idle → Chasing → Windup → (impact) → Chasing…
    ///
    /// - Chasing: walks to the target until it is within AttackRange (center to center minus the
    ///   target's body radius, §4.6), refreshing the path a few times per second.
    /// - Windup: WindupPercent × interval before the hit; cancelling during the windup loses the
    ///   attack (a move order does). After the impact the character may move without losing cadence.
    /// - Interval = 1 / AttackSpeed. Damage = attack stat × DamageRatio, crit on CritChance.
    ///   Melee hits land at the impact; ranged attacks fire a homing projectile then.
    /// - Attack-move (§4.6): walk to a point, attacking whatever enemy comes within range on the way
    ///   (picked by an ITargetSelector); an acquired target that leaves the range is dropped and the
    ///   walk resumes. Ends on arrival.
    /// Range, speed and damage are stats, so they follow levels and modifiers.
    /// </summary>
    public class BasicAttacker : MonoBehaviour {
        [SerializeField] private Character owner;
        [SerializeField] private CharacterMovement movement;
        [SerializeField] private CharacterVisual visual;
        [Tooltip("Seconds between two path refreshes while chasing a moving target.")]
        [SerializeField] private float repathInterval = 0.2f;
        [Tooltip("Seconds between two enemy scans during an attack-move.")]
        [SerializeField] private float scanInterval = 0.1f;
        [Tooltip("Height of the projectile spawn point, as a share of the body height.")]
        [SerializeField] private float muzzleHeight = 0.6f;

        private enum State { Idle, Chasing, Windup }

        private ProjectileLauncher _projectiles;
        private readonly ITargetSelector _selector = new NearestTargetSelector();
        private readonly Collider[] _scanHits = new Collider[32];
        private readonly List<Character> _candidates = new();

        private State _state;
        private Character _target;
        private bool _targetIsAcquired;   // picked by the attack-move scan, not ordered explicitly
        private float _windupEnd;
        private float _nextAttackTime;
        private float _nextRepath;

        private bool _attackMoving;
        private Vector3 _attackMovePoint;
        private float _nextScan;

        public Character Target => _target;
        public bool IsAttacking => _state != State.Idle;
        public bool IsAttackMoving => _attackMoving;
        public bool IsWindingUp => _state == State.Windup;
        /// <summary>
        /// False: a target out of range is not chased, whoever drives the character moves it
        /// (enemy AI, §9.6). The attack resumes as soon as the target is back in range.
        /// </summary>
        public bool ChasesTarget { get; set; } = true;

        /// <summary>Raised on each landed basic attack (target, damage taken). CharacterEvents §8.1.</summary>
        public event Action<Character, float> Hit;

        [Inject]
        public void Construct(ProjectileLauncher projectiles) {
            _projectiles = projectiles;
        }

        private bool CanAttack => owner.Definition != null && owner.Definition.BasicAttack != null;

        /// <summary>Attack <paramref name="target"/> until it dies or another order comes.</summary>
        public bool Attack(Character target) {
            if (!CanAttack || target == null || !target.IsAlive || !owner.IsHostileTo(target)) return false;
            _attackMoving = false;
            SetTarget(target, acquired: false);
            return true;
        }

        /// <summary>
        /// Attack-move to <paramref name="point"/>. <paramref name="preferredTarget"/> (enemy picked
        /// near the click) is attacked first, like a right click.
        /// </summary>
        public bool AttackMove(Vector3 point, Character preferredTarget = null) {
            if (!CanAttack) return false;
            _attackMoving = true;
            _attackMovePoint = point;
            _nextScan = 0f;

            if (preferredTarget != null && preferredTarget.IsAlive && owner.IsHostileTo(preferredTarget)) {
                SetTarget(preferredTarget, acquired: false);
            }
            else {
                ClearTarget();
                movement.SetDestination(point);
            }
            return true;
        }

        /// <summary>Drops the target and any attack-move. A windup in progress is lost.</summary>
        public void Cancel() {
            _attackMoving = false;
            ClearTarget();
        }

        private void SetTarget(Character target, bool acquired) {
            if (target == _target && _state != State.Idle) return;
            _target = target;
            _targetIsAcquired = acquired;
            _state = State.Chasing;
            _nextRepath = 0f;
        }

        private void ClearTarget() {
            _target = null;
            _state = State.Idle;
        }

        private void Update() {
            if (!owner.IsAlive) {
                Cancel();
                return;
            }

            if (_state == State.Idle) {
                if (_attackMoving) UpdateAttackMoveWalk();
                return;
            }

            bool targetLost = _target == null || !_target.IsAlive;
            if (targetLost) {
                ClearTarget();
                if (_attackMoving) movement.SetDestination(_attackMovePoint);   // resume the walk
                else movement.Stop();
                return;
            }

            float range = owner.Stats.Value(StatType.AttackRange);
            bool inRange = DistanceTo(_target) <= range;

            switch (_state) {
                // An enemy acquired on the way is not chased: back to scanning (§4.6 step 4)
                case State.Chasing when !inRange && _targetIsAcquired:
                    ClearTarget();
                    movement.SetDestination(_attackMovePoint);
                    break;

                case State.Chasing when !inRange:
                    if (!ChasesTarget) break;
                    if (Time.time >= _nextRepath) {
                        movement.SetDestination(_target.transform.position);
                        _nextRepath = Time.time + repathInterval;
                    }
                    break;

                case State.Chasing:
                    if (movement.IsMoving) movement.Stop();
                    FaceTarget();
                    if (Time.time >= _nextAttackTime) StartWindup();
                    break;

                // The windup always completes: no target switch mid-attack (§4.6 step 3)
                case State.Windup when Time.time >= _windupEnd:
                    Impact();
                    _state = State.Chasing;
                    break;
            }
        }

        // No target during an attack-move: look for one, else keep walking, stop on arrival.
        private void UpdateAttackMoveWalk() {
            if (Time.time >= _nextScan) {
                _nextScan = Time.time + scanInterval;
                Character found = ScanForEnemy();
                if (found != null) {
                    SetTarget(found, acquired: true);
                    return;
                }
            }

            // Every walk phase starts with SetDestination, so a stopped walk is an arrived one
            // (also when the clicked point was unreachable and got snapped to the NavMesh).
            if (!movement.IsMoving) _attackMoving = false;
        }

        private Character ScanForEnemy() {
            float range = owner.Stats.Value(StatType.AttackRange);
            // Margin so big bodies whose center is out of range still get found (range is to their edge)
            int count = Physics.OverlapSphereNonAlloc(owner.transform.position, range + 2f, _scanHits);

            _candidates.Clear();
            for (int i = 0; i < count; i++) {
                Character c = _scanHits[i].GetComponentInParent<Character>();
                if (c == null || !c.IsAlive || !owner.IsHostileTo(c) || _candidates.Contains(c)) continue;
                if (DistanceTo(c) <= range) _candidates.Add(c);
            }
            return _candidates.Count > 0 ? _selector.Select(owner, _candidates) : null;
        }

        private void StartWindup() {
            float interval = 1f / Mathf.Max(owner.Stats.Value(StatType.AttackSpeed), 0.01f);
            _windupEnd = Time.time + interval * owner.Definition.BasicAttack.WindupPercent;
            _nextAttackTime = Time.time + interval;
            _state = State.Windup;
        }

        private void Impact() {
            BasicAttackDefinition def = owner.Definition.BasicAttack;
            StatController stats = owner.Stats;

            float attack = def.DamageType == DamageType.Magic ? stats.Value(StatType.MagicAttack) : stats.Value(StatType.PhysicalAttack);
            bool crit = UnityEngine.Random.value < stats.Value(StatType.CritChance);
            float raw = attack * def.DamageRatio * (crit ? Mathf.Max(1f, stats.Value(StatType.CritDamage)) : 1f);

            var info = new DamageInfo(raw, def.DamageType, crit, isBasicAttack: true, owner,
                stats.Value(StatType.ArmorPenFlat), stats.Value(StatType.ArmorPenPercent),
                stats.Value(StatType.MagicPenFlat), stats.Value(StatType.MagicPenPercent));

            if (def.IsRanged) {
                Vector3 muzzle = owner.transform.position + Vector3.up * (owner.Definition.BodySize.y * muzzleHeight);
                _projectiles.Launch(def.ProjectilePrefab, muzzle, _target, info, def.ProjectileSpeed, OnHit);
            }
            else {
                OnHit(_target, _target.TakeDamage(info));
            }
        }

        private void OnHit(Character target, float taken) => Hit?.Invoke(target, taken);

        private float DistanceTo(Character other) {
            Vector3 delta = other.transform.position - owner.transform.position;
            delta.y = 0f;
            return delta.magnitude - other.BodyRadius;
        }

        private void FaceTarget() {
            if (visual != null) visual.Face(_target.transform.position - owner.transform.position);
        }
    }
}
