using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/None", order = 11)]
    public class ConditionNone : ConditionData
    {
        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target)
        {   
            return true;
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot selected, Slot target)
        {
            return true;
        }
    }
}