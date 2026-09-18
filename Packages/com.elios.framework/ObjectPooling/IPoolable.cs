namespace Game.Framework.ObjectPooling
{
    // Implement on any component of a pooled prefab (root or children) to reset
    // or initialize state as the instance is spawned from and returned to the pool.
    public interface IPoolable
    {
        // Called right after the instance is activated for reuse.
        void OnSpawn();

        // Called right before the instance is deactivated and pooled.
        void OnReturnToPool();
    }
}
