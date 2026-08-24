using System;
using UnityEngine;

namespace Game.Framework.Profiling
{
    public enum BudgetLevel
    {
        Normal = 0,
        Warning = 1,
        Critical = 2
    }

    // Warning and critical thresholds for a single metric. A threshold left at 0 disables that
    // level, so a half-filled budget never paints the whole overlay red.
    [Serializable]
    public struct MetricBudget
    {
        [SerializeField, Tooltip("Reaching this value turns the metric yellow. 0 disables this level.")]
        private float _warning;

        [SerializeField, Tooltip("Reaching this value turns the metric red. 0 disables this level.")]
        private float _critical;

        public float Warning => _warning;
        public float Critical => _critical;

        public MetricBudget(float warning, float critical)
        {
            _warning = warning;
            _critical = critical;
        }

        // For metrics where a bigger number is worse: frame time, draw calls, GC alloc.
        public BudgetLevel Evaluate(float value)
        {
            if (_critical > 0f && value >= _critical)
                return BudgetLevel.Critical;

            if (_warning > 0f && value >= _warning)
                return BudgetLevel.Warning;

            return BudgetLevel.Normal;
        }

        // For metrics where a smaller number is worse: FPS.
        public BudgetLevel EvaluateInverted(float value)
        {
            if (_critical > 0f && value <= _critical)
                return BudgetLevel.Critical;

            if (_warning > 0f && value <= _warning)
                return BudgetLevel.Warning;

            return BudgetLevel.Normal;
        }
    }
}
