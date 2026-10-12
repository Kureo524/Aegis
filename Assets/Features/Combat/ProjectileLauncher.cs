using System;
using System.Collections.Generic;
using Features.Characters;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using VContainer.Unity;

namespace Features.Combat {
    /// <summary>
    /// Fires pooled projectiles (design doc §9.8: pooling from the start). One pool per prefab;
    /// instances are created through the container so they get injected. Game scope service:
    /// destroyed with the scene, like every projectile in it.
    /// </summary>
    public sealed class ProjectileLauncher : IDisposable {
        private readonly IObjectResolver _resolver;
        private readonly Dictionary<Projectile, ObjectPool<Projectile>> _pools = new();

        public ProjectileLauncher(IObjectResolver resolver) {
            _resolver = resolver;
        }

        public void Launch(Projectile prefab, Vector3 from, Character target, in DamageInfo damage, float speed,
                           Action<Character, float> onHit) {
            ObjectPool<Projectile> pool = GetPool(prefab);
            Projectile projectile = pool.Get();
            projectile.Launch(from, target, damage, speed, onHit, pool.Release);
        }

        public void Dispose() {
            foreach (ObjectPool<Projectile> pool in _pools.Values) pool.Dispose();
            _pools.Clear();
        }

        private ObjectPool<Projectile> GetPool(Projectile prefab) {
            if (_pools.TryGetValue(prefab, out ObjectPool<Projectile> pool)) return pool;

            pool = new ObjectPool<Projectile>(
                createFunc: () => _resolver.Instantiate(prefab),
                actionOnGet: p => p.gameObject.SetActive(true),
                actionOnRelease: p => p.gameObject.SetActive(false),
                actionOnDestroy: p => { if (p != null) UnityEngine.Object.Destroy(p.gameObject); },
                collectionCheck: false,
                defaultCapacity: 16);
            _pools[prefab] = pool;
            return pool;
        }
    }
}
