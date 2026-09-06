# Profiling

Map of the Profiling subsystem. Namespace `Game.Framework.Profiling`
(+ `.Editor`). Asmdef references only `Game.Framework.Diagnostics`, `Unity.InputSystem`, `UnityEngine.UI`.

**Diagnostics only.** Nothing in `Scripts/` references this folder and no gameplay decision reads
these numbers. Deleting it changes nothing about how the game plays.

---

## 1. Core idea

Three data sources with **three different costs**, one view that only formats them.

| Source | Cost | Cadence | Answers |
|---|---|---|---|
| `PerformanceSamplerSystem` | cheap | every frame | "how fast is this frame" — FPS, CPU, draw calls, GC, memory |
| `SceneMetricsSystem` | allocates, walks the scene | every `_sceneScanInterval` (1s) | "how heavy is this scene" — overdraw proxy, Physics2D weight |
| `SceneAuditService` | heavy, allocates a lot | **button only** | "*which object* is expensive" — named, ranked lists |

The split is the whole design: you can afford the first every frame, the second occasionally, the
third never on a timer.

```
PerformanceHudController (MonoBehaviour, plain Update)
    ├─ Sampler.Sample(unscaledDeltaTime)      every frame
    ├─ SceneMetrics.Scan(camera)              every _sceneScanInterval
    ├─ Sampler.RecomputeStats() → View.Present(sampler, sceneMetrics, budget)
    │                                          every _refreshInterval (0.25s)
    └─ RunSceneAudit() → SceneAuditService.Audit(camera) → LastAudit   on demand

PerformanceBudgetSO ──► colours every row (Normal / Warning / Critical)
Editor/PerformanceProfilerWindow ──reads the same sampler + audit, plus UnityStats
```

---

## 2. File map

| File | Kind | Summary |
|---|---|---|
| `PerformanceHudController.cs` | Controller (MonoBehaviour) | Owns sampler + scene metrics, toggle key, timers. `[RequireComponent(PerformanceOverlayView)]` |
| `PerformanceOverlayView.cs` | View (MonoBehaviour) | IMGUI panel + frame-time graph. Formats and colours; decides nothing |
| `PerformanceSamplerSystem.cs` | System (`IDisposable`) | 13 `ProfilerRecorder`s + a rolling frame-time ring buffer |
| `PerformanceSample.cs` | struct | One frame of raw counters. `Unavailable = -1` |
| `PerformanceStats.cs` | readonly struct | Aggregate over the window: avg / min / max / **1% low** |
| `SceneMetricsSystem.cs` | System | Interval scene scan: renderers, materials, sorting layers, Physics2D |
| `SceneAuditService.cs` | Service (static) | On-demand deep scan → ranked lists |
| `SceneAuditReport.cs` | data | Plain carrier; `Entry(Label, Detail, Value)` rows |
| `PerformanceBudgetSO.cs` | SO | Warning/critical thresholds per metric + `TargetFps` |
| `MetricBudget.cs` | struct + `BudgetLevel` | One metric's two thresholds and their evaluation |
| `Editor/PerformanceProfilerWindow.cs` | EditorWindow | `Tools/Profiling/Performance Profiler` |

---

## 3. Counter availability — the biggest gotcha

Most render and memory counters **only exist in the Editor and Development Builds**. A release
build still reports FPS and frame time; everything else reads `n/a`.

Counter names also differ between Unity versions, so every recorder is resolved from a list of
candidate names (`StartRecorder(category, label, params candidateNames)`) and degrades to
`PerformanceSample.Unavailable` instead of throwing. `ResolvedCounters` / `MissingCounters`
report the outcome — the editor window's **Counter Availability** foldout exists so a blank
number is never a mystery.

Two counters are read differently: `Canvas Batch Rebuild` and `Canvas Layout Rebuild` are
**markers**, so `ReadMarkerHits` takes the last sample's `Count` (how many times the marker fired
this frame), not its value.

---

## 4. The two non-obvious measurements

**Overdraw is a proxy, not real overdraw.** `ScreenCoverageRatio` sums each visible renderer's
on-screen bounds area and divides by the screen area. `1.0` = the visible sprites would cover the
screen exactly once if they never overlapped. In a 2D game of transparent quads that tracks real
fill cost closely enough to catch a background stack or a stray full-screen effect. Projection
behind a perspective camera is skipped rather than reported as garbage
(`SceneMetricsSystem.TryGetScreenArea`, shared with the audit so both count the same way).

**Collider "points" only counts path colliders.** `PolygonCollider2D`, `EdgeCollider2D`,
`CompositeCollider2D`. Box/circle/capsule have a fixed negligible cost, and inventing a weight
for them would only make the number harder to read.

