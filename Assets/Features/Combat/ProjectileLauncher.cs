using System;
using Features.Characters;
using Systems.Pooling;
using UnityEngine;

namespace Features.Combat {
    /// <summary>Fires pooled basic attack projectiles, homing or straight (basic attacks, unit-target spells). Game scope service.</summary>
    public sealed class ProjectileLauncher {
        private readonly PrefabPool _pool;

        public ProjectileLauncher(PrefabPool pool) {
            _pool = pool;
        }

        public void Launch(Projectile prefab, Vector3 from, Character target, in DamageInfo damage, float speed,
                           Action<Character, float> onHit, Color? tint = null) {
            Projectile projectile = _pool.Get(prefab);
            projectile.Launch(from, target, damage, speed, onHit, _pool.Release, tint);
        }

        /// <summary>Straight shot that can be dodged (see Projectile.LaunchStraight).</summary>
        public void LaunchStraight(Projectile prefab, Vector3 from, Vector3 aim, float range, in DamageInfo damage, float speed,
                                   Action<Character, float> onHit, Color? tint = null) {
            Projectile projectile = _pool.Get(prefab);
            projectile.LaunchStraight(from, aim, range, damage, speed, onHit, _pool.Release, tint);
        }
    }
}
