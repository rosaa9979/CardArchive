using UnityEngine;

namespace TcgEngine
{
    /// <summary>Probability predicate; invocation lifetime is managed by ConditionEvaluation.</summary>
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Possibility", order = 10)]
    public class ConditionPossibility : ConditionData
    {
        [Header("Possibility (0 <= possibility <= 1)")]
        public float possibility;

        // Probability is independent of target shape. The common evaluator calls this once
        // and owns EvaluateOnce caching across registration, execution and selection resume.
        public override bool Evaluate(Game data, AbilityData ability, Card caster, object target,
            ConditionStage stage, AbilityEventContext context, object selected = null)
        {
            if (possibility <= 0f) return false;
            if (possibility >= 1f) return true;
            return !(context is AbilityExecutionContext execution) || execution.is_query
                || data.GetRandom().NextDouble() < possibility;
        }
    }
}
