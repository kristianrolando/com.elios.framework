namespace Game.Framework.Profiling
{
    // One frame of raw counters. A counter left at Unavailable could not be resolved on this
    // build: most render and memory counters only exist in the Editor and in Development Builds,
    // so a release build still reports FPS and frame time but blanks the rest.
    public struct PerformanceSample
    {
        public const long Unavailable = -1L;
        public const float MillisecondsPerSecond = 1000f;

        public float UnscaledDeltaTime;
        public float MainThreadMs;

        public long DrawCalls;
        public long Batches;
        public long SetPassCalls;
        public long Triangles;
        public long Vertices;

        public long GcAllocBytes;
        public long GcUsedBytes;
        public long TotalUsedBytes;
        public long GfxUsedBytes;
        public long TextureBytes;

        public long CanvasBatchRebuilds;
        public long CanvasLayoutRebuilds;

        public float Fps => UnscaledDeltaTime > 0f ? 1f / UnscaledDeltaTime : 0f;
        public float FrameTimeMs => UnscaledDeltaTime * MillisecondsPerSecond;

        public static bool HasValue(long counter) => counter != Unavailable;
    }
}
