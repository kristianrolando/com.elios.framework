namespace Game.Framework.ObjectPooling
{
    /// <summary>
    /// Implement on any component of a pooled prefab (root or children) to reset
    /// or initialize state as the instance is spawned from and returned to the pool.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>Called right after the instance is activated for reuse.</summary>
        void OnSpawn();

        /// <summary>Called right before the instance is deactivated and pooled.</summary>
        void OnReturnToPool();
    }
}
