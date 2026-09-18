using System;
using System.Collections.Generic;
using UnityEngine;

namespace Elios.Framework.EventBus
{
    // Enum-keyed pub/sub. See README.md in this folder for usage and full API reference.
    public static class Bus<TEnum> where TEnum : struct, Enum
    {
        // ══════════════════════════════════════════════
        // 0 Parameters
        // ══════════════════════════════════════════════

        private static readonly Dictionary<TEnum, Action> _signals = new Dictionary<TEnum, Action>();

        public static void Subscribe(TEnum eventId, Action callback)
        {
            if (_signals.TryGetValue(eventId, out Action existing))
            {
                _signals[eventId] = (Action)Delegate.Combine(existing, callback);
            }
            else
            {
                _signals[eventId] = callback;
            }
        }

        public static void Unsubscribe(TEnum eventId, Action callback)
        {
            if (!_signals.TryGetValue(eventId, out Action existing))
            {
                return;
            }

            Action current = (Action)Delegate.Remove(existing, callback);
            if (current == null)
            {
                _signals.Remove(eventId);
            }
            else
            {
                _signals[eventId] = current;
            }
        }

        public static void Trigger(TEnum eventId)
        {
            if (_signals.TryGetValue(eventId, out Action action))
            {
                action?.Invoke();
            }
        }

        // ══════════════════════════════════════════════
        // 1 Parameter
        // ══════════════════════════════════════════════

        // A closed Cache<T> gets its own static Dictionary per T from the CLR, so lookups
        // never need a runtime Type key.
        private static class Cache<T>
        {
            public static readonly Dictionary<TEnum, Action<T>> Handlers = new Dictionary<TEnum, Action<T>>();

            static Cache()
            {
                _resetActions.Add(() => Handlers.Clear());
            }
        }

        public static void Subscribe<T>(TEnum eventId, Action<T> callback)
        {
            Dictionary<TEnum, Action<T>> dict = Cache<T>.Handlers;
            if (dict.TryGetValue(eventId, out Action<T> existing))
            {
                dict[eventId] = (Action<T>)Delegate.Combine(existing, callback);
            }
            else
            {
                dict[eventId] = callback;
            }
        }

        public static void Unsubscribe<T>(TEnum eventId, Action<T> callback)
        {
            Dictionary<TEnum, Action<T>> dict = Cache<T>.Handlers;
            if (!dict.TryGetValue(eventId, out Action<T> existing))
            {
                return;
            }

            Action<T> current = (Action<T>)Delegate.Remove(existing, callback);
            if (current == null)
            {
                dict.Remove(eventId);
            }
            else
            {
                dict[eventId] = current;
            }
        }

        public static void Trigger<T>(TEnum eventId, T arg)
        {
            if (Cache<T>.Handlers.TryGetValue(eventId, out Action<T> action))
            {
                action?.Invoke(arg);
            }
        }

        // ══════════════════════════════════════════════
        // 2 Parameters
        // ══════════════════════════════════════════════

        private static class Cache<T1, T2>
        {
            public static readonly Dictionary<TEnum, Action<T1, T2>> Handlers = new Dictionary<TEnum, Action<T1, T2>>();

            static Cache()
            {
                _resetActions.Add(() => Handlers.Clear());
            }
        }

        public static void Subscribe<T1, T2>(TEnum eventId, Action<T1, T2> callback)
        {
            Dictionary<TEnum, Action<T1, T2>> dict = Cache<T1, T2>.Handlers;
            if (dict.TryGetValue(eventId, out Action<T1, T2> existing))
            {
                dict[eventId] = (Action<T1, T2>)Delegate.Combine(existing, callback);
            }
            else
            {
                dict[eventId] = callback;
            }
        }

        public static void Unsubscribe<T1, T2>(TEnum eventId, Action<T1, T2> callback)
        {
            Dictionary<TEnum, Action<T1, T2>> dict = Cache<T1, T2>.Handlers;
            if (!dict.TryGetValue(eventId, out Action<T1, T2> existing))
            {
                return;
            }

            Action<T1, T2> current = (Action<T1, T2>)Delegate.Remove(existing, callback);
            if (current == null)
            {
                dict.Remove(eventId);
            }
            else
            {
                dict[eventId] = current;
            }
        }

        public static void Trigger<T1, T2>(TEnum eventId, T1 arg1, T2 arg2)
        {
            if (Cache<T1, T2>.Handlers.TryGetValue(eventId, out Action<T1, T2> action))
            {
                action?.Invoke(arg1, arg2);
            }
        }

        // ══════════════════════════════════════════════
        // 3 Parameters
        // ══════════════════════════════════════════════

        private static class Cache<T1, T2, T3>
        {
            public static readonly Dictionary<TEnum, Action<T1, T2, T3>> Handlers = new Dictionary<TEnum, Action<T1, T2, T3>>();

