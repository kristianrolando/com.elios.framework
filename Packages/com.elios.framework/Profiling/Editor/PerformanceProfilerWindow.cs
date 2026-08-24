using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Framework.Profiling.Editor
{
    // Editor-side companion to the in-game HUD. Two sources of truth sit next to each other here:
    //
    // - Editor Render Stats come from UnityStats and are the numbers the Game view actually
    //   produced this frame. They need nothing in the scene, but they only exist in the Editor.
    // - Runtime HUD reads the same PerformanceSamplerSystem the build would use, so what you tune
    //   against here is what a Development Build reports on a device.
    public class PerformanceProfilerWindow : UnityEditor.EditorWindow
    {
        private const string WindowTitle = "Performance Profiler";
        private const string MenuPath = "Tools/Profiling/Performance Profiler";
        private const string HudObjectName = "PerformanceHud";

        private const float BytesPerKilobyte = 1024f;
        private const float BytesPerMegabyte = 1024f * 1024f;
        private const float GraphHeight = 80f;
        private const int GraphColumnCount = 120;
        private const float RepaintInterval = 0.1f;
        private const string Unavailable = "n/a";

        private static readonly Color NormalColor = new Color(0.45f, 0.78f, 0.5f);
        private static readonly Color WarningColor = new Color(0.95f, 0.72f, 0.2f);
        private static readonly Color CriticalColor = new Color(0.95f, 0.36f, 0.34f);
        private static readonly Color GraphBackgroundColor = new Color(0.13f, 0.13f, 0.15f);
        private static readonly Color GraphTargetColor = new Color(0.4f, 0.42f, 0.48f);

        [SerializeField] private PerformanceBudgetSO _budget;
        [SerializeField] private Vector2 _scroll;
        [SerializeField] private bool _showRenderStats = true;
        [SerializeField] private bool _showRuntimeStats = true;
        [SerializeField] private bool _showAudit = true;
        [SerializeField] private bool _showCounterStatus;

        private PerformanceHudController _hud;
        private SceneAuditReport _report;
        private GUIStyle _valueStyle;
        private double _nextRepaintTime;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            PerformanceProfilerWindow window = GetWindow<PerformanceProfilerWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.Show();
        }

        // ══════════════════════════════════════════════
        // Unity Lifecycle
        // ══════════════════════════════════════════════

        private void Update()
        {
            if (!EditorApplication.isPlaying)
                return;

            if (EditorApplication.timeSinceStartup < _nextRepaintTime)
                return;

            _nextRepaintTime = EditorApplication.timeSinceStartup + RepaintInterval;
            Repaint();
        }

        private void OnGUI()
        {
            EnsureStyles();
            ResolveHud();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawBudgetField();
            DrawRenderStats();
            DrawRuntimeStats();
            DrawSceneAudit();
            DrawCounterStatus();

            EditorGUILayout.EndScrollView();
        }

        // ══════════════════════════════════════════════
        // Sections
        // ══════════════════════════════════════════════

        private void DrawBudgetField()
        {
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(_hud != null))
            {
                _budget = (PerformanceBudgetSO)EditorGUILayout.ObjectField(
                    new GUIContent("Budget", "Thresholds used to colour the numbers. While a HUD is in the scene its own budget is used."),
                    ResolveBudget(),
                    typeof(PerformanceBudgetSO),
                    false);
            }

            EditorGUILayout.Space();
        }

        private void DrawRenderStats()
        {
            _showRenderStats = EditorGUILayout.Foldout(_showRenderStats, "Editor Render Stats (Game view)", true);

            if (!_showRenderStats)
                return;

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to read the Game view render stats.", MessageType.Info);
                return;
            }

            PerformanceBudgetSO budget = ResolveBudget();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawMetric("Draw Calls", UnityStats.drawCalls.ToString(),
                    Evaluate(budget, budget != null ? budget.DrawCalls : default, UnityStats.drawCalls));

                DrawMetric("Batches", UnityStats.batches.ToString(),
                    Evaluate(budget, budget != null ? budget.Batches : default, UnityStats.batches));

                DrawMetric("SetPass Calls", UnityStats.setPassCalls.ToString(),
                    Evaluate(budget, budget != null ? budget.SetPassCalls : default, UnityStats.setPassCalls));

                DrawMetric("Triangles", UnityStats.triangles.ToString("N0"),
                    Evaluate(budget, budget != null ? budget.Triangles : default, UnityStats.triangles));

                DrawMetric("Vertices", UnityStats.vertices.ToString("N0"), BudgetLevel.Normal);

                EditorGUILayout.Space();

                DrawMetric("Dynamic Batched Calls", UnityStats.dynamicBatchedDrawCalls.ToString(), BudgetLevel.Normal);
                DrawMetric("Static Batched Calls", UnityStats.staticBatchedDrawCalls.ToString(), BudgetLevel.Normal);
                DrawMetric("Instanced Batched Calls", UnityStats.instancedBatchedDrawCalls.ToString(), BudgetLevel.Normal);

                EditorGUILayout.Space();

                DrawMetric("Used Textures", UnityStats.usedTextureCount.ToString(), BudgetLevel.Normal);
                DrawMetric("Used Texture Memory", FormatBytes(UnityStats.usedTextureMemorySize, BytesPerMegabyte, "MB"), BudgetLevel.Normal);
            }

            EditorGUILayout.Space();
        }

        private void DrawRuntimeStats()
        {
            _showRuntimeStats = EditorGUILayout.Foldout(_showRuntimeStats, "Runtime HUD", true);

            if (!_showRuntimeStats)
                return;

            if (_hud == null)
            {
                EditorGUILayout.HelpBox(
                    "No PerformanceHudController in the open scene. The HUD is what a Development Build would report; the editor stats above work without it.",
                    MessageType.Info);

                if (GUILayout.Button("Create Performance HUD In Scene"))
                    CreateHud();

                EditorGUILayout.Space();
                return;
            }

            PerformanceSamplerSystem sampler = _hud.Sampler;

            if (sampler == null)
            {
                EditorGUILayout.HelpBox("The HUD exists but has not started yet. Enter Play Mode.", MessageType.Info);
                EditorGUILayout.Space();
                return;
            }

            PerformanceBudgetSO budget = ResolveBudget();
            PerformanceSample sample = sampler.Latest;
            PerformanceStats stats = sampler.Stats;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawMetric("FPS", $"{sample.Fps:F1}",
                    budget != null ? budget.Fps.EvaluateInverted(sample.Fps) : BudgetLevel.Normal);

                DrawMetric("Average FPS", $"{stats.AverageFps:F1}", BudgetLevel.Normal);

                DrawMetric("1% Low FPS", $"{stats.OnePercentLowFps:F1}",
                    budget != null ? budget.Fps.EvaluateInverted(stats.OnePercentLowFps) : BudgetLevel.Normal);

                DrawMetric("Frame Time", $"{sample.FrameTimeMs:F2} ms",
                    Evaluate(budget, budget != null ? budget.FrameTimeMs : default, sample.FrameTimeMs));

                DrawMetric("Worst Frame Time", $"{stats.WorstFrameTimeMs:F2} ms",
                    Evaluate(budget, budget != null ? budget.FrameTimeMs : default, stats.WorstFrameTimeMs));

                DrawMetric("CPU Main Thread", sample.MainThreadMs >= 0f ? $"{sample.MainThreadMs:F2} ms" : Unavailable, BudgetLevel.Normal);

                DrawGraph(sampler, budget);

                EditorGUILayout.Space();

                DrawMetric("GC Alloc / Frame", FormatBytes(sample.GcAllocBytes, BytesPerKilobyte, "KB"),
                    EvaluateBytes(budget, budget != null ? budget.GcAllocKb : default, sample.GcAllocBytes, BytesPerKilobyte));

                DrawMetric("Total Used Memory", FormatBytes(sample.TotalUsedBytes, BytesPerMegabyte, "MB"),
                    EvaluateBytes(budget, budget != null ? budget.TotalMemoryMb : default, sample.TotalUsedBytes, BytesPerMegabyte));

                DrawMetric("GC Heap", FormatBytes(sample.GcUsedBytes, BytesPerMegabyte, "MB"), BudgetLevel.Normal);
                DrawMetric("Gfx Memory (VRAM)", FormatBytes(sample.GfxUsedBytes, BytesPerMegabyte, "MB"), BudgetLevel.Normal);
                DrawMetric("Texture Memory", FormatBytes(sample.TextureBytes, BytesPerMegabyte, "MB"), BudgetLevel.Normal);

                EditorGUILayout.Space();

                DrawMetric("Canvas Batch Rebuilds", FormatCounter(sample.CanvasBatchRebuilds),
                    Evaluate(budget, budget != null ? budget.CanvasRebuilds : default, sample.CanvasBatchRebuilds));

                DrawMetric("Canvas Layout Rebuilds", FormatCounter(sample.CanvasLayoutRebuilds), BudgetLevel.Normal);

                DrawSceneMetrics(_hud.SceneMetrics, budget);
            }

            EditorGUILayout.Space();
        }

        private void DrawSceneMetrics(SceneMetricsSystem sceneMetrics, PerformanceBudgetSO budget)
        {
            EditorGUILayout.Space();

            if (sceneMetrics == null || !sceneMetrics.HasScanned)
            {
                EditorGUILayout.LabelField("Scene scan", "disabled or not run yet");
                return;
            }

            DrawMetric("Overdraw Estimate", $"{sceneMetrics.ScreenCoverageRatio:F2}x",
                Evaluate(budget, budget != null ? budget.OverdrawRatio : default, sceneMetrics.ScreenCoverageRatio));

            DrawMetric("Visible Renderers", $"{sceneMetrics.VisibleRendererCount} of {sceneMetrics.ActiveRendererCount}", BudgetLevel.Normal);
            DrawMetric("Visible Sprites", sceneMetrics.VisibleSpriteCount.ToString(), BudgetLevel.Normal);
            DrawMetric("Unique Materials", sceneMetrics.UniqueMaterialCount.ToString(), BudgetLevel.Normal);
            DrawMetric("Sorting Layers Used", sceneMetrics.SortingLayerCount.ToString(), BudgetLevel.Normal);

            DrawMetric("Rigidbody2D", $"{sceneMetrics.Rigidbody2DCount}  ({sceneMetrics.DynamicBody2DCount} dynamic)", BudgetLevel.Normal);
            DrawMetric("Collider2D", sceneMetrics.Collider2DCount.ToString(), BudgetLevel.Normal);

            DrawMetric("Collider2D Points", sceneMetrics.Collider2DVertexCount.ToString(),
                Evaluate(budget, budget != null ? budget.Collider2DVertices : default, sceneMetrics.Collider2DVertexCount));
        }

        private void DrawSceneAudit()
        {
            _showAudit = EditorGUILayout.Foldout(_showAudit, "Scene Audit", true);

            if (!_showAudit)
                return;

            EditorGUILayout.HelpBox(
                "Walks the open scene and ranks the heaviest objects. Run it while the game is sitting on the frame that looks slow.",
                MessageType.None);

            if (GUILayout.Button("Run Scene Audit"))
                _report = SceneAuditService.Audit(Camera.main);

            if (_report == null)
            {
                EditorGUILayout.Space();
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Captured at", _report.GeneratedAtLabel);

                DrawMetric("Visible Renderers", $"{_report.VisibleRendererCount} of {_report.ActiveRendererCount}", BudgetLevel.Normal);
                DrawMetric("Unique Materials", _report.UniqueMaterialCount.ToString(), BudgetLevel.Normal);
                DrawMetric("Sorting Layers", _report.SortingLayerCount.ToString(), BudgetLevel.Normal);
                DrawMetric("Screen Coverage", $"{_report.ScreenCoverageRatio:F2}x", BudgetLevel.Normal);
                DrawMetric("Canvases / Graphics", $"{_report.CanvasCount} / {_report.GraphicCount}", BudgetLevel.Normal);
                DrawMetric("Raycast Targets", _report.RaycastTargetCount.ToString(), BudgetLevel.Normal);
                DrawMetric("Rigidbody2D", $"{_report.Rigidbody2DCount}  ({_report.DynamicBody2DCount} dynamic)", BudgetLevel.Normal);
                DrawMetric("Collider2D / Points", $"{_report.Collider2DCount} / {_report.Collider2DVertexCount}", BudgetLevel.Normal);
                DrawMetric("Unique Textures", $"{_report.UniqueTextureCount}  ({_report.UniqueTextureBytes / BytesPerMegabyte:F1} MB)", BudgetLevel.Normal);

                DrawEntries("Largest On-Screen Renderers", _report.HeaviestRenderers);
                DrawEntries("Heaviest Collider2D", _report.HeaviestColliders);
                DrawEntries("Busiest Canvases", _report.HeaviestCanvases);
                DrawEntries("Largest Textures", _report.LargestTextures);
            }

            EditorGUILayout.Space();
        }

        private void DrawCounterStatus()
        {
            _showCounterStatus = EditorGUILayout.Foldout(_showCounterStatus, "Counter Availability", true);

            if (!_showCounterStatus)
                return;

            if (_hud == null || _hud.Sampler == null)
            {
                EditorGUILayout.HelpBox("Enter Play Mode with a HUD in the scene to see which profiler counters resolved.", MessageType.Info);
                EditorGUILayout.Space();
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                IReadOnlyList<string> missing = _hud.Sampler.MissingCounters;

                if (missing.Count > 0)
                {
                    EditorGUILayout.LabelField("Unavailable", EditorStyles.boldLabel);

                    for (int i = 0; i < missing.Count; i++)
                        EditorGUILayout.LabelField("   " + missing[i]);

                    EditorGUILayout.HelpBox(
                        "These counters do not exist in this build. Most render, memory and UI counters require the Editor or a Development Build.",
                        MessageType.Warning);
                }

                IReadOnlyList<string> resolved = _hud.Sampler.ResolvedCounters;

                EditorGUILayout.LabelField("Resolved", EditorStyles.boldLabel);

                for (int i = 0; i < resolved.Count; i++)
                    EditorGUILayout.LabelField("   " + resolved[i]);
            }

            EditorGUILayout.Space();
        }

        // ══════════════════════════════════════════════
        // Internal Helpers
        // ══════════════════════════════════════════════

        private void DrawEntries(string title, List<SceneAuditReport.Entry> entries)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            if (entries.Count == 0)
            {
                EditorGUILayout.LabelField("   nothing to report");
                return;
            }

            for (int i = 0; i < entries.Count; i++)
                EditorGUILayout.LabelField("   " + entries[i].Label, entries[i].Detail);
        }

        private void DrawGraph(PerformanceSamplerSystem sampler, PerformanceBudgetSO budget)
        {
            Rect area = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(GraphHeight));

            EditorGUI.DrawRect(area, GraphBackgroundColor);

            int targetFps = budget != null ? budget.TargetFps : 0;

            if (targetFps <= 0 || sampler.HistoryCount == 0)
                return;

            float targetFrameTimeMs = PerformanceSample.MillisecondsPerSecond / targetFps;
            float scaleMs = targetFrameTimeMs * 2f;

            float targetY = area.yMax - area.height * (targetFrameTimeMs / scaleMs);
            EditorGUI.DrawRect(new Rect(area.x, targetY, area.width, 1f), GraphTargetColor);

            int historyCount = sampler.HistoryCount;
            int firstIndex = Mathf.Max(0, historyCount - GraphColumnCount);
            int visibleCount = historyCount - firstIndex;
            float columnWidth = area.width / GraphColumnCount;

            for (int i = 0; i < visibleCount; i++)
            {
                float frameTimeMs = sampler.GetHistoryFrameTimeMs(firstIndex + i);

                if (frameTimeMs <= 0f)
                    continue;

                float normalized = Mathf.Clamp01(frameTimeMs / scaleMs);
                float barHeight = area.height * normalized;

                Rect bar = new Rect(area.x + i * columnWidth, area.yMax - barHeight, columnWidth, barHeight);
                EditorGUI.DrawRect(bar, LevelColor(Evaluate(budget, budget != null ? budget.FrameTimeMs : default, frameTimeMs)));
            }
        }

        private void DrawMetric(string label, string value, BudgetLevel level)
        {
            Color previous = _valueStyle.normal.textColor;

            if (level != BudgetLevel.Normal)
                _valueStyle.normal.textColor = LevelColor(level);

            EditorGUILayout.LabelField(label, value, _valueStyle);

            _valueStyle.normal.textColor = previous;
        }

        private void EnsureStyles()
        {
            if (_valueStyle != null)
                return;

            _valueStyle = new GUIStyle(EditorStyles.label);
        }

        private void ResolveHud()
        {
            if (_hud != null)
                return;

            _hud = FindFirstObjectByType<PerformanceHudController>(FindObjectsInactive.Include);
        }

        private PerformanceBudgetSO ResolveBudget()
        {
            if (_hud != null && _hud.Budget != null)
                return _hud.Budget;

            return _budget;
        }

        private void CreateHud()
        {
            GameObject hostObject = new GameObject(HudObjectName);

            hostObject.AddComponent<PerformanceOverlayView>();
            hostObject.AddComponent<PerformanceHudController>();

            Undo.RegisterCreatedObjectUndo(hostObject, "Create Performance HUD");
            Selection.activeGameObject = hostObject;

            _hud = hostObject.GetComponent<PerformanceHudController>();
        }

        private static BudgetLevel Evaluate(PerformanceBudgetSO budget, MetricBudget metric, float value)
        {
            return budget == null ? BudgetLevel.Normal : metric.Evaluate(value);
        }

        private static BudgetLevel EvaluateBytes(PerformanceBudgetSO budget, MetricBudget metric, long bytes, float divisor)
        {
            if (budget == null || !PerformanceSample.HasValue(bytes))
                return BudgetLevel.Normal;

            return metric.Evaluate(bytes / divisor);
        }

        private static Color LevelColor(BudgetLevel level)
        {
            if (level == BudgetLevel.Critical)
                return CriticalColor;

            if (level == BudgetLevel.Warning)
                return WarningColor;

            return NormalColor;
        }

        private static string FormatBytes(long bytes, float divisor, string unit)
        {
            return PerformanceSample.HasValue(bytes) ? $"{bytes / divisor:F2} {unit}" : Unavailable;
        }

        private static string FormatCounter(long counter)
        {
            return PerformanceSample.HasValue(counter) ? counter.ToString() : Unavailable;
        }
    }
}
