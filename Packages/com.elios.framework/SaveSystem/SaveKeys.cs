namespace Game.Framework.SaveSystem
{
    /// <summary>
    /// Strongly recommended fixed keys to avoid typo/human error.
    /// Add your own keys here over time.
    /// </summary>
    public static class SaveKeys
    {

        // Arena
        public const string Arena = "arena";

        // Player
        public const string PlayerFishInventory = "player/fish/inventory";

        // Checkpoint: room terakhir yang sudah dicapai pemain. Bertahan di disk, tidak seperti
        // snapshot HP yang sengaja hanya hidup selama aplikasi terbuka.
        public const string CheckpointLevel = "checkpoint/level";
        public const string CheckpointRoom = "checkpoint/room";
        public const string CheckpointAttempt = "checkpoint/attempt";

        // Balancing: ring buffer percobaan room terakhir. Data developer, bukan progress pemain.
        public const string RoomStatsRecent = "stats/room/recent";

    }
}