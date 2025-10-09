using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// 카드가 배치될 수 있는지 확인
    /// 배치할 카드가 특정되어 있는 경우에만 사용 (단순 배치 가능 여부는 Condition의 조합으로 사용)
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
        
        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card selected, Slot target)
        {
            return CompareBool(data.CanPlaceCard(selected, target), oper);
        }
    }
}