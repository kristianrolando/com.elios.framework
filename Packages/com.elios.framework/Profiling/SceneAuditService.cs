using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace Game.Framework.Profiling
{
    // On-demand deep scan of the active scene. Answers "what exactly is making this frame
    // expensive" with named objects instead of aggregate numbers.
    //
    // Heavy and allocating by design: it is triggered by a button, never on a timer. Run it while
    // the game sits on the frame that looks slow.
    public static class SceneAuditService
    {
        public const int DefaultTopCount = 8;

        private const float BytesPerMegabyte = 1024f * 1024f;
        private const float PercentScale = 100f;
        private const string TimeLabelFormat = "HH:mm:ss";

        private static readonly int MainTexPropertyId = Shader.PropertyToID("_MainTex");

        private static readonly Comparison<SceneAuditReport.Entry> DescendingByValue =
            (left, right) => right.Value.CompareTo(left.Value);

        private static readonly Dictionary<int, int> GraphicsPerCanvas = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> RaycastTargetsPerCanvas = new Dictionary<int, int>();
        private static readonly Dictionary<int, Canvas> CanvasById = new Dictionary<int, Canvas>();
        private static readonly Dictionary<int, Texture> TexturesById = new Dictionary<int, Texture>();
        private static readonly HashSet<int> MaterialIds = new HashSet<int>();
        private static readonly HashSet<int> SortingLayerIds = new HashSet<int>();

        // ══════════════════════════════════════════════
        // Public API
        // ══════════════════════════════════════════════

        public static SceneAuditReport Audit(Camera camera, int topCount = DefaultTopCount)
        {
            SceneAuditReport report = new SceneAuditReport();
            report.GeneratedAtLabel = DateTime.Now.ToString(TimeLabelFormat);

            AuditRenderers(camera, report);
            AuditColliders2D(report);
            AuditCanvases(report);
            AuditTextures(report);

            TrimToTop(report.HeaviestRenderers, topCount);
            TrimToTop(report.HeaviestColliders, topCount);
            TrimToTop(report.HeaviestCanvases, topCount);
            TrimToTop(report.LargestTextures, topCount);

            return report;
        }

        // ══════════════════════════════════════════════
        // Internal Helpers
        // ══════════════════════════════════════════════

        private static void AuditRenderers(Camera camera, SceneAuditReport report)
        {
            MaterialIds.Clear();
            SortingLayerIds.Clear();
            TexturesById.Clear();

            Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            report.ActiveRendererCount = renderers.Length;

            bool hasCamera = camera != null;
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            float screenArea = screenWidth * screenHeight;
            float coveredArea = 0f;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (!renderer.isVisible)
                    continue;

                report.VisibleRendererCount++;

                Material material = renderer.sharedMaterial;
                if (material != null)
                    MaterialIds.Add(material.GetInstanceID());

                SortingLayerIds.Add(renderer.sortingLayerID);

                CollectTexture(renderer);

                if (!hasCamera)
                    continue;

                if (!SceneMetricsSystem.TryGetScreenArea(camera, renderer.bounds, screenWidth, screenHeight, out float area))
                    continue;

                coveredArea += area;

                float coverage = screenArea > 0f ? area / screenArea : 0f;
                string detail = $"{coverage * PercentScale:F0}% of screen · layer {SortingLayer.IDToName(renderer.sortingLayerID)}";

                report.HeaviestRenderers.Add(new SceneAuditReport.Entry(GetPath(renderer.transform), detail, coverage));
            }

            report.UniqueMaterialCount = MaterialIds.Count;
            report.SortingLayerCount = SortingLayerIds.Count;
            report.ScreenCoverageRatio = screenArea > 0f ? coveredArea / screenArea : 0f;
        }

        private static void AuditColliders2D(SceneAuditReport report)
        {
            Rigidbody2D[] bodies = UnityEngine.Object.FindObjectsByType<Rigidbody2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            report.Rigidbody2DCount = bodies.Length;

            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i].bodyType == RigidbodyType2D.Dynamic)
                    report.DynamicBody2DCount++;
            }

            Collider2D[] colliders = UnityEngine.Object.FindObjectsByType<Collider2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            report.Collider2DCount = colliders.Length;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                int points = GetPathPointCount(collider);

                report.Collider2DVertexCount += points;

                if (points == 0)
                    continue;

                string detail = $"{points} points · {collider.GetType().Name}";
                report.HeaviestColliders.Add(new SceneAuditReport.Entry(GetPath(collider.transform), detail, points));
            }
        }

        private static void AuditCanvases(SceneAuditReport report)
        {
            GraphicsPerCanvas.Clear();
            RaycastTargetsPerCanvas.Clear();
            CanvasById.Clear();

            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            report.CanvasCount = canvases.Length;

            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                int id = canvas.GetInstanceID();

                CanvasById[id] = canvas;
                GraphicsPerCanvas[id] = 0;
                RaycastTargetsPerCanvas[id] = 0;
            }

            Graphic[] graphics = UnityEngine.Object.FindObjectsByType<Graphic>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            report.GraphicCount = graphics.Length;

            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];

                if (graphic.raycastTarget)
                    report.RaycastTargetCount++;

                Canvas canvas = graphic.canvas;
                if (canvas == null)
                    continue;

                int id = canvas.GetInstanceID();
                if (!GraphicsPerCanvas.ContainsKey(id))
                    continue;

                GraphicsPerCanvas[id]++;

                if (graphic.raycastTarget)
                    RaycastTargetsPerCanvas[id]++;

                CollectTexture(graphic.mainTexture);
            }

            foreach (KeyValuePair<int, int> pair in GraphicsPerCanvas)
            {
                if (pair.Value == 0)
                    continue;

                Canvas canvas = CanvasById[pair.Key];
                string detail = $"{pair.Value} graphics · {RaycastTargetsPerCanvas[pair.Key]} raycast targets";

                report.HeaviestCanvases.Add(new SceneAuditReport.Entry(GetPath(canvas.transform), detail, pair.Value));
            }
        }

        private static void AuditTextures(SceneAuditReport report)
        {
            report.UniqueTextureCount = TexturesById.Count;

            foreach (KeyValuePair<int, Texture> pair in TexturesById)
            {
                Texture texture = pair.Value;
                if (texture == null)
                    continue;

                long bytes = Profiler.GetRuntimeMemorySizeLong(texture);
                report.UniqueTextureBytes += bytes;

                string detail = $"{bytes / BytesPerMegabyte:F2} MB · {texture.width}x{texture.height}";
                report.LargestTextures.Add(new SceneAuditReport.Entry(texture.name, detail, bytes));
            }
        }

        private static void CollectTexture(Renderer renderer)
        {
            if (renderer is SpriteRenderer spriteRenderer)
            {
                Sprite sprite = spriteRenderer.sprite;
                if (sprite != null)
                    CollectTexture(sprite.texture);

                return;
            }

            Material material = renderer.sharedMaterial;
            if (material != null && material.HasProperty(MainTexPropertyId))
                CollectTexture(material.GetTexture(MainTexPropertyId));
        }

        private static void CollectTexture(Texture texture)
        {
            if (texture == null)
                return;

            TexturesById[texture.GetInstanceID()] = texture;
        }

        private static int GetPathPointCount(Collider2D collider)
        {
            if (collider is PolygonCollider2D polygon)
                return polygon.GetTotalPointCount();

            if (collider is EdgeCollider2D edge)
                return edge.pointCount;

            if (collider is CompositeCollider2D composite)
                return composite.pointCount;

            return 0;
        }

        private static void TrimToTop(List<SceneAuditReport.Entry> entries, int topCount)
        {
            entries.Sort(DescendingByValue);

            if (entries.Count > topCount)
                entries.RemoveRange(topCount, entries.Count - topCount);
        }

        private static string GetPath(Transform target)
        {
            Transform parent = target.parent;

            if (parent == null)
                return target.name;

            return $"{parent.name}/{target.name}";
        }
    }
}
