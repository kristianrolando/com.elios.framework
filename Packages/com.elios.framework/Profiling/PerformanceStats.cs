namespace Game.Framework.Profiling
{
    // Aggregated view over the sampler's rolling window. Recomputed on the refresh interval
    // rather than every frame, so the numbers stay readable and the sort stays cheap.
    //
    // OnePercentLowFps is the average of the worst 1% of frames, not the single worst one. It is
    // the number that correlates with "the game feels stuttery" far better than average FPS.
    public readonly struct PerformanceStats
    {
        public readonly float AverageFps;
        public readonly float MinFps;
        public readonly float MaxFps;
        public readonly float OnePercentLowFps;
        public readonly float AverageFrameTimeMs;
        public readonly float WorstFrameTimeMs;
        public readonly int SampleCount;

        public PerformanceStats(
            float averageFrameTimeMs,
            float bestFrameTimeMs,
            float worstFrameTimeMs,
            float onePercentLowFrameTimeMs,
            int sampleCount)
        {
            AverageFrameTimeMs = averageFrameTimeMs;
            WorstFrameTimeMs = worstFrameTimeMs;
            SampleCount = sampleCount;

            AverageFps = ToFps(averageFrameTimeMs);
            MinFps = ToFps(worstFrameTimeMs);
            MaxFps = ToFps(bestFrameTimeMs);
            OnePercentLowFps = ToFps(onePercentLowFrameTimeMs);
        }

        private static float ToFps(float frameTimeMs)
        {
            return frameTimeMs > 0f ? PerformanceSample.MillisecondsPerSecond / frameTimeMs : 0f;
        }
    }
}
