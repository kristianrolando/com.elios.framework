using System.Collections.Generic;

namespace Game.Framework.Profiling
{
    // Result of one on-demand scene scan. A plain data carrier: SceneAuditService fills it in,
    // the overlay and the editor window only read it.
    public sealed class SceneAuditReport
    {
        // One ranked row. Value is whatever the list is sorted by; Detail carries the unit and
        // any extra context so the reader does not have to guess what the number means.
        public readonly struct Entry
        {
            public readonly string Label;
            public readonly string Detail;
            public readonly float Value;

            public Entry(string label, string detail, float value)
            {
                Label = label;
                Detail = detail;
                Value = value;
            }
        }

        public string GeneratedAtLabel;

        public int ActiveRendererCount;
        public int VisibleRendererCount;
        public int UniqueMaterialCount;
        public int SortingLayerCount;
        public float ScreenCoverageRatio;

        public int Rigidbody2DCount;
        public int DynamicBody2DCount;
        public int Collider2DCount;
        public int Collider2DVertexCount;

        public int CanvasCount;
        public int GraphicCount;
        public int RaycastTargetCount;

        public int UniqueTextureCount;
        public long UniqueTextureBytes;

        public readonly List<Entry> HeaviestRenderers = new List<Entry>();
        public readonly List<Entry> HeaviestColliders = new List<Entry>();
        public readonly List<Entry> HeaviestCanvases = new List<Entry>();
        public readonly List<Entry> LargestTextures = new List<Entry>();

        public void Clear()
        {
            HeaviestRenderers.Clear();
            HeaviestColliders.Clear();
            HeaviestCanvases.Clear();
            LargestTextures.Clear();
        }
    }
}
