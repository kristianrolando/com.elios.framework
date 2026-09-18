using UnityEngine;

namespace Elios.Framework.Profiling
{
    // Per-metric thresholds that turn the overlay numbers yellow and red. Defaults are tuned for
    // a 2D URP game at 1080p/60. Duplicate the asset and lower the thresholds to profile against
    // a weaker target device.
    [CreateAssetMenu(fileName = "PerformanceBudget", menuName = "Profiling/Performance Budget")]
    public class PerformanceBudgetSO : ScriptableObject
    {
        [Header("Frame")]

        [SerializeField, Min(1), Tooltip("Frame rate this build is expected to hold. Drives the frame time graph scale.")]
        private int _targetFps = 60;

        [SerializeField, Tooltip("FPS thresholds. Lower is worse, so warning should be above critical.")]
        private MetricBudget _fps;

        [SerializeField, Tooltip("CPU frame time in milliseconds.")]
        private MetricBudget _frameTimeMs;

        [Header("Rendering")]

        [SerializeField, Tooltip("Draw calls issued per frame.")]
        private MetricBudget _drawCalls;

        [SerializeField, Tooltip("Batches per frame. A batch count close to the draw call count means batching is not working.")]
        private MetricBudget _batches;

        [SerializeField, Tooltip("SetPass calls per frame. Each one is a material/shader state switch and is the most expensive of the render counters.")]
        private MetricBudget _setPassCalls;

        [SerializeField, Tooltip("Triangles submitted per frame.")]
        private MetricBudget _triangles;

        [SerializeField, Tooltip("Estimated 2D overdraw: total on-screen sprite area divided by screen area. 1.0 means the sprites exactly cover the screen once.")]
        private MetricBudget _overdrawRatio;

        [Header("Memory")]

        [SerializeField, Tooltip("Managed allocations per frame, in kilobytes. Anything above zero in steady state eventually causes a GC spike.")]
        private MetricBudget _gcAllocKb;

        [SerializeField, Tooltip("Total memory used by the player, in megabytes.")]
        private MetricBudget _totalMemoryMb;

        [Header("UI and Physics")]

        [SerializeField, Tooltip("Canvas batch rebuilds per frame. A canvas rebuilds entirely whenever any graphic on it changes.")]
        private MetricBudget _canvasRebuilds;

        [SerializeField, Tooltip("Total points across every PolygonCollider2D, EdgeCollider2D and CompositeCollider2D in the scene.")]
        private MetricBudget _collider2DVertices;

        public int TargetFps => _targetFps;
        public MetricBudget Fps => _fps;
        public MetricBudget FrameTimeMs => _frameTimeMs;
        public MetricBudget DrawCalls => _drawCalls;
        public MetricBudget Batches => _batches;
        public MetricBudget SetPassCalls => _setPassCalls;
        public MetricBudget Triangles => _triangles;
        public MetricBudget OverdrawRatio => _overdrawRatio;
        public MetricBudget GcAllocKb => _gcAllocKb;
        public MetricBudget TotalMemoryMb => _totalMemoryMb;
        public MetricBudget CanvasRebuilds => _canvasRebuilds;
        public MetricBudget Collider2DVertices => _collider2DVertices;

        // ══════════════════════════════════════════════
        // Validation
        // ══════════════════════════════════════════════

        private void Reset()
        {
            ApplyDefaults();
        }

        [ContextMenu("Reset To Defaults")]
        private void ApplyDefaults()
        {
            _targetFps = 60;

            _fps = new MetricBudget(55f, 30f);
            _frameTimeMs = new MetricBudget(16.7f, 33.3f);

            _drawCalls = new MetricBudget(150f, 300f);
            _batches = new MetricBudget(200f, 400f);
            _setPassCalls = new MetricBudget(40f, 80f);
            _triangles = new MetricBudget(100000f, 300000f);
            _overdrawRatio = new MetricBudget(2.5f, 4f);

            _gcAllocKb = new MetricBudget(2f, 16f);
            _totalMemoryMb = new MetricBudget(700f, 1200f);

            _canvasRebuilds = new MetricBudget(2f, 6f);
            _collider2DVertices = new MetricBudget(3000f, 8000f);
        }
    }
}
