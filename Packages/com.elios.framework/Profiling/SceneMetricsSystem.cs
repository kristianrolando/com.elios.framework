using System.Collections.Generic;
using UnityEngine;

namespace Game.Framework.Profiling
{
    // Samples the things Unity exposes no profiler counter for: how much of the screen the 2D
    // renderers actually cover, and how heavy the Physics2D setup in the scene is.
    //
    // ScreenCoverageRatio is an overdraw proxy, not real overdraw. It sums the on-screen area of
    // every visible renderer's bounds and divides by the screen area, so 1.0 means the visible
    // sprites would cover the screen exactly once if they never overlapped. In a 2D game where
    // every sprite is a transparent quad, that number tracks the real fill cost closely enough to
    // spot a background stack or a full-screen effect that should not be there.
    //
    // The scan walks the whole scene and allocates, so it runs on an interval, never per frame.
    public sealed class SceneMetricsSystem
    {
        private readonly HashSet<int> _materialIds = new HashSet<int>();
        private readonly HashSet<int> _sortingLayerIds = new HashSet<int>();

        public int ActiveRendererCount { get; private set; }
        public int VisibleRendererCount { get; private set; }
        public int VisibleSpriteCount { get; private set; }
        public float ScreenCoverageRatio { get; private set; }
        public int UniqueMaterialCount { get; private set; }
        public int SortingLayerCount { get; private set; }

        public int Rigidbody2DCount { get; private set; }
        public int DynamicBody2DCount { get; private set; }
        public int Collider2DCount { get; private set; }
        public int Collider2DVertexCount { get; private set; }

        public bool HasScanned { get; private set; }

        // ══════════════════════════════════════════════
        // Public API
        // ══════════════════════════════════════════════

        public void Scan(Camera camera)
        {
            ScanRenderers(camera);
            ScanPhysics2D();

            HasScanned = true;
        }

        // ══════════════════════════════════════════════
        // Internal Helpers
        // ══════════════════════════════════════════════

        private void ScanRenderers(Camera camera)
        {
            _materialIds.Clear();
            _sortingLayerIds.Clear();

            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            ActiveRendererCount = renderers.Length;
            VisibleRendererCount = 0;
            VisibleSpriteCount = 0;
            ScreenCoverageRatio = 0f;

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

                VisibleRendererCount++;

                if (renderer is SpriteRenderer)
                    VisibleSpriteCount++;

                Material material = renderer.sharedMaterial;
                if (material != null)
                    _materialIds.Add(material.GetInstanceID());

                _sortingLayerIds.Add(renderer.sortingLayerID);

                if (hasCamera && TryGetScreenArea(camera, renderer.bounds, screenWidth, screenHeight, out float area))
                    coveredArea += area;
            }

            UniqueMaterialCount = _materialIds.Count;
            SortingLayerCount = _sortingLayerIds.Count;
            ScreenCoverageRatio = screenArea > 0f ? coveredArea / screenArea : 0f;
        }

        private void ScanPhysics2D()
        {
            Rigidbody2D[] bodies = Object.FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            Rigidbody2DCount = bodies.Length;
            DynamicBody2DCount = 0;

            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i].bodyType == RigidbodyType2D.Dynamic)
                    DynamicBody2DCount++;
            }

            Collider2D[] colliders = Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            Collider2DCount = colliders.Length;
            Collider2DVertexCount = 0;

            for (int i = 0; i < colliders.Length; i++)
                Collider2DVertexCount += GetPathPointCount(colliders[i]);
        }

        // Only path-based colliders are counted. Box, circle and capsule colliders have a fixed,
        // negligible cost, so folding an invented weight for them into the total would only make
        // the number harder to read.
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

        // Shared with SceneAuditService so both report coverage the same way.
        internal static bool TryGetScreenArea(Camera camera, Bounds bounds, float screenWidth, float screenHeight, out float area)
        {
            area = 0f;

            Vector3 min = camera.WorldToScreenPoint(bounds.min);
            Vector3 max = camera.WorldToScreenPoint(bounds.max);

            // Behind a perspective camera the projection flips; skip rather than report garbage.
            if (min.z < 0f || max.z < 0f)
                return false;

            float left = Mathf.Max(Mathf.Min(min.x, max.x), 0f);
            float right = Mathf.Min(Mathf.Max(min.x, max.x), screenWidth);
            float bottom = Mathf.Max(Mathf.Min(min.y, max.y), 0f);
            float top = Mathf.Min(Mathf.Max(min.y, max.y), screenHeight);

            float width = right - left;
            float height = top - bottom;

            if (width <= 0f || height <= 0f)
                return false;

            area = width * height;
            return true;
        }
    }
}
