using System.Text;
using UnityEngine;

namespace Elios.Framework.Profiling
{
    public enum ScreenCorner
    {
        TopLeft = 0,
        TopRight = 1,
        BottomLeft = 2,
        BottomRight = 3
    }

    // Draws the numbers. No sampling, no timers, no decisions about what is slow: it is handed a
    // sampler and a budget, formats what it finds, and paints each row with the budget colour.
    //
    // IMGUI on purpose. It needs no Canvas, no prefab and no scene wiring, and it stays out of the
    // Canvas rebuild counters it is reporting. It does cost a handful of draw calls of its own, so
    // the render numbers read one or two higher than they will in a build without the overlay.
    [DisallowMultipleComponent]
    public class PerformanceOverlayView : MonoBehaviour
    {
        private const float BytesPerKilobyte = 1024f;
        private const float BytesPerMegabyte = 1024f * 1024f;
        private const float TargetLineHeightFraction = 0.5f;
        private const int ThousandsThreshold = 1000;
        private const int MillionsThreshold = 1000000;
        private const string Unavailable = "n/a";
        private const string Separator = "──────────────";

        private static readonly Color NormalColor = new Color(0.73f, 0.94f, 0.77f);
        private static readonly Color WarningColor = new Color(1f, 0.84f, 0.36f);
        private static readonly Color CriticalColor = new Color(1f, 0.43f, 0.40f);
        private static readonly Color MutedColor = new Color(0.64f, 0.67f, 0.73f);

        private static readonly string NormalHex = ColorUtility.ToHtmlStringRGB(NormalColor);
        private static readonly string WarningHex = ColorUtility.ToHtmlStringRGB(WarningColor);
        private static readonly string CriticalHex = ColorUtility.ToHtmlStringRGB(CriticalColor);
        private static readonly string MutedHex = ColorUtility.ToHtmlStringRGB(MutedColor);

        [Header("Placement")]

        [SerializeField, Tooltip("Which screen corner the panel is anchored to.")]
        private ScreenCorner _corner = ScreenCorner.TopLeft;

        [SerializeField, Tooltip("Distance in pixels between the panel and the screen edges.")]
        private Vector2 _margin = new Vector2(12f, 12f);

        [SerializeField, Min(120f), Tooltip("Panel width in pixels.")]
        private float _panelWidth = 300f;

        [SerializeField, Min(8), Tooltip("Font size of the readout.")]
        private int _fontSize = 12;

        [Header("Graph")]

        [SerializeField, Tooltip("Draw the frame time history graph under the numbers.")]
        private bool _showGraph = true;

        [SerializeField, Min(32), Tooltip("Frame time graph width in pixels. One pixel column is one frame.")]
        private int _graphWidth = 276;

        [SerializeField, Min(16), Tooltip("Frame time graph height in pixels.")]
        private int _graphHeight = 52;

        private readonly StringBuilder _labelBuilder = new StringBuilder();
        private readonly StringBuilder _valueBuilder = new StringBuilder();

        private GUIStyle _labelStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _panelStyle;
        private Texture2D _panelTexture;
        private Texture2D _graphTexture;
        private Color32[] _graphPixels;

        private string _labelColumn = string.Empty;
        private string _valueColumn = string.Empty;
        private string _toggleHint = string.Empty;
        private int _lineCount;
        private bool _isVisible;
        private bool _hasContent;

        // ══════════════════════════════════════════════
        // Unity Lifecycle
        // ══════════════════════════════════════════════

        private void OnGUI()
        {
            if (!_isVisible || !_hasContent)
                return;

            EnsureStyles();

            float lineHeight = _valueStyle.lineHeight;
            float graphHeight = _showGraph && _graphTexture != null ? _graphHeight + _margin.y : 0f;
            float panelHeight = _lineCount * lineHeight + _margin.y * 2f + graphHeight;

            Rect panel = BuildPanelRect(panelHeight);

            GUI.Box(panel, GUIContent.none, _panelStyle);

            Rect content = new Rect(
                panel.x + _margin.x,
                panel.y + _margin.y,
                panel.width - _margin.x * 2f,
                _lineCount * lineHeight);

            GUI.Label(content, _labelColumn, _labelStyle);
            GUI.Label(content, _valueColumn, _valueStyle);

            if (!_showGraph || _graphTexture == null)
                return;

            Rect graph = new Rect(content.x, content.yMax + _margin.y * 0.5f, _graphWidth, _graphHeight);
            GUI.DrawTexture(graph, _graphTexture);
        }

