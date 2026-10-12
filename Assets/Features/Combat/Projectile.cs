using System;
using Features.Characters;
using UnityEngine;

namespace Features.Combat {
    /// <summary>
    /// Projectile of a ranged basic attack, with the damage snapshotted at launch. Pooled by
    /// ProjectileLauncher.
    /// - Homing (LoL auto-attacks cannot miss): flies to the target's chest; vanishes if the target
    ///   dies in flight.
    /// - Straight (<see cref="LaunchStraight"/>, enemy archers): flies towards where the target was,
    ///   hits the first enemy of the shooter on its way, vanishes after its range: can be dodged.
    /// </summary>
    public class Projectile : MonoBehaviour {
        [Tooltip("Hit when this close to the aim point.")]
        [SerializeField] private float hitDistance = 0.2f;
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private SpriteRenderer sprite;
        [Tooltip("Straight mode: hit radius.")]
        [SerializeField] private float straightRadius = 0.35f;

        private Character _target;
        private DamageInfo _damage;
        private float _speed;
        private Action<Projectile> _release;
        private Action<Character, float> _onHit;
        private bool _straight;
        private Vector3 _direction;
        private float _remaining;
        private readonly Collider[] _hits = new Collider[16];

        public void Launch(Vector3 from, Character target, in DamageInfo damage, float speed,
                           Action<Character, float> onHit, Action<Projectile> release, Color? tint = null) {
            transform.position = from;
            _target = target;
            _damage = damage;
            _speed = speed;
            _onHit = onHit;
            _release = release;
            _straight = false;
            if (trail != null) trail.Clear();
            ProjectileTint.Apply(sprite, trail, tint ?? Color.white);
        }

        /// <summary>Straight shot towards <paramref name="aim"/>, up to <paramref name="range"/>.</summary>
        public void LaunchStraight(Vector3 from, Vector3 aim, float range, in DamageInfo damage, float speed,
                                   Action<Character, float> onHit, Action<Projectile> release, Color? tint = null) {
            Launch(from, null, damage, speed, onHit, release, tint);
            _straight = true;
            Vector3 direction = aim - from;
            direction.y = 0f;   // level flight at the muzzle height
            _direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            _remaining = range;
        }

        private void Update() {
            if (_straight) {
                UpdateStraight();
                return;
            }
            if (_target == null || !_target.IsAlive) {
                Release();
                return;
            }

            Vector3 aim = _target.transform.position + Vector3.up * (_target.Definition.BodySize.y * 0.6f);
            Vector3 toAim = aim - transform.position;
            float step = _speed * Time.deltaTime;

            if (toAim.magnitude <= Mathf.Max(step, hitDistance)) {
                float taken = _target.TakeDamage(_damage);
                _onHit?.Invoke(_target, taken);
                Release();
                return;
            }

            transform.position += toAim.normalized * step;
        }

        private void UpdateStraight() {
            float step = Mathf.Min(_speed * Time.deltaTime, _remaining);
            transform.position += _direction * step;
            _remaining -= step;

            Character shooter = _damage.Source;
            int count = Physics.OverlapSphereNonAlloc(transform.position, straightRadius, _hits);
            for (int i = 0; i < count; i++) {
                Character c = _hits[i].GetComponentInParent<Character>();
                if (c == null || !c.IsAlive || shooter == null || !shooter.IsHostileTo(c)) continue;
                float taken = c.TakeDamage(_damage);
                _onHit?.Invoke(c, taken);
                Release();
                return;
            }
            if (_remaining <= 0f) Release();
        }

        private void Release() {
            _target = null;
            _straight = false;
            _onHit = null;
            Action<Projectile> release = _release;
            _release = null;
            release?.Invoke(this);
        }
    }
}
