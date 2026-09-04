using NUnit.Framework;

namespace Game.Framework.SaveSystem
{
    public class SaveMigratorTests
    {
        private const string SampleKey = "progress/level";

        // ══════════════════════════════════════════════
        // Guards
        // ══════════════════════════════════════════════

        [Test]
        public void Migrate_NullDatabase_ReturnsFalse()
        {
            Assert.IsFalse(SaveMigrator.Migrate(null));
        }

        [Test]
        public void Migrate_DatabaseWithoutMeta_ReturnsFalse()
        {
            var database = new SaveDatabase { meta = null };

            Assert.IsFalse(SaveMigrator.Migrate(database));
        }

        // ══════════════════════════════════════════════
        // Stepping
        // ══════════════════════════════════════════════

        [Test]
        public void Migrate_AlreadyCurrent_ReportsNoChange()
        {
            SaveDatabase database = BuildDatabase(SaveConstants.CurrentSchemaVersion);

            Assert.IsFalse(SaveMigrator.Migrate(database));
            Assert.AreEqual(SaveConstants.CurrentSchemaVersion, database.meta.schemaVersion);
        }

        [Test]
        public void Migrate_OlderSchema_AdoptsTheCurrentVersion()
        {
            SaveDatabase database = BuildDatabase(SaveConstants.CurrentSchemaVersion - 1);

            Assert.IsTrue(SaveMigrator.Migrate(database), "an outdated database should report a change");
            Assert.AreEqual(SaveConstants.CurrentSchemaVersion, database.meta.schemaVersion);
        }

        [Test]
        public void Migrate_OlderSchema_KeepsExistingEntries()
        {
            // With no migration registered the version is adopted as-is; values must survive that,
            // otherwise adopting a version would silently wipe a player's progress.
            SaveDatabase database = BuildDatabase(SaveConstants.CurrentSchemaVersion - 1);
            database.entries[SampleKey] = SaveEntry.Create("42", typeof(string));

            SaveMigrator.Migrate(database);

            Assert.IsTrue(database.entries.ContainsKey(SampleKey));
            Assert.AreEqual("42", database.entries[SampleKey].value);
        }

        [Test]
        public void Migrate_Twice_ReportsNoChangeTheSecondTime()
        {
            SaveDatabase database = BuildDatabase(SaveConstants.CurrentSchemaVersion - 1);

            SaveMigrator.Migrate(database);

            Assert.IsFalse(SaveMigrator.Migrate(database), "migration is not idempotent");
        }

        [Test]
        public void Migrate_SchemaNewerThanThisBuild_IsLeftUntouched()
        {
            // A save written by a newer build must not be downgraded or rewritten here.
            int futureVersion = SaveConstants.CurrentSchemaVersion + 1;
            SaveDatabase database = BuildDatabase(futureVersion);

            Assert.IsFalse(SaveMigrator.Migrate(database));
            Assert.AreEqual(futureVersion, database.meta.schemaVersion);
        }

        // ══════════════════════════════════════════════
        // Internal Helpers
        // ══════════════════════════════════════════════

        private static SaveDatabase BuildDatabase(int schemaVersion)
        {
            SaveDatabase database = SaveDatabase.CreateNew();
            database.meta.schemaVersion = schemaVersion;
            return database;
        }
    }
}
