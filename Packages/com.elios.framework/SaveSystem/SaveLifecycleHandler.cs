using UnityEngine;

namespace Game.Framework.SaveSystem
{
    // Persists unsaved changes when the app is backgrounded, loses focus, or quits.
    // On mobile, Application.quitting is unreliable (the OS can kill a suspended app),
    // so OnApplicationPause/OnApplicationFocus are the primary flush signals.
    // On WebGL these focus/pause events are likewise the primary signals (OnApplicationQuit does
    // not fire on tab close); each flush is persisted to IndexedDB by the storage layer.
    [DisallowMultipleComponent]
    public sealed class SaveLifecycleHandler : MonoBehaviour
    {
        private static SaveLifecycleHandler _instance;

        public static void EnsureExists()
        {
            if (_instance != null)
                return;

            var go = new GameObject("[SaveLifecycleHandler]")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            _instance = go.AddComponent<SaveLifecycleHandler>();
            DontDestroyOnLoad(go);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
                FlushIfNeeded();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                FlushIfNeeded();
        }

        private void OnApplicationQuit()
        {
            FlushIfNeeded();
        }

        private static void FlushIfNeeded()
        {
            if (Save.IsInitialized && Save.IsDirty)
                Save.Flush();
        }
    }
}
