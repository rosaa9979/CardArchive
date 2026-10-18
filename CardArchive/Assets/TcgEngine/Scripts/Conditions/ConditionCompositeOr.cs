using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Composite condition: returns true if ANY of the sub-conditions is met.
    /// The engine AND-combines conditions inside an array; this provides OR.
    /// Empty list is treated as "always true" (neutral element), like a missing condition.
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/CompositeOr", order = 10)]
    public class ConditionCompositeOr : ConditionData
    {
        [Header("True if ANY of these is met")]
        public ConditionData[] any;

        public override bool Evaluate(Game data, AbilityData ability, Card caster, object target,
            ConditionStage stage, AbilityEventContext context, object selected = null)
        {
            if (any == null || any.Length == 0) return true;
            foreach (ConditionData child in any)
                if (child != null && ConditionEvaluation.Matches(child, data, ability, caster, target, stage, context, selected))
                    return true;
            return false;
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target, AbilityEventContext context = null)
        {
            return Evaluate(data, ability, caster, target, ConditionStage.Target, context);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target, AbilityEventContext context = null)
        {
            return Evaluate(data, ability, caster, target, ConditionStage.Target, context);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target, AbilityEventContext context = null)
        {
            return Evaluate(data, ability, caster, target, ConditionStage.Target, context);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, CardData target, AbilityEventContext context = null)
        {
            return Evaluate(data, ability, caster, target, ConditionStage.Target, context);
        }

    }
}
