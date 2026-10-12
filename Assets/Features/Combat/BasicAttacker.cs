using System;
using Features.Characters;
using Features.Movement;
using Features.Stats;
using UnityEngine;

namespace Features.Combat {
    /// <summary>
    /// LoL-style basic attack (design doc §4.5): Idle → Chasing → Windup → (impact) → Chasing…
    ///
    /// - Chasing: walks to the target until it is within AttackRange (center to center minus the
    ///   target's body radius, §4.6), refreshing the path a few times per second.
    /// - Windup: WindupPercent × interval before the hit; cancelling during the windup loses the
    ///   attack (a move order does). After the impact the character may move without losing cadence.
    /// - Interval = 1 / AttackSpeed. Damage = attack stat × DamageRatio, crit on CritChance.
    /// Range, speed and damage are stats, so they follow levels and modifiers.
    /// </summary>
    public class BasicAttacker : MonoBehaviour {
        [SerializeField] private Character owner;
        [SerializeField] private CharacterMovement movement;
        [SerializeField] private CharacterVisual visual;
        [Tooltip("Seconds between two path refreshes while chasing a moving target.")]
        [SerializeField] private float repathInterval = 0.2f;

        private enum State { Idle, Chasing, Windup }

        private State _state;
        private Character _target;
        private float _windupEnd;
        private float _nextAttackTime;
        private float _nextRepath;

        public Character Target => _target;
        public bool IsAttacking => _state != State.Idle;

        /// <summary>Raised on each landed basic attack (target, damage taken). CharacterEvents §8.1.</summary>
        public event Action<Character, float> Hit;

        /// <summary>Attack <paramref name="target"/> until it dies or another order comes.</summary>
        public bool Attack(Character target) {
            if (owner.Definition == null || owner.Definition.BasicAttack == null) return false;
            if (target == null || !target.IsAlive || !owner.IsHostileTo(target)) return false;
            if (target == _target && _state != State.Idle) return true;

            _target = target;
            _state = State.Chasing;
            _nextRepath = 0f;
            return true;
        }

        /// <summary>Drops the target. A windup in progress is lost.</summary>
        public void Cancel() {
            _target = null;
            _state = State.Idle;
        }

        private void Update() {
            if (_state == State.Idle) return;

            if (_target == null || !_target.IsAlive || !owner.IsAlive) {
                if (_state == State.Chasing) movement.Stop();
                Cancel();
                return;
            }

            bool inRange = DistanceToTarget() <= owner.Stats.Value(StatType.AttackRange);

            switch (_state) {
                case State.Chasing when !inRange:
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

                case State.Windup when Time.time >= _windupEnd:
                    Impact();
                    _state = State.Chasing;
                    break;
            }
        }

        private void StartWindup() {
            float interval = 1f / Mathf.Max(owner.Stats.Value(StatType.AttackSpeed), 0.01f);
            _windupEnd = Time.time + interval * owner.Definition.BasicAttack.WindupPercent;
            _nextAttackTime = Time.time + interval;
            _state = State.Windup;
        }

        // TODO: ranged attacks with a ProjectilePrefab travel to the target instead of hitting instantly.
        private void Impact() {
            BasicAttackDefinition def = owner.Definition.BasicAttack;
            StatController stats = owner.Stats;

            float attack = def.DamageType == DamageType.Magic ? stats.Value(StatType.MagicAttack) : stats.Value(StatType.PhysicalAttack);
            bool crit = UnityEngine.Random.value < stats.Value(StatType.CritChance);
            float raw = attack * def.DamageRatio * (crit ? Mathf.Max(1f, stats.Value(StatType.CritDamage)) : 1f);

            var info = new DamageInfo(raw, def.DamageType, crit, isBasicAttack: true, owner,
                stats.Value(StatType.ArmorPenFlat), stats.Value(StatType.ArmorPenPercent),
                stats.Value(StatType.MagicPenFlat), stats.Value(StatType.MagicPenPercent));

            float taken = _target.TakeDamage(info);
            Hit?.Invoke(_target, taken);
        }

        private float DistanceToTarget() {
            Vector3 delta = _target.transform.position - owner.transform.position;
            delta.y = 0f;
            return delta.magnitude - _target.BodyRadius;
        }

        private void FaceTarget() {
            if (visual != null) visual.Face(_target.transform.position - owner.transform.position);
        }
    }
}
