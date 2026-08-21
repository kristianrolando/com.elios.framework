using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Framework.Ticking
{
    // Single update loop for the whole game. One hidden MonoBehaviour drives every registered
    // object instead of Unity calling a separate Update() per component, so execution order is
    // explicit and the per-component native call overhead disappears.
    //
    // Four independent channels: scaled, unscaled, fixed, late. An object registers only in the
    // channels it needs. Register/Unregister is safe to call from inside a tick; the change is
    // applied after the current channel finishes iterating.
    public static class TickManager
    {
        private const string DriverName = "__TickDriver__";

        // Interval below this is treated as "every frame". Keeps a mistyped tiny interval from
        // turning into a division-sized accumulation error.
        private const float MinInterval = 0.0001f;

        private static readonly TickChannel<ITickable> UpdateChannel =
            new TickChannel<ITickable>((target, deltaTime) => target.Tick(deltaTime));

        private static readonly TickChannel<IUnscaledTickable> UnscaledChannel =
            new TickChannel<IUnscaledTickable>((target, deltaTime) => target.UnscaledTick(deltaTime));

        private static readonly TickChannel<IFixedTickable> FixedChannel =
            new TickChannel<IFixedTickable>((target, deltaTime) => target.FixedTick(deltaTime));

        private static readonly TickChannel<ILateTickable> LateChannel =
            new TickChannel<ILateTickable>((target, deltaTime) => target.LateTick(deltaTime));

        private static TickDriver _driver;
        private static bool _isSubscribed;

        // ══════════════════════════════════════════════
        // Public API
        // ══════════════════════════════════════════════

        // Freezes the scaled, fixed and late channels; the unscaled channel keeps running.
        // Independent of Time.timeScale: this only stops our own loop, it does not stop Unity's
        // physics simulation, animators, or particles. Set timeScale as well for a full freeze.
        public static bool IsPaused { get; set; }

        // interval 0 means every frame. A positive interval throttles the target: it is ticked
        // only after that many seconds have passed, and receives the real elapsed time as its
        // deltaTime, so accumulating movement stays correct.
        public static void RegisterTick(ITickable target, float interval = 0f)
        {
            EnsureDriver();
            UpdateChannel.Register(target, interval);
        }

        public static void UnregisterTick(ITickable target)
        {
            UpdateChannel.Unregister(target);
        }

        public static void RegisterUnscaledTick(IUnscaledTickable target, float interval = 0f)
        {
            EnsureDriver();
            UnscaledChannel.Register(target, interval);
        }

        public static void UnregisterUnscaledTick(IUnscaledTickable target)
        {
            UnscaledChannel.Unregister(target);
        }

        public static void RegisterFixedTick(IFixedTickable target, float interval = 0f)
        {
            EnsureDriver();
            FixedChannel.Register(target, interval);
        }

        public static void UnregisterFixedTick(IFixedTickable target)
        {
            FixedChannel.Unregister(target);
        }

        public static void RegisterLateTick(ILateTickable target, float interval = 0f)
        {
            EnsureDriver();
            LateChannel.Register(target, interval);
        }

        public static void UnregisterLateTick(ILateTickable target)
        {
            LateChannel.Unregister(target);
        }

        public static bool IsRegistered(ITickable target) => UpdateChannel.Contains(target);

        public static bool IsRegistered(IUnscaledTickable target) => UnscaledChannel.Contains(target);

        public static bool IsRegistered(IFixedTickable target) => FixedChannel.Contains(target);

        public static bool IsRegistered(ILateTickable target) => LateChannel.Contains(target);

        // Drops every registration in every channel. Only for a hard reset (run ended,
        // scene about to be reloaded); normal objects unregister themselves.
        public static void Clear()
        {
            UpdateChannel.Clear();
            UnscaledChannel.Clear();
            FixedChannel.Clear();
            LateChannel.Clear();
        }

        // ══════════════════════════════════════════════
        // Lifecycle
        // ══════════════════════════════════════════════

        // Runs on every Play start, even with "Enter Play Mode without Domain Reload" on,
        // so stale static state from a previous session never leaks in.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Clear();

            // Without a domain reload the subscription from the previous session survives.
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            Application.quitting -= HandleQuitting;

            _driver = null;
            _isSubscribed = false;
            IsPaused = false;
        }

        // ══════════════════════════════════════════════
        // Main Logic
        // ══════════════════════════════════════════════

        internal static void RunUpdate()
        {
            if (!IsPaused)
            {
                UpdateChannel.Tick(Time.deltaTime);
            }

            UnscaledChannel.Tick(Time.unscaledDeltaTime);
        }

        internal static void RunFixedUpdate()
        {
            if (IsPaused)
            {
                return;
            }

            FixedChannel.Tick(Time.fixedDeltaTime);
        }

        internal static void RunLateUpdate()
        {
            if (IsPaused)
            {
                return;
            }

            LateChannel.Tick(Time.deltaTime);
        }

        // ══════════════════════════════════════════════
        // Internal Helpers
        // ══════════════════════════════════════════════

        private static void EnsureDriver()
        {
            EnsureSubscribed();

            if (_driver != null)
            {
                return;
            }

            GameObject driverObject = new GameObject(DriverName)
            {
                hideFlags = HideFlags.DontSave
            };

            _driver = driverObject.AddComponent<TickDriver>();
        }

        private static void EnsureSubscribed()
        {
            if (_isSubscribed)
            {
                return;
            }

            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            Application.quitting += HandleQuitting;
            _isSubscribed = true;
        }

        // The driver lives in the scene, so a scene load destroys it along with every registered
        // MonoBehaviour. Rebuild the driver and drop the entries whose target no longer exists;
        // an object that survived the load (DontDestroyOnLoad) keeps ticking untouched.
        private static void HandleActiveSceneChanged(Scene previous, Scene next)
        {
            UpdateChannel.PurgeDestroyed();
            UnscaledChannel.PurgeDestroyed();
            FixedChannel.PurgeDestroyed();
            LateChannel.PurgeDestroyed();

            if (UpdateChannel.Count + UnscaledChannel.Count + FixedChannel.Count + LateChannel.Count > 0)
            {
                EnsureDriver();
            }
        }

        private static void HandleQuitting()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            Application.quitting -= HandleQuitting;
            _isSubscribed = false;

            Clear();
        }

