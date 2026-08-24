using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace Game.Framework.Profiling
{
    // Owns the ProfilerRecorder handles and the rolling frame time window. Object-agnostic: it
    // knows nothing about the scene, so the HUD and the editor window can both drive it.
    //
    // Counter names differ between Unity versions and are simply absent outside the Editor and
    // Development Builds. Every recorder is therefore resolved from a list of candidate names and
    // degrades to PerformanceSample.Unavailable instead of throwing. ResolvedCounters and
    // MissingCounters expose the outcome so a blank number is never a mystery.
    public sealed class PerformanceSamplerSystem : IDisposable
    {
        public const float UnavailableTime = -1f;

        private const int RecorderCapacity = 1;
        private const float NanosecondsToMilliseconds = 1e-6f;
        private const float OnePercentFraction = 0.01f;
        private const int MinWindowSize = 30;
        private const int MaxWindowSize = 4096;

        private readonly float[] _frameTimesMs;
        private readonly float[] _sortScratch;
        private readonly List<string> _resolvedCounters = new List<string>();
        private readonly List<string> _missingCounters = new List<string>();

        private ProfilerRecorder _mainThread;
        private ProfilerRecorder _drawCalls;
        private ProfilerRecorder _batches;
        private ProfilerRecorder _setPassCalls;
        private ProfilerRecorder _triangles;
        private ProfilerRecorder _vertices;
        private ProfilerRecorder _gcAlloc;
        private ProfilerRecorder _gcUsed;
        private ProfilerRecorder _totalUsed;
        private ProfilerRecorder _gfxUsed;
        private ProfilerRecorder _textureMemory;
        private ProfilerRecorder _canvasBatchRebuild;
        private ProfilerRecorder _canvasLayoutRebuild;

        private int _writeIndex;
        private int _count;
        private bool _suppressNextGcAlloc;
        private long _lastStableGcAllocBytes = PerformanceSample.Unavailable;
        private bool _isDisposed;

        public PerformanceSamplerSystem(int windowSize)
        {
            int clampedWindow = Mathf.Clamp(windowSize, MinWindowSize, MaxWindowSize);

            _frameTimesMs = new float[clampedWindow];
            _sortScratch = new float[clampedWindow];

            StartRecorders();
        }

        public PerformanceSample Latest { get; private set; }
        public PerformanceStats Stats { get; private set; }

        public int WindowSize => _frameTimesMs.Length;
        public int HistoryCount => _count;

        public IReadOnlyList<string> ResolvedCounters => _resolvedCounters;
        public IReadOnlyList<string> MissingCounters => _missingCounters;

        // ══════════════════════════════════════════════
        // Public API
        // ══════════════════════════════════════════════

        public void Sample(float unscaledDeltaTime)
        {
            if (_isDisposed)
                return;

            PerformanceSample sample = default;
            sample.UnscaledDeltaTime = unscaledDeltaTime;

            long mainThreadNs = ReadCounter(_mainThread);
            sample.MainThreadMs = PerformanceSample.HasValue(mainThreadNs)
                ? mainThreadNs * NanosecondsToMilliseconds
                : UnavailableTime;

            sample.DrawCalls = ReadCounter(_drawCalls);
            sample.Batches = ReadCounter(_batches);
            sample.SetPassCalls = ReadCounter(_setPassCalls);
            sample.Triangles = ReadCounter(_triangles);
            sample.Vertices = ReadCounter(_vertices);

            sample.GcAllocBytes = ReadGcAlloc();
            sample.GcUsedBytes = ReadCounter(_gcUsed);
            sample.TotalUsedBytes = ReadCounter(_totalUsed);
            sample.GfxUsedBytes = ReadCounter(_gfxUsed);
            sample.TextureBytes = ReadCounter(_textureMemory);

            sample.CanvasBatchRebuilds = ReadMarkerHits(_canvasBatchRebuild);
            sample.CanvasLayoutRebuilds = ReadMarkerHits(_canvasLayoutRebuild);

            Latest = sample;

            PushFrameTime(sample.FrameTimeMs);
        }

        // The overdraw scan walks the whole scene and allocates, which would show up as a fake GC
        // spike on the next frame. The scanner calls this so the HUD keeps reporting the last
        // value the game itself produced.
        public void SuppressNextGcAllocSample()
        {
            _suppressNextGcAlloc = true;
        }

        public void RecomputeStats()
        {
            if (_count == 0)
            {
                Stats = default;
                return;
            }

            Array.Copy(_frameTimesMs, _sortScratch, _count);
            Array.Sort(_sortScratch, 0, _count);

            float total = 0f;
            for (int i = 0; i < _count; i++)
                total += _sortScratch[i];

            int lowSampleCount = Mathf.Max(1, Mathf.RoundToInt(_count * OnePercentFraction));
            float lowTotal = 0f;
            for (int i = _count - lowSampleCount; i < _count; i++)
                lowTotal += _sortScratch[i];

            Stats = new PerformanceStats(
                total / _count,
                _sortScratch[0],
                _sortScratch[_count - 1],
                lowTotal / lowSampleCount,
                _count);
        }

        // Index 0 is the oldest frame still in the window. Used by the frame time graph.
        public float GetHistoryFrameTimeMs(int index)
        {
            if (index < 0 || index >= _count)
                return 0f;

            int capacity = _frameTimesMs.Length;
            int oldest = _count == capacity ? _writeIndex : 0;

            return _frameTimesMs[(oldest + index) % capacity];
        }

        public void Reset()
        {
            _writeIndex = 0;
            _count = 0;
            Stats = default;
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            DisposeRecorder(ref _mainThread);
            DisposeRecorder(ref _drawCalls);
            DisposeRecorder(ref _batches);
            DisposeRecorder(ref _setPassCalls);
            DisposeRecorder(ref _triangles);
            DisposeRecorder(ref _vertices);
            DisposeRecorder(ref _gcAlloc);
            DisposeRecorder(ref _gcUsed);
            DisposeRecorder(ref _totalUsed);
            DisposeRecorder(ref _gfxUsed);
            DisposeRecorder(ref _textureMemory);
            DisposeRecorder(ref _canvasBatchRebuild);
            DisposeRecorder(ref _canvasLayoutRebuild);
        }

        // ══════════════════════════════════════════════
        // Internal Helpers
        // ══════════════════════════════════════════════

        private void StartRecorders()
        {
            _mainThread = StartRecorder(ProfilerCategory.Internal, "CPU Main Thread", "Main Thread");

            _drawCalls = StartRecorder(ProfilerCategory.Render, "Draw Calls", "Draw Calls Count");
            _batches = StartRecorder(ProfilerCategory.Render, "Batches", "Batches Count");
            _setPassCalls = StartRecorder(ProfilerCategory.Render, "SetPass Calls", "SetPass Calls Count");
            _triangles = StartRecorder(ProfilerCategory.Render, "Triangles", "Triangles Count");
            _vertices = StartRecorder(ProfilerCategory.Render, "Vertices", "Vertices Count");

            _gcAlloc = StartRecorder(ProfilerCategory.Memory, "GC Alloc / Frame", "GC Allocated In Frame");
            _gcUsed = StartRecorder(ProfilerCategory.Memory, "GC Used Memory", "GC Used Memory");
            _totalUsed = StartRecorder(ProfilerCategory.Memory, "Total Used Memory", "Total Used Memory", "System Used Memory");
            _gfxUsed = StartRecorder(ProfilerCategory.Memory, "Gfx Used Memory", "Gfx Used Memory");
            _textureMemory = StartRecorder(ProfilerCategory.Memory, "Texture Memory", "Texture Memory");

            _canvasBatchRebuild = StartRecorder(
                ProfilerCategory.Gui,
                "Canvas Batch Rebuild",
                "UGUI.Rendering.UpdateBatches",
                "Canvas.BuildBatch");

            _canvasLayoutRebuild = StartRecorder(
                ProfilerCategory.Gui,
                "Canvas Layout Rebuild",
                "Canvas.SendWillRenderCanvases",
                "UGUI.Rendering.RenderOverlays");
        }

        private ProfilerRecorder StartRecorder(ProfilerCategory category, string label, params string[] candidateNames)
        {
            for (int i = 0; i < candidateNames.Length; i++)
            {
                ProfilerRecorder recorder = ProfilerRecorder.StartNew(category, candidateNames[i], RecorderCapacity);

                if (recorder.Valid)
                {
                    _resolvedCounters.Add($"{label}  ({candidateNames[i]})");
                    return recorder;
                }

                recorder.Dispose();
            }

            _missingCounters.Add(label);
            return default;
        }

        private static long ReadCounter(ProfilerRecorder recorder)
        {
            return recorder.Valid ? recorder.LastValue : PerformanceSample.Unavailable;
        }

        // Marker recorders sum every sample of the frame into one entry, so the entry's Count is
        // how many times the marker fired that frame. That is the rebuild count.
        private static long ReadMarkerHits(ProfilerRecorder recorder)
        {
            if (!recorder.Valid)
                return PerformanceSample.Unavailable;

            if (recorder.Count == 0)
                return 0L;

            return recorder.GetSample(recorder.Count - 1).Count;
        }

        private long ReadGcAlloc()
        {
            long allocated = ReadCounter(_gcAlloc);

            if (_suppressNextGcAlloc)
            {
                _suppressNextGcAlloc = false;
                return _lastStableGcAllocBytes;
            }

            _lastStableGcAllocBytes = allocated;
            return allocated;
        }

        private void PushFrameTime(float frameTimeMs)
        {
            _frameTimesMs[_writeIndex] = frameTimeMs;
            _writeIndex = (_writeIndex + 1) % _frameTimesMs.Length;

            if (_count < _frameTimesMs.Length)
                _count++;
        }

        private static void DisposeRecorder(ref ProfilerRecorder recorder)
        {
            if (recorder.Valid)
                recorder.Dispose();

            recorder = default;
        }
    }
}
