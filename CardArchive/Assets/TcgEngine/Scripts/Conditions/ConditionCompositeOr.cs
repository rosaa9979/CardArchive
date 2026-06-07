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

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            if (any == null || any.Length == 0) return true;
            foreach (ConditionData c in any)
                if (c != null && c.IsTriggerConditionMet(data, ability, caster)) return true;
            return false;
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target)
        {
            if (any == null || any.Length == 0) return true;
            foreach (ConditionData c in any)
                if (c != null && c.IsTargetConditionMet(data, ability, caster, target)) return true;
            return false;
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target)
        {
            if (any == null || any.Length == 0) return true;
            foreach (ConditionData c in any)
                if (c != null && c.IsTargetConditionMet(data, ability, caster, target)) return true;
            return false;
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target)
        {
            if (any == null || any.Length == 0) return true;
            foreach (ConditionData c in any)
                if (c != null && c.IsTargetConditionMet(data, ability, caster, target)) return true;
            return false;
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, CardData target)
        {
            if (any == null || any.Length == 0) return true;
            foreach (ConditionData c in any)
                if (c != null && c.IsTargetConditionMet(data, ability, caster, target)) return true;
            return false;
        }
    }
}