#if UNITY_EDITOR
        // Editor-only snapshot used by the driver's Inspector to spot leaks (a target that was
        // never unregistered) without opening the profiler.
        internal static void FillDebugSnapshot(List<string> buffer, out int updateCount,
            out int unscaledCount, out int fixedCount, out int lateCount)
        {
            buffer.Clear();

            updateCount = UpdateChannel.Count;
            unscaledCount = UnscaledChannel.Count;
            fixedCount = FixedChannel.Count;
            lateCount = LateChannel.Count;

            UpdateChannel.FillDebugNames("Tick", buffer);
            UnscaledChannel.FillDebugNames("Unscaled", buffer);
            FixedChannel.FillDebugNames("Fixed", buffer);
            LateChannel.FillDebugNames("Late", buffer);
        }
#endif

        // ══════════════════════════════════════════════
        // Tick Channel
        // ══════════════════════════════════════════════

        // One array of targets plus the bookkeeping needed to mutate it while it is being iterated.
        // A raw array rather than a List so the loop can take each entry by ref: the throttling
        // fields are written in place instead of copying the struct out and back every frame.
        private sealed class TickChannel<T> where T : class
        {
            private const int InitialCapacity = 16;

            private struct Entry
            {
                public T Target;
                public float Interval;

                // Drives the schedule. Keeps its remainder past the interval instead of resetting
                // to zero, otherwise every cycle throws away the overshoot of the frame that
                // crossed the threshold and the effective rate drifts slower than requested.
                public float Scheduled;

                // Real time since the last invoke. Reported as the target's deltaTime so
                // accumulation stays correct even when Scheduled carries a remainder forward.
                public float SinceLastTick;
            }

            private readonly List<Entry> _pendingAdds = new();
            private readonly HashSet<T> _registered = new();
            private readonly Action<T, float> _invoke;

            private Entry[] _entries = new Entry[InitialCapacity];
            private int _count;
            private bool _isTicking;
            private bool _hasDeadEntries;

            public TickChannel(Action<T, float> invoke)
            {
                _invoke = invoke;
            }

            public int Count => _registered.Count;

            public bool Contains(T target)
            {
                return target != null && _registered.Contains(target);
            }

            public void Register(T target, float interval)
            {
                if (target == null)
                {
                    Debug.LogError($"[{nameof(TickManager)}] Tried to register a null {typeof(T).Name}.");
                    return;
                }

                if (!_registered.Add(target))
                {
                    EditorDebug.Warning($"[{nameof(TickManager)}] '{DescribeTarget(target)}' is already registered for {typeof(T).Name}. Ignored.", target as UnityEngine.Object);
                    return;
                }

                Entry entry = new Entry
                {
                    Target = target,
                    Interval = interval < MinInterval ? 0f : interval,
                    Scheduled = 0f,
                    SinceLastTick = 0f
                };

                if (_isTicking)
                {
                    _pendingAdds.Add(entry);
                    return;
                }

                Append(entry);
            }

            public void Unregister(T target)
            {
                if (target == null || !_registered.Remove(target))
                {
                    return;
                }

                for (int i = 0; i < _pendingAdds.Count; i++)
                {
                    if (!ReferenceEquals(_pendingAdds[i].Target, target))
                    {
                        continue;
                    }

                    _pendingAdds.RemoveAt(i);
                    return;
                }

                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(_entries[i].Target, target))
                    {
                        continue;
                    }

                    MarkDead(i);
                    break;
                }

                if (!_isTicking)
                {
                    Compact();
                }
            }

            public void Tick(float deltaTime)
            {
                if (_count == 0)
                {
                    FlushPending();
                    return;
                }

                _isTicking = true;

                for (int i = 0; i < _count; i++)
                {
                    ref Entry entry = ref _entries[i];

                    if (IsDead(entry.Target))
                    {
                        DropDestroyed(i);
                        continue;
                    }

                    float step = deltaTime;

                    if (entry.Interval > 0f)
                    {
                        entry.Scheduled += deltaTime;
                        entry.SinceLastTick += deltaTime;

                        if (entry.Scheduled < entry.Interval)
                        {
                            continue;
                        }

                        // Modulus, not zero: keeps the schedule drift-free, and collapses the
                        // backlog after a long hitch into a single tick instead of a burst.
                        entry.Scheduled %= entry.Interval;
                        step = entry.SinceLastTick;
                        entry.SinceLastTick = 0f;
                    }

                    // Held separately because the entry can be cleared underneath us if the
                    // target unregisters itself, or the whole channel, from inside its tick.
                    T target = entry.Target;

                    // One broken target must not stop every other one for the rest of the run,
                    // which is what an uncaught exception would do in a shared loop. The offender
                    // is dropped so the error is reported once instead of every frame.
                    try
                    {
                        _invoke(target, step);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError($"[{nameof(TickManager)}] '{DescribeTarget(target)}' threw during {typeof(T).Name} and was unregistered.\n{exception}");

                        _registered.Remove(target);

                        if (i < _count)
                        {
                            MarkDead(i);
                        }
                    }
                }

                _isTicking = false;

                Compact();
                FlushPending();
            }

            public void PurgeDestroyed()
            {
                for (int i = 0; i < _count; i++)
                {
                    if (IsDead(_entries[i].Target))
                    {
                        DropDestroyed(i);
                    }
                }

                Compact();
            }

            public void Clear()
            {
                Array.Clear(_entries, 0, _count);
                _count = 0;
                _pendingAdds.Clear();
                _registered.Clear();
                _isTicking = false;
                _hasDeadEntries = false;
            }

