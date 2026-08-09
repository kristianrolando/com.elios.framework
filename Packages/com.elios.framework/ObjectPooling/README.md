# Object Pooling

A lightweight, **zero-setup** object pool. Reuse GameObjects instead of `Instantiate`/`Destroy` to avoid GC spikes and hitches — one pool is created automatically per prefab on first use.

> TL;DR: `ObjectPoolManager.Get(prefab, position)` to spawn, `ObjectPoolManager.Return(instance)` to recycle. That's it.

---

## Table of Contents
- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
- [Usage by Scenario](#usage-by-scenario)
- [Resetting State (IPoolable)](#resetting-state-ipoolable)
- [Prewarming](#prewarming)
- [Managing & Clearing Pools](#managing--clearing-pools)
- [Architecture & Flow](#architecture--flow)
- [⚠️ Limitations & Important Notes](#-limitations--important-notes)
- [API Reference](#api-reference)

---

## Quick Start

```csharp
using Game.Framework.ObjectPooling;

// Spawn (a pool for this prefab is created automatically the first time)
GameObject bullet = ObjectPoolManager.Get(bulletPrefab, spawnPoint.position);

// Spawn with rotation and/or parent
GameObject fx = ObjectPoolManager.Get(hitFxPrefab, pos, Quaternion.identity, parentTransform);

// Recycle back into the pool
ObjectPoolManager.Return(bullet);
```

No manager to place in the scene, no registration required. Just call `Get` and `Return`.

---

## Core Concepts

| Term | Meaning |
|---|---|
| **Pool** | A reserve of inactive clones of one prefab. Key = `prefab.GetInstanceID()`. |
| **Get / Rent** | Take an instance from the pool (or instantiate a new one if empty) and activate it. |
| **Return** | Deactivate an instance and put it back into the pool for reuse. |
| **Reserve** | The pooled (inactive) instances waiting to be reused. Capped by `maxSize`. |
| **PoolableKey** | An auto-added marker component linking an instance to its pool. You don't touch it. |

Pooled instances live under a hidden root `__ObjectPoolRoot__`. All pools are **cleared automatically when the active scene changes**.

---

## Usage by Scenario

### Spawn & recycle (most common)
```csharp
GameObject e = ObjectPoolManager.Get(enemyPrefab, spawn.position);
// ... later ...
ObjectPoolManager.Return(e);
```

### Spawn under a parent (e.g. UI, world container)
```csharp
GameObject coin = ObjectPoolManager.Get(coinPrefab, pos, Quaternion.identity, coinContainer);
```
> On `Return`, the instance is re-parented back to the pool root (it detaches from `coinContainer`).

### Auto-return after a delay
```csharp
GameObject fx = ObjectPoolManager.Get(explosionPrefab, pos);
StartCoroutine(ReturnAfter(fx, 2f));

IEnumerator ReturnAfter(GameObject go, float seconds)
{
    yield return new WaitForSeconds(seconds);
    ObjectPoolManager.Return(go);
}
```

### Check whether a pool exists
```csharp
if (ObjectPoolManager.HasPool(bulletPrefab)) { /* ... */ }
```

### Diagnostics (editor-only)
```csharp
ObjectPoolManager.LogStats(); // prints active/reserve/rented/returned per pool
```

---

## Resetting State (IPoolable)

Pooled objects **keep their previous state** (velocity, running coroutines, particles, timers). Implement `IPoolable` on the prefab (root or any child) to reset/initialize cleanly:

```csharp
using Game.Framework.ObjectPooling;
using UnityEngine;

public sealed class Bullet : MonoBehaviour, IPoolable
{
    private Rigidbody _rb;
    private void Awake() => _rb = GetComponent<Rigidbody>();

    public void OnSpawn()
    {
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    public void OnReturnToPool()
    {
        // Stop trails/particles, cancel timers, etc.
    }
}
```

- `OnSpawn()` runs right after the instance is activated on `Get`.
- `OnReturnToPool()` runs right before it is deactivated on `Return`.
- Multiple `IPoolable` components (including on children) are all notified. They are cached on first spawn, so it stays cheap.

---

## Prewarming

Avoid first-use instantiation spikes by creating instances ahead of time:

```csharp
// Create the pool with 20 ready instances; reserve capped at 50.
ObjectPoolManager.RegisterPrefab(bulletPrefab, initialSize: 20, maxSize: 50);
```

- `initialSize` is clamped to `maxSize` (extras would only be created then destroyed).
- Prewarming does **not** fire `IPoolable` callbacks — instances are simply created inactive.
- `RegisterPrefab` is optional; `Get` registers with defaults (`initialSize: 0, maxSize: 200`) if you skip it.

---

## Managing & Clearing Pools

```csharp
ObjectPoolManager.ClearPoolForPrefab(bulletPrefab); // destroy reserve + drop this pool
ObjectPoolManager.ClearAll();                        // destroy all reserves + drop all pools
```

Pools are also cleared automatically on **active scene change**, so you usually don't need to clear manually.

---

## Architecture & Flow

```
Gameplay code
     │  ObjectPoolManager.Get / Return
     ▼
ObjectPoolManager (static facade)          ObjectPoolManager.cs
     │  Dictionary<InstanceID, ObjectPool>, auto-register, clear on scene change
     ▼
ObjectPool (one per prefab)                ObjectPool.cs
     │  Queue<GameObject> reserve, maxSize cap, stats, IPoolable notifications
     ▼
PoolableKey (auto marker) + IPoolable      PoolableKey.cs / IPoolable.cs
```

**Get flow:** dequeue a valid instance (skip destroyed ones) or instantiate → ensure marker → set parent/position → activate → `OnSpawn()`.

**Return flow:** validate marker & pool → `OnReturnToPool()` → re-parent to root → deactivate → enqueue (or destroy if reserve is full).

---

## ⚠️ Limitations & Important Notes

| # | Note | Impact / How to handle |
|---|---|---|
| 1 | **Main-thread only** | Not thread-safe. Call only from Unity's main thread. |
| 2 | **No automatic state reset** | Reused objects keep old state (velocity, coroutines, particles). Implement `IPoolable` to reset. |
| 3 | **Pools don't persist across scenes** | All pools are cleared on **active scene change**; the pool root is not `DontDestroyOnLoad`. Additive scene loads do **not** trigger a clear. |
| 4 | **`maxSize` caps the reserve, not active instances** | When the reserve is empty, `Get` always instantiates. A leak (rent without return) grows unbounded. Watch `ActiveCount` / `LogStats()`. |
| 5 | **Use-after-return aliasing** | Immediate double-`Return` is ignored (safe). But returning an object and then continuing to use it (or holding two references) can corrupt the pool. Drop your reference after `Return`. |
| 6 | **Return re-parents to the pool root** | The instance detaches from any custom parent on `Return`. Re-set the parent on the next `Get` if you depend on hierarchy. |
| 7 | **`localScale` is not reset** | `SetParent(parent, false)` keeps local scale; a parent with non-1 scale changes world scale. Reset scale in `OnSpawn()` if needed. |
| 8 | **Wrong / non-pooled objects are destroyed on Return** | Returning an object to the wrong pool, or one without a `PoolableKey`, destroys it by design. |
| 9 | **Key = `prefab.GetInstanceID()`** | Stable within a play session only. Do not persist pool keys. |
| 10 | **Editor-only warnings** | Setup warnings use `EditorDebug` and are stripped from builds. |

---

## API Reference

**Spawn / recycle** — `ObjectPoolManager`
```csharp
GameObject Get(GameObject prefab, Vector3 position, Transform parent = null)
GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
void       Return(GameObject instance)
```

**Pool management**
```csharp
void RegisterPrefab(GameObject prefab, int initialSize = 0, int maxSize = 200)
bool HasPool(GameObject prefab)
void ClearPoolForPrefab(GameObject prefab)
void ClearAll()
void LogStats()   // editor-only
```

**Lifecycle hook** — `IPoolable` (implement on the prefab)
```csharp
void OnSpawn()
void OnReturnToPool()
```

**Per-pool diagnostics** — `ObjectPool`
```csharp
int Count          // instances currently in the reserve
int TotalRented    // cumulative Get count
int TotalReturned  // cumulative Return count
int ActiveCount    // TotalRented - TotalReturned (rough live estimate)
```
