using System.Collections.Generic;
using UnityEngine;

namespace Game.Framework.ObjectPooling
{
    /// <summary>
    /// Simple per-prefab object pool.
    /// - One pool per prefab (key = prefab.GetInstanceID()).
    /// - Prewarms via initialSize; caps the reserve at maxSize.
    /// - Notifies IPoolable components on spawn/return.
    /// - Tracks TotalRented / TotalReturned / ActiveCount for diagnostics.
    /// </summary>
    public sealed class ObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Queue<GameObject> _pool;
        private readonly int _maxSize;
        private readonly Transform _rootParent;

        public int PoolKey => _prefab.GetInstanceID();
        public int Count => _pool.Count;
        public int TotalRented { get; private set; }
        public int TotalReturned { get; private set; }

        /// <summary>Estimated number of instances currently rented out (not yet returned).</summary>
        public int ActiveCount => TotalRented - TotalReturned;

        // ══════════════════════════════════════════════
        // Construction
        // ══════════════════════════════════════════════

        public ObjectPool(GameObject prefab, int initialSize, int maxSize, Transform rootParent)
        {
            _prefab = prefab;
            _maxSize = Mathf.Max(1, maxSize);
            _rootParent = rootParent;
            _pool = new Queue<GameObject>(_maxSize);

            Prewarm(initialSize);
        }

        // ══════════════════════════════════════════════
        // Public API
        // ══════════════════════════════════════════════

        public GameObject Get(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            GameObject instance = DequeueValid() ?? CreateInstance();

            PoolableKey key = EnsureMarker(instance);
            key.IsInPool = false;

            Transform targetParent = parent != null ? parent : _rootParent;
            Transform tr = instance.transform;
            tr.SetParent(targetParent, false);
            tr.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            NotifyPoolables(key, spawned: true);
            TotalRented++;

            return instance;
        }

        public void Return(GameObject instance)
        {
            if (instance == null)
                return;

            if (!instance.TryGetComponent(out PoolableKey key))
            {
                // Not a pooled object → just destroy.
                Object.Destroy(instance);
                return;
            }

            // Wrong pool → destroy (likely returned via the wrong manager/pool).
            if (key.PoolKey != PoolKey)
            {
                Object.Destroy(instance);
                return;
            }

            // Already in pool → ignore (prevents double-enqueue).
            if (key.IsInPool)
                return;

            NotifyPoolables(key, spawned: false);
            key.IsInPool = true;
            TotalReturned++;

            if (_rootParent != null)
                instance.transform.SetParent(_rootParent, false);

            if (_pool.Count < _maxSize)
            {
                instance.SetActive(false);
                _pool.Enqueue(instance);
            }
            else
            {
                // Reserve is full → discard the surplus instance.
                Object.Destroy(instance);
            }
        }

        public void Clear()
        {
            while (_pool.Count > 0)
            {
                GameObject inst = _pool.Dequeue();
                if (inst != null)
                    Object.Destroy(inst);
            }
        }

        // ══════════════════════════════════════════════
        // Internal Helpers
        // ══════════════════════════════════════════════

        private void Prewarm(int initialSize)
        {
            // Never prewarm beyond the reserve cap; extras would be created then destroyed.
            int count = Mathf.Clamp(initialSize, 0, _maxSize);
            if (initialSize > _maxSize)
                EditorDebug.Warning($"[ObjectPool] initialSize ({initialSize}) exceeds maxSize ({_maxSize}) for '{_prefab.name}'. Clamped to {_maxSize}.");

            for (int i = 0; i < count; i++)
            {
                GameObject inst = CreateInstance();
                PoolableKey key = EnsureMarker(inst);
                key.IsInPool = true;
                _pool.Enqueue(inst);
            }
        }

        private GameObject DequeueValid()
        {
            // Skip instances that were destroyed externally while sitting in the pool.
            while (_pool.Count > 0)
            {
                GameObject inst = _pool.Dequeue();
                if (inst != null)
                    return inst;
            }

            return null;
        }

        private GameObject CreateInstance()
        {
            GameObject inst = Object.Instantiate(_prefab);

            if (_rootParent != null)
                inst.transform.SetParent(_rootParent, false);

            PoolableKey key = EnsureMarker(inst);
            key.IsInPool = false;
            inst.SetActive(false);

            return inst;
        }

        private PoolableKey EnsureMarker(GameObject instance)
        {
            if (!instance.TryGetComponent(out PoolableKey key))
                key = instance.AddComponent<PoolableKey>();

            key.PoolKey = PoolKey;
            key.Poolables ??= instance.GetComponentsInChildren<IPoolable>(true);

            return key;
        }

        private static void NotifyPoolables(PoolableKey key, bool spawned)
        {
            IPoolable[] poolables = key.Poolables;
            if (poolables == null)
                return;

            for (int i = 0; i < poolables.Length; i++)
            {
                if (poolables[i] == null)
                    continue;

                if (spawned)
                    poolables[i].OnSpawn();
                else
                    poolables[i].OnReturnToPool();
            }
        }
    }
}
