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

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            float ran_pos = UnityEngine.Random.Range(0f, 1f);

            if (ran_pos < possibility)
                return true;
            return false;
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target)
        {
            return IsTriggerConditionMet(data, ability, caster);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target)
        {
            return IsTriggerConditionMet(data, ability, caster);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target)
        {
            return IsTriggerConditionMet(data, ability, caster);
        }
    }
}