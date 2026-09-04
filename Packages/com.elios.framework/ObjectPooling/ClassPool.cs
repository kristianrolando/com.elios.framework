using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Game.Framework.ObjectPooling
{
    // Reserve of plain C# instances, for what a prefab pool cannot hold: runtime state objects,
    // command/request objects, working buffers. Same counters and the same IPoolable reset
    // contract as ObjectPool, so "reset what you mutate" means one thing across the subsystem.
    //
    // For collections, use Unity's UnityEngine.Pool (ListPool<T>, HashSetPool<T>,
    // DictionaryPool<K, V>) rather than wrapping one here.
    public sealed class ClassPool<T> where T : class
    {
        private readonly Func<T> _factory;
        private readonly Queue<T> _reserve;

        // Membership is by reference, never by Equals: two instances that compare equal are still
        // two objects, and a pool that confuses them hands the same one to two owners.
        private readonly HashSet<T> _pooled;

        private readonly int _maxSize;

        public int Count => _reserve.Count;
        public int MaxSize => _maxSize;
        public int TotalRented { get; private set; }
        public int TotalReturned { get; private set; }

        // Rough number of instances currently rented out. A value that only climbs is a leak.
        public int ActiveCount => TotalRented - TotalReturned;

        // ══════════════════════════════════════════════
        // Construction
        // ══════════════════════════════════════════════

        // maxSize of 0 or less falls back to ObjectPoolManager.DefaultMaxSize, so one project-level
        // knob governs both pool kinds.
        public ClassPool(Func<T> factory, int initialSize = 0, int maxSize = 0)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _factory = factory;
            _maxSize = maxSize > 0 ? maxSize : ObjectPoolManager.DefaultMaxSize;
            _reserve = new Queue<T>(_maxSize);
            _pooled = new HashSet<T>(ReferenceComparer.Instance);

            Prewarm(initialSize);
        }

        // ══════════════════════════════════════════════
        // Public API
        // ══════════════════════════════════════════════

        public T Get()
        {
            T instance = _reserve.Count > 0 ? Dequeue() : Create();

            if (instance == null)
                return null;

            if (instance is IPoolable poolable)
                poolable.OnSpawn();

            TotalRented++;

            return instance;
        }

        public void Return(T instance)
        {
            if (instance == null)
                return;

            // Already in the reserve → ignore, otherwise one instance is handed to two owners.
            if (_pooled.Contains(instance))
                return;

            if (instance is IPoolable poolable)
                poolable.OnReturnToPool();

            TotalReturned++;

            // Reserve is full → drop the surplus and let the GC take it.
            if (_reserve.Count >= _maxSize)
                return;

            _reserve.Enqueue(instance);
            _pooled.Add(instance);
        }

        // Empties the reserve. Counters are kept so a leak stays visible across a clear.
        public void Clear()
        {
            _reserve.Clear();
            _pooled.Clear();
        }

        // ══════════════════════════════════════════════
        // Internal Helpers
        // ══════════════════════════════════════════════

        private void Prewarm(int initialSize)
        {
            // Never prewarm past the cap; the extras would be created only to be dropped again.
            int count = Mathf.Clamp(initialSize, 0, _maxSize);

            if (initialSize > _maxSize)
                EditorDebug.Warning($"[{nameof(ClassPool<T>)}] initialSize ({initialSize}) exceeds maxSize ({_maxSize}) for '{typeof(T).Name}'. Clamped to {_maxSize}.");

            for (int i = 0; i < count; i++)
            {
                T instance = Create();
                if (instance == null)
                    return;

                _reserve.Enqueue(instance);
                _pooled.Add(instance);
            }
        }

        private T Create()
        {
            T instance = _factory();

            // A null here poisons the pool and only surfaces far from its cause, so it is reported
            // even in a build.
            if (instance == null)
                Debug.LogError($"[{nameof(ClassPool<T>)}] The factory for '{typeof(T).Name}' returned null.");

            return instance;
        }

        private T Dequeue()
        {
            T instance = _reserve.Dequeue();
            _pooled.Remove(instance);
            return instance;
        }

        // netstandard has no ReferenceEqualityComparer, so this is the smallest stand-in.
        private sealed class ReferenceComparer : IEqualityComparer<T>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();

            public bool Equals(T x, T y) => ReferenceEquals(x, y);

            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
