using System.Runtime.InteropServices;

namespace Game.Framework.SaveSystem
{
    /// <summary>
    /// Bridges to platform-specific file-system persistence. On WebGL, writes to
    /// persistentDataPath only reach the browser's IndexedDB after an explicit FS.syncfs, so the
    /// storage layer calls <see cref="PersistToDisk"/> after each write/delete. No-op elsewhere.
    /// </summary>
    internal static class SaveFileSystem
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void SaveFileSync_Flush();
#endif

        internal static bool IsWebGLRuntime =>
#if UNITY_WEBGL && !UNITY_EDITOR
            true;
#else
            false;
#endif

        /// <summary>Flushes pending file writes to durable storage. Only does work on WebGL.</summary>
        internal static void PersistToDisk()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SaveFileSync_Flush();
#endif
        }
    }
}
