using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Framework.SaveSystem
{
    // Auto-initialize save system before first scene loads.
    // Also flushes on application quit.
    public static class SaveBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            Save.InitializeIfNeeded();

            // Handles pause/focus/quit flushing (primary safety net on mobile).
            SaveLifecycleHandler.EnsureExists();

            // Avoid duplicate subscription during domain reloads in editor
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Save.IsInitialized && Save.IsDirty)
                Save.Flush();
        }
    }
}