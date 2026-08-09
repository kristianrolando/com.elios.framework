namespace Game.Framework.SaveSystem
{
    /// <summary>
    /// Upgrades a loaded database from one schema version to the next.
    /// One migration bumps data from <see cref="FromVersion"/> to FromVersion + 1.
    /// </summary>
    public interface ISaveMigration
    {
        /// <summary>Schema version this migration expects as input.</summary>
        int FromVersion { get; }

        /// <summary>Mutates <paramref name="database"/> in place to the next schema version.</summary>
        void Apply(SaveDatabase database);
    }
}
