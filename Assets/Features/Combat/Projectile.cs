using System;
using Features.Characters;
using UnityEngine;

namespace Features.Combat {
    /// <summary>
    /// Homing projectile of a ranged basic attack (LoL auto-attacks cannot miss): flies to the
    /// target's chest and applies the damage snapshotted at launch. Pooled by ProjectileLauncher.
    /// If the target dies in flight, the projectile vanishes.
    /// </summary>
    public class Projectile : MonoBehaviour {
        [Tooltip("Hit when this close to the aim point.")]
        [SerializeField] private float hitDistance = 0.2f;
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private SpriteRenderer sprite;

        private Character _target;
        private DamageInfo _damage;
        private float _speed;
        private Action<Projectile> _release;
        private Action<Character, float> _onHit;

        public void Launch(Vector3 from, Character target, in DamageInfo damage, float speed,
                           Action<Character, float> onHit, Action<Projectile> release, Color? tint = null) {
            transform.position = from;
            _target = target;
            _damage = damage;
            _speed = speed;
            _onHit = onHit;
            _release = release;
            if (trail != null) trail.Clear();
            ProjectileTint.Apply(sprite, trail, tint ?? Color.white);
        }

        private void Update() {
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

        private void Release() {
            _target = null;
            _onHit = null;
            Action<Projectile> release = _release;
            _release = null;
            release?.Invoke(this);
        }
    }
}