        private void OnDestroy()
        {
            if (_graphTexture != null)
                Destroy(_graphTexture);

            if (_panelTexture != null)
                Destroy(_panelTexture);
        }

        // ══════════════════════════════════════════════
        // Public API
        // ══════════════════════════════════════════════

        public void SetVisible(bool isVisible)
        {
            _isVisible = isVisible;
        }

        public void SetToggleHint(string hint)
        {
            _toggleHint = hint;
        }

        public void Present(PerformanceSamplerSystem sampler, SceneMetricsSystem sceneMetrics, PerformanceBudgetSO budget)
        {
            if (sampler == null)
                return;

            BuildColumns(sampler, sceneMetrics, budget);

            if (_showGraph)
                BuildGraph(sampler, budget);

            _hasContent = true;
        }

        // ══════════════════════════════════════════════
        // Internal Helpers
        // ══════════════════════════════════════════════

        private void BuildColumns(PerformanceSamplerSystem sampler, SceneMetricsSystem sceneMetrics, PerformanceBudgetSO budget)
        {
            PerformanceSample sample = sampler.Latest;
            PerformanceStats stats = sampler.Stats;

            _labelBuilder.Clear();
            _valueBuilder.Clear();
            _lineCount = 0;

            AddRow("PERFORMANCE", Muted(_toggleHint));
            AddRow(string.Empty, Muted(Separator));

            AddRow("FPS", Colored(
                $"{sample.Fps:F1}   avg {stats.AverageFps:F1} · 1% low {stats.OnePercentLowFps:F1}",
                EvaluateFps(budget, stats.OnePercentLowFps)));

            AddRow("Frame", Colored(
                $"{sample.FrameTimeMs:F1} ms   worst {stats.WorstFrameTimeMs:F1} ms",
                EvaluateHigher(budget, budget != null ? budget.FrameTimeMs : default, sample.FrameTimeMs)));

            AddRow("CPU main", Muted(FormatMilliseconds(sample.MainThreadMs)));

            AddRow(string.Empty, Muted(Separator));

            AddRow("Draw calls", ColoredCounter(sample.DrawCalls, budget, budget != null ? budget.DrawCalls : default));
            AddRow("Batches", ColoredCounter(sample.Batches, budget, budget != null ? budget.Batches : default));
            AddRow("SetPass", ColoredCounter(sample.SetPassCalls, budget, budget != null ? budget.SetPassCalls : default));
            AddRow("Tris / Verts", Muted($"{FormatCount(sample.Triangles)} / {FormatCount(sample.Vertices)}"));

            AddRow(string.Empty, Muted(Separator));

            AddOverdrawRow(sceneMetrics, budget);

            AddRow("GC / frame", ColoredBytes(sample.GcAllocBytes, BytesPerKilobyte, "KB",
                budget, budget != null ? budget.GcAllocKb : default));

            AddRow("Memory", ColoredBytes(sample.TotalUsedBytes, BytesPerMegabyte, "MB",
                budget, budget != null ? budget.TotalMemoryMb : default));

            AddRow("Gfx / Tex", Muted(
                $"{FormatBytes(sample.GfxUsedBytes, BytesPerMegabyte, "MB")} / {FormatBytes(sample.TextureBytes, BytesPerMegabyte, "MB")}"));

            AddRow("GC heap", Muted(FormatBytes(sample.GcUsedBytes, BytesPerMegabyte, "MB")));

            AddRow("Canvas", ColoredCounter(sample.CanvasBatchRebuilds, budget, budget != null ? budget.CanvasRebuilds : default,
                $" batch · {FormatCounter(sample.CanvasLayoutRebuilds)} layout"));

            AddPhysicsRow(sceneMetrics, budget);

            _labelColumn = _labelBuilder.ToString();
            _valueColumn = _valueBuilder.ToString();
        }

