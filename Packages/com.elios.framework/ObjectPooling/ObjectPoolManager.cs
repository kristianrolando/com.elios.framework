using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elios.Framework.ObjectPooling
{
    // Global facade for pooling.
    // - Automatically creates a pool per prefab on first use (no setup required).
    // - Clears all pools automatically on active scene change.
    public static class ObjectPoolManager
    {
        private const string PoolRootName = "__ObjectPoolRoot__";

        // Reserve cap a pool starts out with before a project overrides DefaultMaxSize.
        private const int BuiltInDefaultMaxSize = 200;

        private static readonly Dictionary<int, ObjectPool> _pools = new();
        private static Transform _poolRoot;
        private static bool _subscribed;
        private static int _defaultMaxSize = BuiltInDefaultMaxSize;

        // ══════════════════════════════════════════════
        // Lifecycle
        // ══════════════════════════════════════════════

        // Runs on every Play start, even when "Enter Play Mode without Domain Reload" is on,
        // so stale static state from a previous session never leaks in.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _pools.Clear();
            _poolRoot = null;
            _subscribed = false;
            _defaultMaxSize = BuiltInDefaultMaxSize;
        }

        // ══════════════════════════════════════════════
        // Public API
        // ══════════════════════════════════════════════

        // Reserve cap used whenever a pool is created without an explicit maxSize, for prefab
        // pools and ClassPool{T} alike. Set it once at startup; a value below 1 is
        // clamped, since a pool has to be able to hold something.
        public static int DefaultMaxSize
        {
            get => _defaultMaxSize;
            set => _defaultMaxSize = Mathf.Max(1, value);
        }

        // Pre-creates a pool (and optionally prewarms it) for a prefab. Optional.
        // A maxSize of 0 or less means "use DefaultMaxSize".
        public static void RegisterPrefab(GameObject prefab, int initialSize = 0, int maxSize = 0)
        {
            if (prefab == null)
                return;

            EnsureSubscribed();

            if (prefab.scene.IsValid())
                EditorDebug.Warning($"[{nameof(ObjectPoolManager)}] '{prefab.name}' looks like a scene object, not a Prefab Asset. Register a Prefab Asset instead.");

            int key = prefab.GetInstanceID();
            if (_pools.ContainsKey(key))
                return;

            int reserveCap = maxSize > 0 ? maxSize : _defaultMaxSize;
            _pools[key] = new ObjectPool(prefab, initialSize, reserveCap, EnsureRoot());
        }

        public static GameObject Get(GameObject prefab, Vector3 position, Transform parent = null)
        {
            return Get(prefab, position, Quaternion.identity, parent);
        }

        public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null)
                return null;

            EnsureSubscribed();

            int key = prefab.GetInstanceID();
            if (!_pools.TryGetValue(key, out ObjectPool pool))
            {
                RegisterPrefab(prefab);
                pool = _pools[key];
            }

            return pool.Get(position, rotation, parent);
        }

        public static void Return(GameObject instance)
        {
            if (instance == null)
                return;

            if (!instance.TryGetComponent(out PoolableKey key))
            {
                // Not a pooled object → destroy.
                Object.Destroy(instance);
                return;
            }

            if (_pools.TryGetValue(key.PoolKey, out ObjectPool pool))
                pool.Return(instance);
            else
                Object.Destroy(instance); // Pool no longer exists (e.g., cleared) → destroy.
        }

        public static bool HasPool(GameObject prefab)
        {
            return prefab != null && _pools.ContainsKey(prefab.GetInstanceID());
        }

        public static void ClearPoolForPrefab(GameObject prefab)
        {
            if (prefab == null)
                return;

            int key = prefab.GetInstanceID();
            if (_pools.TryGetValue(key, out ObjectPool pool))
            {
                pool.Clear();
                _pools.Remove(key);
            }
        }

        public static void ClearAll()
        {
            foreach (ObjectPool pool in _pools.Values)
                pool.Clear();

            _pools.Clear();
        }

        // Editor-only dump of per-pool counters, useful for spotting leaks.
        public static void LogStats()
        {
            foreach (KeyValuePair<int, ObjectPool> kv in _pools)
                EditorDebug.Value("[ObjectPool] active/reserve", $"{kv.Value.ActiveCount}/{kv.Value.Count} (rented {kv.Value.TotalRented}, returned {kv.Value.TotalReturned})");
        }

        // ══════════════════════════════════════════════
        // Internal Helpers
        // ══════════════════════════════════════════════

        private static void EnsureSubscribed()
        {
            if (_subscribed)
                return;

            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            _subscribed = true;
        }

        private static Transform EnsureRoot()
        {
            if (_poolRoot == null)
                _poolRoot = new GameObject(PoolRootName).transform;

            return _poolRoot;
        }

        private static void OnActiveSceneChanged(Scene from, Scene to)
        {
            ClearAll();
            _poolRoot = null; // Old root was destroyed together with the previous scene.
        }
    }
}
