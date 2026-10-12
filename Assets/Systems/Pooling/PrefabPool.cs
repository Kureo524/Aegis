using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Systems.Pooling {
    /// <summary>
    /// Pools instances of any prefab (design doc §9.8: pooling from the start). One ObjectPool per
    /// prefab; instances are created through the container so they get injected, and remember
    /// their pool so they can be released without knowing where they came from.
    /// Scene-scoped service: everything is destroyed with the scene.
    /// </summary>
    public sealed class PrefabPool : IDisposable {
        private readonly IObjectResolver _resolver;
        private readonly Dictionary<Component, ObjectPool<Component>> _pools = new();
        private readonly Dictionary<Component, ObjectPool<Component>> _owners = new();

        public PrefabPool(IObjectResolver resolver) {
            _resolver = resolver;
        }

        public T Get<T>(T prefab) where T : Component {
            if (!_pools.TryGetValue(prefab, out ObjectPool<Component> pool)) {
                pool = new ObjectPool<Component>(
                    createFunc: () => _resolver.Instantiate(prefab),
                    actionOnGet: c => c.gameObject.SetActive(true),
                    actionOnRelease: c => c.gameObject.SetActive(false),
                    actionOnDestroy: c => { if (c != null) Object.Destroy(c.gameObject); },
                    collectionCheck: false,
                    defaultCapacity: 16);
                _pools[prefab] = pool;
            }

            var instance = (T)pool.Get();
            _owners[instance] = pool;
            return instance;
        }

        public void Release(Component instance) {
            if (instance != null && _owners.TryGetValue(instance, out ObjectPool<Component> pool)) pool.Release(instance);
        }

        public void Dispose() {
            foreach (ObjectPool<Component> pool in _pools.Values) pool.Dispose();
            _pools.Clear();
            _owners.Clear();
        }
    }
}