        private void AddOverdrawRow(SceneMetricsSystem sceneMetrics, PerformanceBudgetSO budget)
        {
            if (sceneMetrics == null || !sceneMetrics.HasScanned)
            {
                AddRow("Overdraw", Muted("scan disabled"));
                return;
            }

            BudgetLevel level = EvaluateHigher(budget, budget != null ? budget.OverdrawRatio : default, sceneMetrics.ScreenCoverageRatio);

            AddRow("Overdraw", Colored(
                $"{sceneMetrics.ScreenCoverageRatio:F2}x   {sceneMetrics.VisibleSpriteCount} sprites · {sceneMetrics.UniqueMaterialCount} mats",
                level));
        }

        private void AddPhysicsRow(SceneMetricsSystem sceneMetrics, PerformanceBudgetSO budget)
        {
            AddRow(string.Empty, Muted(Separator));

            if (sceneMetrics == null || !sceneMetrics.HasScanned)
            {
                AddRow("Physics 2D", Muted("scan disabled"));
                return;
            }

            AddRow("Bodies 2D", Muted($"{sceneMetrics.Rigidbody2DCount}   {sceneMetrics.DynamicBody2DCount} dynamic"));

            BudgetLevel level = EvaluateHigher(budget, budget != null ? budget.Collider2DVertices : default, sceneMetrics.Collider2DVertexCount);

            AddRow("Colliders 2D", Colored(
                $"{sceneMetrics.Collider2DCount}   {sceneMetrics.Collider2DVertexCount} points",
                level));
        }

        private void AddRow(string label, string value)
        {
            if (_lineCount > 0)
            {
                _labelBuilder.Append('\n');
                _valueBuilder.Append('\n');
            }

            _labelBuilder.Append(label);
            _valueBuilder.Append(value);
            _lineCount++;
        }

        private void BuildGraph(PerformanceSamplerSystem sampler, PerformanceBudgetSO budget)
        {
            EnsureGraphTexture();

            int width = _graphTexture.width;
            int height = _graphTexture.height;

            Color32 background = new Color32(18, 18, 22, 210);
            Color32 gridColor = new Color32(70, 74, 84, 255);

            for (int i = 0; i < _graphPixels.Length; i++)
                _graphPixels[i] = background;

            int targetFps = budget != null ? budget.TargetFps : 0;
            float targetFrameTimeMs = targetFps > 0
                ? PerformanceSample.MillisecondsPerSecond / targetFps
                : 0f;

            // The target frame time sits at half height, so the full graph spans twice the budget.
            float scaleMs = targetFrameTimeMs > 0f ? targetFrameTimeMs / TargetLineHeightFraction : 0f;

            if (targetFrameTimeMs > 0f)
            {
                int targetRow = Mathf.Clamp(Mathf.RoundToInt(height * TargetLineHeightFraction), 0, height - 1);
                for (int x = 0; x < width; x++)
                    _graphPixels[targetRow * width + x] = gridColor;
            }

            int historyCount = sampler.HistoryCount;
            int firstIndex = Mathf.Max(0, historyCount - width);

            for (int x = 0; x < width; x++)
            {
                int historyIndex = firstIndex + x;

                if (historyIndex >= historyCount)
                    break;

                float frameTimeMs = sampler.GetHistoryFrameTimeMs(historyIndex);

                if (frameTimeMs <= 0f || scaleMs <= 0f)
                    continue;

                int barHeight = Mathf.Clamp(Mathf.RoundToInt(frameTimeMs / scaleMs * height), 1, height);
                Color32 barColor = ToColor32(BudgetColor(EvaluateHigher(budget, budget != null ? budget.FrameTimeMs : default, frameTimeMs)));

                for (int y = 0; y < barHeight; y++)
                    _graphPixels[y * width + x] = barColor;
            }

            _graphTexture.SetPixels32(_graphPixels);
            _graphTexture.Apply(false);
        }

        private void EnsureGraphTexture()
        {
            if (_graphTexture != null && _graphTexture.width == _graphWidth && _graphTexture.height == _graphHeight)
                return;

            if (_graphTexture != null)
                Destroy(_graphTexture);

            _graphTexture = new Texture2D(_graphWidth, _graphHeight, TextureFormat.RGBA32, false)
            {
                name = "PerformanceOverlayGraph",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            _graphPixels = new Color32[_graphWidth * _graphHeight];
        }

        private void EnsureStyles()
        {
            if (_labelStyle != null)
                return;

            _panelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "PerformanceOverlayPanel",
                hideFlags = HideFlags.HideAndDontSave
            };
            _panelTexture.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.07f, 0.82f));
            _panelTexture.Apply(false);

