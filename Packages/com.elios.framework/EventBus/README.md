# EventBus

Generic enum-based event bus for decoupled communication between systems (e.g. Combat, Audio, UI, Save System) without holding direct object references to each other.

> TL;DR: define an enum for the event category, `Bus<TEnum>.Subscribe(id, callback)` in `Start` (external publisher) or `OnEnable` (internal publisher), `Bus<TEnum>.Trigger(id, ...)` from anywhere, `Bus<TEnum>.Unsubscribe(id, callback)` in `OnDestroy`.

---

## Table of Contents
- [Quick Start](#quick-start)
- [Parameter Categories](#parameter-categories)
- [Automatic Reset](#automatic-reset)
- [⚠️ Limitations & Important Notes](#-limitations--important-notes)
- [API Reference](#api-reference)

---

## Quick Start

```csharp
using Elios.Framework.EventBus;

public enum CombatEvent { OnHit, OnEnemyDied }

private void Start() => Bus<CombatEvent>.Subscribe<int>(CombatEvent.OnHit, HandleHit);
private void OnDestroy() => Bus<CombatEvent>.Unsubscribe<int>(CombatEvent.OnHit, HandleHit);

private void HandleHit(int damage) { /* ... */ }

// Trigger from anywhere, the publisher doesn't need to know who the listeners are
Bus<CombatEvent>.Trigger(CombatEvent.OnHit, 50);
```

One enum = one category. `TEnum` determines which dictionary is used, so `Bus<CombatEvent>` and `Bus<GameState>` are completely independent of each other.

Follow the subscription lifecycle rules in CLAUDE.md: external publishers (another Manager/singleton) are subscribed in `Start`, internal publishers (a component on the same GameObject) in `OnEnable` with `-=` before `+=`. Always unsubscribe in `OnDestroy`.

---

## Parameter Categories

| Arity | Subscribe | Trigger |
|---|---|---|
| 0 | `Subscribe(id, Action callback)` | `Trigger(id)` |
| 1 | `Subscribe<T>(id, Action<T> callback)` | `Trigger(id, arg)` |
| 2 | `Subscribe<T1,T2>(id, Action<T1,T2> callback)` | `Trigger(id, arg1, arg2)` |
| 3 | `Subscribe<T1,T2,T3>(id, Action<T1,T2,T3> callback)` | `Trigger(id, arg1, arg2, arg3)` |
| 4 | `Subscribe<T1,T2,T3,T4>(id, Action<T1,T2,T3,T4> callback)` | `Trigger(id, arg1, arg2, arg3, arg4)` |

The parameter types on `Subscribe` and `Trigger` must be identical, including their order. If they differ (e.g. subscribing with `<int>` but triggering with `<float>`), they are treated as entirely different events and will never fire each other — with no compile-time or runtime error, so a type typo fails silently.

---

## Automatic Reset

All dictionaries here are `static`. With the "Enter Play Mode without Domain Reload" Editor option enabled, static fields are not reset between Play sessions — subscribers from a previous session can linger and reference objects that have already been destroyed.

Just like `TickManager`, `Bus<TEnum>` resets automatically on every entry to Play Mode via `[RuntimeInitializeOnLoadMethod]` — no manual call needed, and this applies to every `TEnum` and every arity that has ever been used.

`ClearAll()` is still available for a manual hard reset (e.g. a major scene transition mid-session), but for normal cases it's enough for each listener to `Unsubscribe` itself in `OnDestroy`.

---

## ⚠️ Limitations & Important Notes

- **`Unsubscribe` is mandatory.** There is no automatic detection like `TickManager` — a listener that forgets to unsubscribe is still invoked even after its object has been destroyed, and that will access a fake-null object.
- **One throwing subscriber stops the other subscribers on the same event.** `Trigger` calls the multicast delegate sequentially; an exception in one handler stops the rest of the chain. This is standard C# event behavior, not specific to this bus — don't put logic that can fail into a handler without a try-catch if that event is also listened to by other critical systems.
- **No guaranteed call order** other than `Subscribe` order. Don't rely on the order between subscribers for logic that matters.
- **Different parameter type combinations = different events**, even with the same `eventId`. See [Parameter Categories](#parameter-categories).

---

## API Reference

```csharp
// Per arity (0-4 parameters), same pattern for all:
void Subscribe(TEnum id, Action callback);
void Unsubscribe(TEnum id, Action callback);
void Trigger(TEnum id);
// ...similarly for Subscribe<T>, Subscribe<T1,T2>, Subscribe<T1,T2,T3>, Subscribe<T1,T2,T3,T4>

// Reset
void ClearAll(); // remove all subscribers for this TEnum, all arities
```
