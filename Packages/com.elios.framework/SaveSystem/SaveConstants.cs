
namespace Game.Framework.SaveSystem
{
    public static class SaveConstants
    {
        public const string RootFolderName = "save_v1";
        public const string SlotFileExtension = ".json";

        // Backup kept next to each slot file so a failed write can be recovered.
        public const string BackupFileExtension = ".json.bak";

        public const string DefaultSlotId = "slot_1";
        public const string PlayerPrefsLastSlotKey = "save.last_slot";

        public const int CurrentSchemaVersion = 1;

        // Reserved namespace for framework/internal usage. Values are the normalized
        // form (leading underscores are stripped by key normalization), so this must
        // stay reachable through the key pipeline to be enforceable.
        public const string ReservedSystemNamespace = "sys";
        public const string ReservedSystemPrefix = "sys/";
    }
}