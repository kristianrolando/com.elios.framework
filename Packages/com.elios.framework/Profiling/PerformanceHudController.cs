using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Framework.Profiling
{
    // Drives the in-game profiling HUD: owns the sampler and the scene scanner, handles the
    // toggle key, and pushes the result to the view on a fixed refresh interval so the numbers
    // stay readable instead of flickering every frame.
    //
    // Drop it on one GameObject in the scene. The render, memory and UI counters it reports only
    // exist in the Editor and in Development Builds; in a release build the frame numbers still
    // work and the rest read "n/a".
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PerformanceOverlayView))]
    public class PerformanceHudController : MonoBehaviour
    {
        private const string ToggleHintFormat = "[{0}]";
        private const string NoToggleHint = "no toggle key";

        [Header("Budget")]

        [SerializeField, Tooltip("Thresholds that colour the readout. Leave empty to show every metric in the neutral colour.")]
        private PerformanceBudgetSO _budget;

        [Header("Display")]

        [SerializeField, Tooltip("Key that shows and hides the overlay. Set to None to disable the shortcut.")]
        private Key _toggleKey = Key.F3;

        [SerializeField, Tooltip("Show the overlay as soon as the scene starts.")]
        private bool _visibleOnStart = true;

        [SerializeField, Min(0.05f), Tooltip("Seconds between readout refreshes. Sampling still happens every frame.")]
        private float _refreshInterval = 0.25f;

        [Header("Sampling")]

        [SerializeField, Min(30), Tooltip("Number of frames kept for average, worst and 1% low. 300 frames is about 5 seconds at 60 FPS.")]
        private int _sampleWindow = 300;

        [SerializeField, Tooltip("Scan the scene periodically for the overdraw estimate and the Physics 2D counts. The scan allocates, so its own GC cost is excluded from the reading.")]
        private bool _enableSceneScan = true;

        [SerializeField, Min(0.25f), Tooltip("Seconds between scene scans. Lower values react faster but walk the whole scene more often.")]
        private float _sceneScanInterval = 1f;

        [Header("Debug")]

        [SerializeField, Tooltip("Log setup problems, such as a missing budget asset or camera.")]
        private bool _enableLogging;

        private PerformanceOverlayView _view;
        private PerformanceSamplerSystem _sampler;
        private SceneMetricsSystem _sceneMetrics;
        private Camera _camera;

        private float _refreshTimer;
        private float _scanTimer;
        private bool _isVisible;

        public bool IsVisible => _isVisible;
        public PerformanceBudgetSO Budget => _budget;
        public PerformanceSamplerSystem Sampler => _sampler;
        public SceneMetricsSystem SceneMetrics => _sceneMetrics;
        public SceneAuditReport LastAudit { get; private set; }

        // ══════════════════════════════════════════════
        // Unity Lifecycle
        // ══════════════════════════════════════════════

        private void Awake()
        {
            _view = GetComponent<PerformanceOverlayView>();
            _sampler = new PerformanceSamplerSystem(_sampleWindow);
            _sceneMetrics = new SceneMetricsSystem();
        }

        private void Start()
        {
            _camera = Camera.main;

            if (_camera == null && _enableLogging)
                EditorDebug.Warning("[PerformanceHud] No camera tagged MainCamera. The overdraw estimate stays at zero.", this);

            if (_budget == null && _enableLogging)
                EditorDebug.Warning("[PerformanceHud] No PerformanceBudget assigned. Every metric will render in the neutral colour.", this);

            _view.SetToggleHint(BuildToggleHint());
            SetVisible(_visibleOnStart);
        }

        private void Update()
        {
            if (WasTogglePressed())
                SetVisible(!_isVisible);

            _sampler.Sample(Time.unscaledDeltaTime);

            TickSceneScan();
            TickRefresh();
        }

        private void OnDestroy()
        {
            if (_sampler != null)
                _sampler.Dispose();
        }

        // ══════════════════════════════════════════════
        // Public API
        // ══════════════════════════════════════════════

        public void SetVisible(bool isVisible)
        {
            _isVisible = isVisible;
            _view.SetVisible(isVisible);

            if (isVisible)
                Refresh();
        }

        public void ResetStats()
        {
            _sampler.Reset();
        }

        [ContextMenu("Run Scene Audit")]
        public void RunSceneAudit()
        {
            LastAudit = SceneAuditService.Audit(ResolveCamera());
            _sampler.SuppressNextGcAllocSample();
        }

        // ══════════════════════════════════════════════
        // Internal Helpers
        // ══════════════════════════════════════════════

        private void TickSceneScan()
        {
            if (!_enableSceneScan)
                return;

            _scanTimer -= Time.unscaledDeltaTime;

            if (_scanTimer > 0f)
                return;

            _scanTimer = _sceneScanInterval;

            _sceneMetrics.Scan(ResolveCamera());
            _sampler.SuppressNextGcAllocSample();
        }

        private void TickRefresh()
        {
            if (!_isVisible)
                return;

            _refreshTimer -= Time.unscaledDeltaTime;

            if (_refreshTimer > 0f)
                return;

            _refreshTimer = _refreshInterval;
            Refresh();
        }

        private void Refresh()
        {
            _sampler.RecomputeStats();
            _view.Present(_sampler, _sceneMetrics, _budget);
        }

        private Camera ResolveCamera()
        {
            if (_camera == null)
                _camera = Camera.main;

            return _camera;
        }

        private bool WasTogglePressed()
        {
            if (_toggleKey == Key.None)
                return false;

            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
                return false;

            return keyboard[_toggleKey].wasPressedThisFrame;
        }

        private string BuildToggleHint()
        {
            return _toggleKey == Key.None ? NoToggleHint : string.Format(ToggleHintFormat, _toggleKey);
        }
    }
}
