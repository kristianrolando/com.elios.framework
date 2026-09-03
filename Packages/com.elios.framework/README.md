# Framework — Engine Layer

Entry point for `Assets/_Main/Framework`. Five self-contained subsystems that gameplay stands
on. Each has its own asmdef and its own README with the full API; **this file is the map: what
each one is for, how they boot, and the rules that cut across all of them.**

| Subsystem | Namespace | Replaces | Users in `Scripts/` | Deep doc |
|---|---|---|---|---|
| **Ticking** | `Game.Framework.Ticking` | `Update` / `FixedUpdate` / `LateUpdate` | 17 files | [README](Ticking/README.md) |
| **ObjectPooling** | `Game.Framework.ObjectPooling` | `Instantiate` / `Destroy` | 13 files | [README](ObjectPooling/README.md) |
| **SaveSystem** | `Game.Framework.SaveSystem` | `PlayerPrefs` | 6 files | [README](SaveSystem/README.md) |
| **Profiling** | `Game.Framework.Profiling` | Unity Profiler window (in-build) | 0 — scene-only HUD | [README](Profiling/README.md) |
| **EventBus** | `Game.Framework.EventBus` | direct references between systems | **0 — built, not adopted** | [README](EventBus/README.md) |

Only the first three are load-bearing. Profiling is diagnostics, and EventBus is available but
has no callers: normal in-scene communication still goes through a plain C# `event` on a Manager
(see `/CLAUDE.md`). Reach for the bus only when two systems genuinely must not see each other.

---

## 1. Dependency rule

Every asmdef references **at most `Game.Utils`** — Profiling also takes `Unity.InputSystem` and
`UnityEngine.UI`, and EventBus references nothing at all. Nothing here references gameplay, and
nothing here knows what a room, an enemy, or a player is.

`Game.Utils` itself pulls in `Lofelt.NiceVibrations`, `MoreMountains.Tools` and `UnityEngine.UI`
(haptics and the screen fader), so those ride along into every framework assembly.

```
Game.Scripts (default assembly)  ──►  Game.Framework.*  ──►  Game.Utils
                                                   ▲
                        Game.Framework.*.Editor ───┘   (SaveSystem, Profiling; Editor platform only)
```

Adding a `Game.Gameplay.*` reference to any framework asmdef is a design error: push the
gameplay-specific part up into `Scripts/` instead.

---

## 2. Boot order (one frame)

```
SubsystemRegistration   TickManager.ResetStatics()          ← statics wiped
                        ObjectPoolManager.ResetStatics()    ← statics wiped
                        Bus<TEnum> reset                    ← statics wiped, every enum + arity
BeforeSceneLoad         SaveBootstrap.Bootstrap()
                            Save.InitializeIfNeeded()       ← loads last-used slot from disk
                            SaveLifecycleHandler.EnsureExists()
                            SceneManager.sceneLoaded += flush-if-dirty
first Awake             gameplay singletons (-100 … -85)
first registration      TickManager creates TickDriver (order -90, HideFlags.DontSave)
first pool rent         ObjectPoolManager creates __ObjectPoolRoot__
```

