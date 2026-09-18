using UnityEngine;
using Object = UnityEngine.Object;

namespace Elios.Framework.SaveSystem
{
    // Centralised logging for the save system.
    // Verbose logs are opt-in (enableLogging) and editor-only via EditorDebug,
    // so they are stripped from builds. Critical persistence failures use runtime logging so
    // they still surface in device/build logs.
    internal static class SaveLog
    {
        // Toggle from gameplay via Save.EnableLogging. Off by default to keep the console quiet.
        internal static bool enableLogging = false;

        // ══════════════════════════════════════════════
        // Verbose (opt-in, editor-only)
        // ══════════════════════════════════════════════

        internal static void Info(object message, Object context = null)
        {
            if (!enableLogging) return;
            EditorDebug.Info(message, context);
        }

        internal static void Success(object message, Object context = null)
        {
            if (!enableLogging) return;
            EditorDebug.Success(message, context);
        }

        // ══════════════════════════════════════════════
        // Developer guidance (editor-only, not gated)
        // ══════════════════════════════════════════════

        internal static void Warning(object message, Object context = null)
        {
            EditorDebug.Warning(message, context);
        }

        // ══════════════════════════════════════════════
        // Critical failures (runtime, never stripped or hidden)
        // ══════════════════════════════════════════════

        internal static void Error(object message, Object context = null)
        {
            Debug.LogError(message, context);
        }
    }
}
