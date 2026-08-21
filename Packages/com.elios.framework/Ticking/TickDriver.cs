using System.Collections.Generic;
using UnityEngine;

namespace Game.Framework.Ticking
{
    // The only Update/FixedUpdate/LateUpdate left in the project. Created automatically by
    // TickManager on the first registration; never add it to a scene by hand.
    // The negative execution order keeps every tick ahead of the remaining plain MonoBehaviours.
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    internal class TickDriver : MonoBehaviour
    {
#if UNITY_EDITOR
        // Seconds between Inspector stat refreshes. Rebuilding the list every frame would cost
        // more than the loop it is meant to watch.
        private const float DebugRefreshInterval = 0.5f;

        [SerializeField, Tooltip("Registered targets per channel. Editor only, read only.")]
        private int _tickCount;

        [SerializeField, Tooltip("Registered targets in the unscaled channel.")]
        private int _unscaledTickCount;

        [SerializeField, Tooltip("Registered targets in the fixed (physics) channel.")]
        private int _fixedTickCount;

        [SerializeField, Tooltip("Registered targets in the late channel.")]
        private int _lateTickCount;

        [SerializeField, Tooltip("Every registered target by type. A name that lingers after the object is gone means a missing Unregister.")]
        private List<string> _registeredTargets = new();

        private float _debugRefreshTimer;
#endif

        // ══════════════════════════════════════════════
        // Unity Lifecycle
        // ══════════════════════════════════════════════

        private void Update()
        {
            TickManager.RunUpdate();
            RefreshDebugStats();
        }

        private void FixedUpdate()
        {
            TickManager.RunFixedUpdate();
        }

        private void LateUpdate()
        {
            TickManager.RunLateUpdate();
        }

        // ══════════════════════════════════════════════
        // Editor Debug
        // ══════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void RefreshDebugStats()
        {
#if UNITY_EDITOR
            _debugRefreshTimer += Time.unscaledDeltaTime;

            if (_debugRefreshTimer < DebugRefreshInterval)
            {
                return;
            }

            _debugRefreshTimer = 0f;

            TickManager.FillDebugSnapshot(_registeredTargets, out _tickCount, out _unscaledTickCount,
                out _fixedTickCount, out _lateTickCount);
#endif
        }
    }
}