**Why `ResetStatics` exists:** with *Enter Play Mode without Domain Reload* enabled, statics keep
their values from the previous Play session. Every static holder in this folder resets itself
under `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`. Copy that pattern for any new
static state (gameplay's `RunSession` already does).

### Scene change
| Subsystem | What happens |
|---|---|
| Ticking | `IsPaused = false`, destroyed targets purged from all four channels, `TickDriver` rebuilt |
| EventBus | Nothing — subscribers persist. Each listener must `Unsubscribe` in its own `OnDestroy` |
| ObjectPooling | **All pools cleared**, `_poolRoot` dropped (it died with the old scene) |
| SaveSystem | Flush if dirty (`sceneLoaded`) |

Pools do not survive a scene load — never cache a `GameObject` rented before a reload.

---

## 3. Ticking

One driver, four channels. `TickDriver` is *the only* `Update`/`FixedUpdate`/`LateUpdate` in the
project, at execution order **-90**, so all ticks run before the remaining plain MonoBehaviours.

| Interface | Channel | deltaTime | Stops when paused |
|---|---|---|---|
| `ITickable` | scaled | `Time.deltaTime` | yes |
| `IUnscaledTickable` | unscaled | `Time.unscaledDeltaTime` | **no** — UI, menus, timers |
| `IFixedTickable` | fixed | `Time.fixedDeltaTime` | yes — everything touching a Rigidbody |
| `ILateTickable` | late | `Time.deltaTime` | yes — cameras, anchors, anything reading final positions |

- Register in `OnEnable`, unregister in `OnDisable`. Register/unregister is safe **from inside a
  tick**: the change is applied after the current channel finishes iterating.
- `RegisterTick(target, interval)` — a positive interval throttles the target and hands it the
  *real* elapsed time, so accumulated movement stays correct.
- `TickManager.IsPaused` freezes scaled/fixed/late only. It is **independent of
  `Time.timeScale`**: it stops this loop, not Unity's physics, animators, or particles. Gameplay
  pause therefore sets both (`TimeScaleManager` owns `Time.timeScale`).
- Channels are raw arrays iterated by `ref` so throttle counters are written in place — that is
  why this is not a `List<T>`.

---

## 4. ObjectPooling

`ObjectPoolManager` is a static facade over one `ObjectPool` per prefab, keyed by
`prefab.GetInstanceID()`. Pools are created on first `Get`; there is no setup step.

```
Get(prefab, pos, rot, parent) → pool exists? → dequeue : Instantiate
                              → PoolableKey stamped, IPoolable[] cached on first spawn
                              → SetActive(true) → OnSpawn() on root + children
Return(instance)              → no PoolableKey?      → Destroy
                              → key from another pool → Destroy
                              → already in pool       → ignored
                              → reserve full (max 200)→ Destroy the surplus
                              → else OnReturnToPool() → SetActive(false) → enqueue
```

- `IPoolable.OnSpawn` / `OnReturnToPool` are the reset hooks, on **root or any child**.
  A pooled object must reset every field it mutates — a stale timer or a disabled renderer
  survives recycling.
- `RegisterPrefab(prefab, initialSize, maxSize)` prewarms; optional.
- `ActiveCount = TotalRented - TotalReturned`; `LogStats()` (editor-only) dumps per-pool
  counters — a climbing `ActiveCount` is a leak.
- Returning a scene object or a non-pooled instance is safe: it is just destroyed.

**The trap this project already hit:** an object whose `OnDisable` stops the coroutine that would
have called `Return` never comes back. Keep the return path independent of enable state, or drain
before deactivating a parent (see `RoomController.IsDrained()`).

---

## 5. SaveSystem

PlayerPrefs-shaped API (`Save.Get/Set/HasKey/DeleteKey`) over a per-slot JSON database.

```
Save (static facade)
  └─ SaveService (engine: dirty tracking, slots, migration, reserved 'sys/' namespace)
       ├─ ISaveSerializer  → NewtonsoftSaveSerializer (+ UnityJsonConverters: Vector*, Quaternion, Color*)
       └─ ISaveStorage     → FileSaveStorage → [ISaveEncryptor → AesSaveEncryptor]
                                             → SaveFileSystem (WebGL IndexedDB sync)
```

| Piece | Fact worth knowing |
|---|---|
| Layout | `persistentDataPath/save_v1/slot_1.json` (+ `.json.bak`) |
| `SaveDatabase` | `meta` (`SaveMeta`: schemaVersion, created/modified, appVersion) + `Dictionary<string, SaveEntry>` |
| `SaveEntry` | opaque `value` token owned by the serializer — never assume its concrete type; plus `typeName` and `modifiedUtc` for debugging |
| Writes | **Atomic**: temp file → backup the old one → swap. A crashed write is recoverable from `.bak`; leftover temp files are cleaned on startup |
| Encryption | Opt-in via `Save.ConfigureEncryption(password)`. AES-CBC + PBKDF2 + HMAC, `"GSAVEC1\0"` magic header. Reads auto-detect, so old plaintext saves still load. A baked-in password is *friction*, not anti-cheat |
| Slots | `SetActiveSlot`, `ListSlots`, `TryGetSlotMeta` (peek without loading — this is what a load-game screen uses). The active slot is remembered in **PlayerPrefs** (`save.last_slot`, default `slot_1`) and reloaded on boot |
| Migration | `SaveMigrator` steps a loaded DB one version at a time to `SaveConstants.CurrentSchemaVersion` (currently 1, no migrations registered) |
| Keys | Fixed keys in `SaveKeys`; dynamic ones via `SaveKeyBuilder` (normalizes/joins segments). `sys/` is reserved for the framework and survives `DeleteAllKeys` |
| Flushing | `Set` only marks dirty. Disk writes happen on `Flush()`, `autoFlush: true`, scene load, and app pause/focus/quit (`SaveLifecycleHandler` — the primary signal on mobile and WebGL, where quit is unreliable) |
| Editor | *Save File Browser* window reads the files directly and never touches `Save`/`SaveService`, so opening it cannot initialize or mutate runtime state |

Audio volume deliberately stays on **PlayerPrefs** (`AudioSaveKeys`): it is a device setting that
must be identical across every slot.

---

## 6. Profiling

In-build performance HUD plus an editor window. Diagnostics only — no gameplay reads it, nothing
in `Scripts/` references it. Drop `PerformanceHudController` (requires `PerformanceOverlayView`)
on one GameObject; `F3` toggles it.

| File | Role |
|---|---|
| `PerformanceHudController` | Owns the sampler + scene scanner, handles the toggle key, pushes to the view on `_refreshInterval` (0.25s) so numbers stay readable. Uses a plain `Update` — Profiling does not reference Ticking |
| `PerformanceSamplerSystem` | Owns the `ProfilerRecorder` handles and the rolling frame window. Object-agnostic, so HUD and editor window share it |
| `PerformanceSample` | One frame of raw counters. `Unavailable = -1` |
| `PerformanceStats` | Aggregate over the window. `OnePercentLowFps` = mean of the worst 1% of frames — the number that tracks "feels stuttery" |
| `SceneMetricsSystem` | Periodic scan for what Unity exposes no counter for: 2D screen coverage (overdraw proxy) and Physics2D weight. Allocates, so it runs on an interval |
| `SceneAuditService` → `SceneAuditReport` | On-demand deep scan naming the heaviest renderers/colliders/canvases. Heavy by design — button-triggered, never on a timer |
| `PerformanceBudgetSO` + `MetricBudget` | Warning/critical thresholds that colour the readout. Defaults target 2D URP 1080p/60; duplicate the asset to profile a weaker device |
| `PerformanceOverlayView` | Draws the overlay in a screen corner |
| `Editor/PerformanceProfilerWindow` | Editor companion. Shows Unity's `UnityStats` **and** the same sampler a Development Build would use, side by side |

**Most render/memory counters only exist in the Editor and Development Builds.** A release build
still reports FPS and frame time; everything else resolves to `Unavailable` and prints `n/a`.
Recorders are resolved from candidate name lists because counter names differ between Unity
versions — `ResolvedCounters` / `MissingCounters` say which ones landed.

---

## 7. EventBus

`Bus<TEnum>` is a static, enum-keyed pub/sub: one enum type = one independent channel, with
`Subscribe` / `Trigger` / `Unsubscribe` overloads for 0–4 arguments.

```csharp
public enum CombatEvent { OnHit, OnEnemyDied }

Bus<CombatEvent>.Subscribe<int>(CombatEvent.OnHit, HandleHit);   // Start / OnEnable
Bus<CombatEvent>.Trigger(CombatEvent.OnHit, 50);                 // anywhere
Bus<CombatEvent>.Unsubscribe<int>(CombatEvent.OnHit, HandleHit); // OnDestroy
```

**Status: available, unused.** No file in `Scripts/` calls it. The project's default remains a
plain C# `event` on the publishing Manager, which is type-checked and traceable in the IDE.

Three traps worth knowing before adopting it:

- **The parameter types are part of the key.** Subscribing with `<int>` and triggering with
  `<float>` are two different events that never reach each other, with no compile-time or
  runtime error.
- **`Unsubscribe` is mandatory.** Unlike `TickManager`, nothing detects a destroyed listener; a
  forgotten `-=` keeps invoking a fake-null object.
- **One throwing subscriber stops the rest of the chain** for that event — standard multicast
  delegate behaviour, but it matters more when the publisher cannot see who is listening.

Statics reset automatically on entry to Play Mode; `ClearAll()` is there for a manual hard reset.

---

## 8. Rules when editing anything here

- New static state → reset it in a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`.
- Never reference gameplay assemblies; keep these subsystems object-agnostic.
- Editor-only code goes in the subsystem's `Editor/` folder with its Editor-platform asmdef, and
  a custom inspector inside a `*.Editor` namespace must write `: UnityEditor.Editor` in full.
- Diagnostic logs go through `EditorDebug` (stripped from builds); only genuine persistence
  failures use `Debug.LogError` (see `SaveLog` for the split).
- Update the subsystem's own README when you change its public API — this file only maps.
