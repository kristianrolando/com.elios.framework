namespace Elios.Framework.SaveSystem
{
    // Upgrades a loaded database from one schema version to the next.
    // One migration bumps data from FromVersion to FromVersion + 1.
    public interface ISaveMigration
    {
        // Schema version this migration expects as input.
        int FromVersion { get; }

        // Mutates database in place to the next schema version.
        void Apply(SaveDatabase database);
    }
}
