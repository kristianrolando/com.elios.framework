using UnityEngine;

namespace Elios.Framework.ObjectPooling
{
    // Runtime marker that links an instance to its pool via PoolKey and
    // tracks whether it currently sits inside the pool. Added automatically by the pool;
    // you normally never touch this directly.
    [DisallowMultipleComponent]
    public sealed class PoolableKey : MonoBehaviour
    {
        public int PoolKey { get; internal set; }
        public bool IsInPool { get; internal set; }

        // Cached IPoolable components (root + children) for spawn/return callbacks.
        internal IPoolable[] Poolables { get; set; }
    }
}
