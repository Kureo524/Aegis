using System;
using System.Collections.Generic;
using Features.Characters;
using UnityEngine;

namespace Features.Combat {
    /// <summary>
    /// Straight projectile of a skillshot spell (design doc §7.5): flies in a fixed direction up to
    /// its range and hits the first enemy it touches (or every enemy on its way when piercing).
    /// Can miss, unlike basic attack projectiles. Pooled through PrefabPool.
    /// </summary>
    public class SkillshotProjectile : MonoBehaviour {
        [SerializeField] private SpriteRenderer sprite;
        [SerializeField] private TrailRenderer trail;

        private readonly Collider[] _hits = new Collider[16];
        private readonly HashSet<Character> _alreadyHit = new();
        private Character _caster;
        private DamageInfo _damage;
        private Vector3 _direction;
        private float _speed, _remaining, _radius;
        private bool _pierce;
        private Action<Character, float> _onHit;
        private Action<Component> _release;

        public void Launch(Vector3 from, Vector3 direction, float range, float speed, float radius, bool pierce,
                           Character caster, in DamageInfo damage, Color color,
                           Action<Character, float> onHit, Action<Component> release) {
            transform.position = from;
            _direction = direction;
            _remaining = range;
            _speed = speed;
            _radius = Mathf.Max(0.1f, radius);
            _pierce = pierce;
            _caster = caster;
            _damage = damage;
            _onHit = onHit;
            _release = release;
            _alreadyHit.Clear();
            if (trail != null) trail.Clear();
            ProjectileTint.Apply(sprite, trail, color);
        }

        private void Update() {
            float step = Mathf.Min(_speed * Time.deltaTime, _remaining);
            transform.position += _direction * step;
            _remaining -= step;

            int count = Physics.OverlapSphereNonAlloc(transform.position, _radius, _hits);
            for (int i = 0; i < count; i++) {
                Character c = _hits[i].GetComponentInParent<Character>();
                if (c == null || !c.IsAlive || _caster == null || !_caster.IsHostileTo(c) || !_alreadyHit.Add(c)) continue;

                float taken = c.TakeDamage(_damage);
                _onHit?.Invoke(c, taken);
                if (!_pierce) {
                    Release();
                    return;
                }
            }

            if (_remaining <= 0f) Release();
        }

        private void Release() {
            _onHit = null;
            Action<Component> release = _release;
            _release = null;
            release?.Invoke(this);
        }
    }
}
