using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// 카드가 배치될 수 있는지 확인
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/CanPlace", order = 11)]
    public class ConditionCanPlace : ConditionData
    {
        public ConditionOperatorBool oper;
        
        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target)
        { 
            return CompareBool(data.CanPlaceCard(caster, target.slot), oper);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target)
        {
            return CompareBool(data.CanPlaceCard(caster, target), oper);
        }
    }
}