**1% low ≠ worst frame.** `OnePercentLowFps` is the *mean of the worst 1%* of the window — it
correlates with "feels stuttery" far better than average FPS. Window default 300 frames (~5s at
60 FPS), clamped to 30…4096.

---

## 5. Scanning must not pollute what it measures

Both the interval scan and the audit allocate, which would show up as a fake GC spike on the next
frame. Each one calls `Sampler.SuppressNextGcAllocSample()`, so the next `GC / frame` reading
repeats the last value the *game* produced instead of the scanner's own garbage.

The IMGUI overlay costs a few draw calls of its own, so render counters read one or two higher
than a build without it. That is why the view is IMGUI in the first place: it needs no Canvas and
therefore stays out of the Canvas rebuild counters it is reporting.

---

## 6. Budget and colouring

`PerformanceBudgetSO` holds a `MetricBudget` (warning, critical) per metric plus `TargetFps`
(which also scales the frame-time graph).

- `Evaluate(value)` — bigger is worse: frame time, draw calls, GC, overdraw, collider points.
- `EvaluateInverted(value)` — smaller is worse: **FPS only**.
- **A threshold left at `0` disables that level**, so a half-filled budget never paints the whole
  overlay red.
- No budget assigned → every row renders in the neutral colour. Nothing breaks.

FPS is coloured from `OnePercentLowFps`, not from the instantaneous value.

Defaults are tuned for 2D URP at 1080p/60. To profile a weaker device, duplicate the asset and
lower the thresholds — do not edit the numbers in code.

---

## 7. Overlay rows (what the HUD shows)

```
FPS            now   avg · 1% low          ← coloured by 1% low
Frame          ms    worst ms
CPU main       ms
────────
Draw calls / Batches / SetPass / Tris·Verts
────────
Overdraw       ratio · visible sprites · unique materials   ("scan disabled" if off)
GC / frame     KB
Memory         MB total
Gfx / Tex      MB / MB
GC heap        MB
Canvas         batch rebuilds · layout rebuilds
────────
Bodies 2D      total · dynamic
Colliders 2D   count · points
```
Plus a frame-time history graph (one pixel column per frame) when `_showGraph` is on.

---

## 8. Editor window

`Tools/Profiling/Performance Profiler`. Repaints every 0.1s while in Play Mode. Four foldouts,
and deliberately **two sources of truth side by side**:

| Section | Source | Note |
|---|---|---|
| Editor Render Stats | `UnityStats` | What the Game view actually produced this frame. Needs nothing in the scene, but exists only in the Editor. Adds batching breakdown and used-texture memory |
| Runtime HUD | the scene's `PerformanceHudController` sampler | The same numbers a Development Build would report on a device — tune against these |
| Scene Audit | `SceneAuditService.Audit(Camera.main)` | Works without a HUD in the scene |
| Counter Availability | `ResolvedCounters` / `MissingCounters` | Why a number is blank |

The budget comes from the scene HUD when one exists, otherwise from the window's own field.

Audit output: totals (visible/active renderers, unique materials, sorting layers, coverage,
canvases/graphics, raycast targets, bodies, colliders/points, unique textures + MB) plus four
top-8 lists — **Largest On-Screen Renderers, Heaviest Collider2D, Busiest Canvases, Largest
Textures** — each row labelled `parent/child` with a unit-carrying detail string.

---

## 9. Setup and current state

1. Create a `PerformanceBudget` asset (`Assets > Create > Profiling > Performance Budget`).
2. Add one GameObject with `PerformanceHudController`; `PerformanceOverlayView` comes along via
   `[RequireComponent]`. Assign the budget.
3. `F3` toggles the overlay (`_toggleKey`, New Input System `Keyboard.current`; `Key.None`
   disables). `Run Scene Audit` is also a `[ContextMenu]` on the controller.

**Nothing is wired yet**: no `PerformanceBudget` asset exists and the HUD is in no scene or
prefab. `Camera.main` is resolved lazily — without a MainCamera the overdraw estimate stays at
zero and the rest still works.

---

## 10. Adding a metric

1. Add the field to `PerformanceSample` (`long`, default `Unavailable`).
2. Start a recorder in `PerformanceSamplerSystem.StartRecorders` with candidate names, read it in
   `Sample`, dispose it in `Dispose`.
3. Add a `MetricBudget` field + property to `PerformanceBudgetSO` if it should be colour-coded.
4. Add one `AddRow(...)` in `PerformanceOverlayView.BuildColumns`, choosing `Evaluate` or
   `EvaluateInverted`.
5. Optionally mirror it in `PerformanceProfilerWindow.DrawRuntimeStats`.

For a *scene-shaped* number (something you must walk the scene to learn), it belongs in
`SceneMetricsSystem` (aggregate, interval) or `SceneAuditService` (named rows, on demand) —
not in the per-frame sampler.