            _panelStyle = new GUIStyle(GUIStyle.none);
            _panelStyle.normal.background = _panelTexture;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = _fontSize,
                richText = true,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false
            };
            _labelStyle.normal.textColor = MutedColor;

            _valueStyle = new GUIStyle(_labelStyle)
            {
                alignment = TextAnchor.UpperRight
            };
        }

        private Rect BuildPanelRect(float panelHeight)
        {
            float x = _corner == ScreenCorner.TopRight || _corner == ScreenCorner.BottomRight
                ? Screen.width - _panelWidth - _margin.x
                : _margin.x;

            float y = _corner == ScreenCorner.BottomLeft || _corner == ScreenCorner.BottomRight
                ? Screen.height - panelHeight - _margin.y
                : _margin.y;

            return new Rect(x, y, _panelWidth, panelHeight);
        }

        // ══════════════════════════════════════════════
        // Formatting
        // ══════════════════════════════════════════════

        private static BudgetLevel EvaluateFps(PerformanceBudgetSO budget, float fps)
        {
            return budget == null ? BudgetLevel.Normal : budget.Fps.EvaluateInverted(fps);
        }

        private static BudgetLevel EvaluateHigher(PerformanceBudgetSO budget, MetricBudget metric, float value)
        {
            return budget == null ? BudgetLevel.Normal : metric.Evaluate(value);
        }

        private static Color BudgetColor(BudgetLevel level)
        {
            if (level == BudgetLevel.Critical)
                return CriticalColor;

            if (level == BudgetLevel.Warning)
                return WarningColor;

            return NormalColor;
        }

        private static Color32 ToColor32(Color color)
        {
            return new Color32(
                (byte)(color.r * byte.MaxValue),
                (byte)(color.g * byte.MaxValue),
                (byte)(color.b * byte.MaxValue),
                byte.MaxValue);
        }

        private static string Colored(string text, BudgetLevel level)
        {
            if (level == BudgetLevel.Critical)
                return $"<color=#{CriticalHex}>{text}</color>";

            if (level == BudgetLevel.Warning)
                return $"<color=#{WarningHex}>{text}</color>";

            return $"<color=#{NormalHex}>{text}</color>";
        }

        private static string Muted(string text)
        {
            return $"<color=#{MutedHex}>{text}</color>";
        }

        private static string ColoredCounter(long counter, PerformanceBudgetSO budget, MetricBudget metric, string suffix = "")
        {
            if (!PerformanceSample.HasValue(counter))
                return Muted(Unavailable);

            return Colored($"{counter}{suffix}", EvaluateHigher(budget, metric, counter));
        }

        private static string ColoredBytes(long bytes, float divisor, string unit, PerformanceBudgetSO budget, MetricBudget metric)
        {
            if (!PerformanceSample.HasValue(bytes))
                return Muted(Unavailable);

            float scaled = bytes / divisor;
            return Colored($"{scaled:F1} {unit}", EvaluateHigher(budget, metric, scaled));
        }

        private static string FormatBytes(long bytes, float divisor, string unit)
        {
            return PerformanceSample.HasValue(bytes) ? $"{bytes / divisor:F1} {unit}" : Unavailable;
        }

        private static string FormatCounter(long counter)
        {
            return PerformanceSample.HasValue(counter) ? counter.ToString() : Unavailable;
        }

        private static string FormatCount(long counter)
        {
            if (!PerformanceSample.HasValue(counter))
                return Unavailable;

            if (counter >= MillionsThreshold)
                return $"{counter / (float)MillionsThreshold:F1}M";

            if (counter >= ThousandsThreshold)
                return $"{counter / (float)ThousandsThreshold:F1}K";

            return counter.ToString();
        }

        private static string FormatMilliseconds(float milliseconds)
        {
            return milliseconds >= 0f ? $"{milliseconds:F2} ms" : Unavailable;
        }
    }
}
