using System.Collections.Generic;

namespace Elios.Framework.SaveSystem
{
    // Runs registered schema migrations when a slot is loaded, stepping the database
    // forward one version at a time until it matches the current schema version.
    // Register future migrations in Migrations.
    public static class SaveMigrator
    {
        // Ordered by FromVersion. Add a new ISaveMigration here whenever the schema changes.
        private static readonly List<ISaveMigration> Migrations = new List<ISaveMigration>
        {
            // Example: new Migration_1_To_2(),
        };

        // Brings the database up to SaveConstants.CurrentSchemaVersion.
        // Returns true if anything changed, so the caller can persist the upgrade.
        public static bool Migrate(SaveDatabase database)
        {
            if (database?.meta == null)
                return false;

            bool changed = false;

            while (database.meta.schemaVersion < SaveConstants.CurrentSchemaVersion)
            {
                int from = database.meta.schemaVersion;
                ISaveMigration migration = FindMigration(from);

                if (migration == null)
                {
                    // No migration path defined: adopt the current version to avoid a loop.
                    // Existing values are kept as-is.
                    SaveLog.Warning($"[SaveMigrator] No migration from schema v{from} to v{from + 1}. Marking as current (v{SaveConstants.CurrentSchemaVersion}).");
                    database.meta.schemaVersion = SaveConstants.CurrentSchemaVersion;
                    changed = true;
                    break;
                }

                migration.Apply(database);
                database.meta.schemaVersion = from + 1;
                changed = true;
            }

            return changed;
        }

        private static ISaveMigration FindMigration(int fromVersion)
        {
            for (int i = 0; i < Migrations.Count; i++)
            {
                if (Migrations[i] != null && Migrations[i].FromVersion == fromVersion)
                    return Migrations[i];
            }

            return null;
        }
    }
}
