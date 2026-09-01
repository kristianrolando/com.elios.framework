# Ticking

A single update loop for the entire game. One hidden MonoBehaviour (`__TickDriver__`) ticks every registered object, replacing thousands of separate `Update()` calls.

> TL;DR: implement `ITickable`, call `TickManager.RegisterTick(this)` in `OnEnable`, `TickManager.UnregisterTick(this)` in `OnDisable`/`OnDestroy`. No scene setup required.

---

## Table of Contents
- [Quick Start](#quick-start)
- [Four Channels](#four-channels)
- [Interval Throttling](#interval-throttling)
- [Pause](#pause)
- [Registration Lifecycle](#registration-lifecycle)
- [Debug Stats](#debug-stats)
- [⚠️ Limitations & Important Notes](#-limitations--important-notes)
- [API Reference](#api-reference)

---

## Quick Start

```csharp
using Game.Framework.Ticking;
using UnityEngine;

public class ExampleController : MonoBehaviour, ITickable
{
    private void OnEnable()  => TickManager.RegisterTick(this);
    private void OnDisable() => TickManager.UnregisterTick(this);

    public void Tick(float deltaTime)
    {
        // your old Update() body goes here
    }
}
```

The driver is created automatically on first registration. No prefab needed, no GameObject required in the scene.

---

## Four Channels

| Interface | Method | deltaTime | Replaces |
|---|---|---|---|
| `ITickable` | `Tick(float)` | `Time.deltaTime` | `Update()` |
| `IUnscaledTickable` | `UnscaledTick(float)` | `Time.unscaledDeltaTime` | `Update()` that must keep running while paused |
| `IFixedTickable` | `FixedTick(float)` | `Time.fixedDeltaTime` | `FixedUpdate()` |
| `ILateTickable` | `LateTick(float)` | `Time.deltaTime` | `LateUpdate()` |

The channels are independent. A class may implement several interfaces at once, but each still needs its own registration:

```csharp
public class PlayerController : MonoBehaviour, ITickable, IFixedTickable
{
    private void OnEnable()
    {
        TickManager.RegisterTick(this);
        TickManager.RegisterFixedTick(this);
    }

    private void OnDisable()
    {
        TickManager.UnregisterTick(this);
        TickManager.UnregisterFixedTick(this);
    }

    public void Tick(float deltaTime) { /* read input, timers */ }

    public void FixedTick(float fixedDeltaTime) { /* physics */ }
}
```

The `Update` vs. `FixedUpdate` rule from CLAUDE.md still applies: input & timers in `Tick`, anything touching `Rigidbody2D` in `FixedTick`.

For pausing, see [Pause](#pause).

---

## Interval Throttling

Logic that doesn't need to run every frame (AI vision, target scanning, distance checks) can be throttled:

```csharp
private const float VisionInterval = 0.2f;

private void OnEnable() => TickManager.RegisterTick(this, VisionInterval);

public void Tick(float deltaTime)
{
    // called 5x per second.
    // deltaTime = real elapsed time since the last tick (~0.2), not a single frame's time,
    // so deltaTime-based accumulation stays correct.
}
```

An interval of `0` (default) means every frame.

Scheduling is drift-free: leftover time beyond the interval carries into the next cycle (modulus) instead of being discarded. So a 0.2s interval still fires 5x per second even at a 60ms frame time — it doesn't drift to 0.24s the way it would if the accumulator were reset to zero. After a long hitch (e.g. a 2-second freeze), the backlog collapses into a single tick instead of bursting.

---

## Pause

```csharp
TickManager.IsPaused = true;   // open the pause menu
TickManager.IsPaused = false;  // resume
```

| Channel | While `IsPaused` |
|---|---|
| `Tick` | fully stopped (not called at all) |
| `FixedTick` | fully stopped |
| `LateTick` | fully stopped |
| `UnscaledTick` | keeps running |

This stops iteration of the channel entirely rather than sending `deltaTime` 0 — so non-delta logic inside `Tick` (reading input, distance checks) also stops, and there's no iteration cost while paused.

**`IsPaused` is separate from `Time.timeScale`.** Only our own loop is stopped; Unity's physics simulation, Animator, and ParticleSystem keep running. For a full freeze, set both:

```csharp
TickManager.IsPaused = true;
Time.timeScale = 0f;
```

If you only use `Time.timeScale = 0` without `IsPaused`, `Tick()` is still called every frame with `deltaTime` 0 (standard `Update()` behavior), while `FixedTick` stops on its own.

---

## Registration Lifecycle

- **`OnEnable` / `OnDisable`** — the default choice. A disabled object automatically stops ticking, just like Unity's built-in `Update()`. Required for pooled objects.
- **`Start` / `OnDestroy`** — when the object must keep ticking even while its component is disabled.

`Register` and `Unregister` are safe to call from inside `Tick()` — the change is applied after the channel finishes iterating. Duplicate registration is ignored (and logs a warning in the editor).

---

## Debug Stats

During Play Mode, select the `__TickDriver__` GameObject in the Hierarchy. Its Inspector shows the target count per channel plus a list of registered type names, refreshed every 0.5 seconds. Editor-only, fully stripped from builds.

A type name that's still listed even though its object is gone means an `Unregister` call was missed somewhere.

---

## ⚠️ Limitations & Important Notes

- **`Unregister` is still mandatory.** A destroyed object without an unregister call is detected automatically and dropped (with a warning in the editor), but only on the next tick — don't rely on it.
- **A target that throws an exception is unregistered immediately** and the error is logged once. This is intentional: in a shared loop, one uncaught exception would kill every other object on that channel.
- **Execution order = registration order.** There is no priority system yet. If A must tick before B, don't rely on ordering — call B from A instead.
- **The driver lives in the active scene.** On a scene change, entries whose targets have been destroyed are cleaned up and the driver is recreated. `DontDestroyOnLoad` objects stay registered and keep ticking.
- **`Time.timeScale` 0 does not block `Tick()`.** The method is still called with `deltaTime` 0. Use `TickManager.IsPaused` if you want it to actually stop — see [Pause](#pause).
- **`IsPaused` is global static state.** If it's forgotten and left `true`, the entire game stops ticking. It resets automatically on every entry to Play Mode.
- **`try-catch` stays active in release builds**, not editor-only. Editor and build behavior must match; if the exception handler were stripped, a single exception in a build would kill the whole channel — the worst failure mode, and on the path least likely to be tested.
- **Not a coroutine replacement.** For time-based sequences, a coroutine is still the right tool.

---

## API Reference

```csharp
// Scaled
void RegisterTick(ITickable target, float interval = 0f);
void UnregisterTick(ITickable target);

// Unscaled
void RegisterUnscaledTick(IUnscaledTickable target, float interval = 0f);
void UnregisterUnscaledTick(IUnscaledTickable target);

// Physics
void RegisterFixedTick(IFixedTickable target, float interval = 0f);
void UnregisterFixedTick(IFixedTickable target);

// Late
void RegisterLateTick(ILateTickable target, float interval = 0f);
void UnregisterLateTick(ILateTickable target);

// Pause
bool IsPaused { get; set; }                   // stops scaled + fixed + late; unscaled keeps running

// Query & reset
bool IsRegistered(ITickable target);          // an overload exists for each interface
void Clear();                                 // remove all registrations on all channels
```
