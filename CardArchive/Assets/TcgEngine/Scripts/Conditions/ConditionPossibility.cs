using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Condition that check if the target is the same as the caster
    /// </summary>
    
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Possibility", order = 10)]
    public class ConditionPossibility : ConditionData
    {
        [Header("Possibility (0 <= possibility <= 1)")]
        public float possibility;

        // The probability is rolled ONLY in IsTargetConditionMet, never in IsTriggerConditionMet.
        // Rationale: a trigger condition is evaluated by AreTriggerConditionsMet, which calls BOTH
        // IsTriggerConditionMet and IsTargetConditionMet for the same condition. If both rolled,
        // the effective chance would be possibility^2 (e.g. 0.5 -> 0.25). IsTargetConditionMet is the
        // method called in EVERY slot (trigger, criteria/target, wide-range), so keeping the single
        // roll here yields a consistent, correct probability everywhere; IsTriggerConditionMet is a
        // no-op (true).
        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            return true;
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target)
        {
            return Roll();
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target)
        {
            return Roll();
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target)
        {
            return Roll();
        }

        private bool Roll()
        {
            return UnityEngine.Random.Range(0f, 1f) < possibility;
        }
    }
}