            static Cache()
            {
                _resetActions.Add(() => Handlers.Clear());
            }
        }

        public static void Subscribe<T1, T2, T3>(TEnum eventId, Action<T1, T2, T3> callback)
        {
            Dictionary<TEnum, Action<T1, T2, T3>> dict = Cache<T1, T2, T3>.Handlers;
            if (dict.TryGetValue(eventId, out Action<T1, T2, T3> existing))
            {
                dict[eventId] = (Action<T1, T2, T3>)Delegate.Combine(existing, callback);
            }
            else
            {
                dict[eventId] = callback;
            }
        }

        public static void Unsubscribe<T1, T2, T3>(TEnum eventId, Action<T1, T2, T3> callback)
        {
            Dictionary<TEnum, Action<T1, T2, T3>> dict = Cache<T1, T2, T3>.Handlers;
            if (!dict.TryGetValue(eventId, out Action<T1, T2, T3> existing))
            {
                return;
            }

            Action<T1, T2, T3> current = (Action<T1, T2, T3>)Delegate.Remove(existing, callback);
            if (current == null)
            {
                dict.Remove(eventId);
            }
            else
            {
                dict[eventId] = current;
            }
        }

        public static void Trigger<T1, T2, T3>(TEnum eventId, T1 arg1, T2 arg2, T3 arg3)
        {
            if (Cache<T1, T2, T3>.Handlers.TryGetValue(eventId, out Action<T1, T2, T3> action))
            {
                action?.Invoke(arg1, arg2, arg3);
            }
        }

        // ══════════════════════════════════════════════
        // 4 Parameters
        // ══════════════════════════════════════════════

        private static class Cache<T1, T2, T3, T4>
        {
            public static readonly Dictionary<TEnum, Action<T1, T2, T3, T4>> Handlers = new Dictionary<TEnum, Action<T1, T2, T3, T4>>();

            static Cache()
            {
                _resetActions.Add(() => Handlers.Clear());
            }
        }

        public static void Subscribe<T1, T2, T3, T4>(TEnum eventId, Action<T1, T2, T3, T4> callback)
        {
            Dictionary<TEnum, Action<T1, T2, T3, T4>> dict = Cache<T1, T2, T3, T4>.Handlers;
            if (dict.TryGetValue(eventId, out Action<T1, T2, T3, T4> existing))
            {
                dict[eventId] = (Action<T1, T2, T3, T4>)Delegate.Combine(existing, callback);
            }
            else
            {
                dict[eventId] = callback;
            }
        }

        public static void Unsubscribe<T1, T2, T3, T4>(TEnum eventId, Action<T1, T2, T3, T4> callback)
        {
            Dictionary<TEnum, Action<T1, T2, T3, T4>> dict = Cache<T1, T2, T3, T4>.Handlers;
            if (!dict.TryGetValue(eventId, out Action<T1, T2, T3, T4> existing))
            {
                return;
            }

            Action<T1, T2, T3, T4> current = (Action<T1, T2, T3, T4>)Delegate.Remove(existing, callback);
            if (current == null)
            {
                dict.Remove(eventId);
            }
            else
            {
                dict[eventId] = current;
            }
        }

        public static void Trigger<T1, T2, T3, T4>(TEnum eventId, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            if (Cache<T1, T2, T3, T4>.Handlers.TryGetValue(eventId, out Action<T1, T2, T3, T4> action))
            {
                action?.Invoke(arg1, arg2, arg3, arg4);
            }
        }

        // ══════════════════════════════════════════════
        // Reset
        // ══════════════════════════════════════════════

        // Populated lazily: every Cache<...> variant registers its own clear the first time
        // it's touched, so ClearAll works without knowing every T combination in use.
        private static readonly List<Action> _resetActions = new List<Action>();

        // Drops every subscriber across every arity for this TEnum. Only for a hard reset
        // (run ended, scene about to reload); normal listeners unsubscribe themselves.
        public static void ClearAll()
        {
            _signals.Clear();

            for (int i = 0; i < _resetActions.Count; i++)
            {
                _resetActions[i]();
            }
        }

        static Bus()
        {
            BusResetRegistry.Register(ClearAll);
        }
    }

    // Non-generic so a single RuntimeInitializeOnLoadMethod can reset every closed Bus<TEnum>,
    // matching how TickManager resets itself when "Enter Play Mode without Domain Reload" is on.
    internal static class BusResetRegistry
    {
        private static readonly List<Action> _resetActions = new List<Action>();

        internal static void Register(Action resetAction)
        {
            _resetActions.Add(resetAction);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll()
        {
            for (int i = 0; i < _resetActions.Count; i++)
            {
                _resetActions[i]();
            }
        }
    }
}