#if UNITY_EDITOR
            internal void FillDebugNames(string channelLabel, List<string> buffer)
            {
                for (int i = 0; i < _count; i++)
                {
                    T target = _entries[i].Target;

                    if (ReferenceEquals(target, null))
                    {
                        continue;
                    }

                    float interval = _entries[i].Interval;
                    string suffix = interval > 0f ? $" @{interval:0.###}s" : string.Empty;

                    buffer.Add($"[{channelLabel}] {target.GetType().Name}{suffix}");
                }
            }
#endif

            // A destroyed MonoBehaviour is only "fake null": the generic T comparison uses plain
            // reference equality and would happily tick a dead object, so the Unity check is
            // explicit here.
            private static bool IsDead(T target)
            {
                if (ReferenceEquals(target, null))
                {
                    return true;
                }

                return target is UnityEngine.Object unityTarget && unityTarget == null;
            }

            private void Append(in Entry entry)
            {
                if (_count == _entries.Length)
                {
                    Array.Resize(ref _entries, _entries.Length * 2);
                }

                _entries[_count] = entry;
                _count++;
            }

            private void DropDestroyed(int index)
            {
                T target = _entries[index].Target;

                if (!ReferenceEquals(target, null))
                {
                    _registered.Remove(target);
                    EditorDebug.Warning($"[{nameof(TickManager)}] A destroyed {target.GetType().Name} was still registered for {typeof(T).Name}. Unregister in OnDestroy.");
                }

                MarkDead(index);
            }

            private void MarkDead(int index)
            {
                _entries[index].Target = null;
                _hasDeadEntries = true;
            }

            private void Compact()
            {
                if (!_hasDeadEntries)
                {
                    return;
                }

                int write = 0;

                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_entries[i].Target, null))
                    {
                        continue;
                    }

                    _entries[write] = _entries[i];
                    write++;
                }

                // Clear the tail: a stale reference left in the array would keep the object alive.
                Array.Clear(_entries, write, _count - write);

                _count = write;
                _hasDeadEntries = false;
            }

            private void FlushPending()
            {
                if (_pendingAdds.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < _pendingAdds.Count; i++)
                {
                    Append(_pendingAdds[i]);
                }

                _pendingAdds.Clear();
            }

            private static string DescribeTarget(T target)
            {
                return target is UnityEngine.Object unityTarget && unityTarget != null
                    ? $"{unityTarget.name} ({target.GetType().Name})"
                    : target.GetType().Name;
            }
        }
    }
}
