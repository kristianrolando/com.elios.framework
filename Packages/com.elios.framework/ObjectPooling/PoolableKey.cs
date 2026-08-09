using UnityEngine;

namespace Game.Framework.ObjectPooling
{
    /// <summary>
    /// Runtime marker that links an instance to its pool via <see cref="PoolKey"/> and
    /// tracks whether it currently sits inside the pool. Added automatically by the pool;
    /// you normally never touch this directly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoolableKey : MonoBehaviour
    {
        public int PoolKey { get; internal set; }
        public bool IsInPool { get; internal set; }

        /// <summary>Cached IPoolable components (root + children) for spawn/return callbacks.</summary>
        internal IPoolable[] Poolables { get; set; }
    }
}
