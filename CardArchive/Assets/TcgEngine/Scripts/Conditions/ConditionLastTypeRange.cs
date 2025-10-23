using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.AI;

namespace TcgEngine
{
    /// <summary>
    /// 마지막 유형 타일 / 카드에서 일정 범위 안에 있는지 확인하는 조건 (0으로 설정시 해당 타입인지 검사)
    /// 카드의 경우 last_played는 거리 조건을 따지지 않음 (play는 이벤트 카드도 가능하기 때문)
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/LastTypeRange", order = 10)]
    public class ConditionLastTypeRange : ConditionData
    {
        [Header("Last Type")]
        public LastType type;
        public int range;

        public ConditionOperatorBool oper;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target)
        {
            if (type == LastType.LastPlayed)
                return CompareBool(data.last_played == target.uid, oper);


            return IsTargetConditionMet(data, ability, caster, target.slot);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target)
        {
            bool result = false;
            
            if (type == LastType.LastPlayed)
                return false;

            if (type == LastType.LastAttacked)
            {
                if (!data.last_player_attacked && data.last_attacked_slot.GetNeighborSlot(range).Contains(target))
                    result = true;
            }

            if (type == LastType.LastTargeted && data.last_targeted_slot.GetNeighborSlot(range).Contains(target))
                result = true;

            if (type == LastType.LastSummoned && data.last_summoned_slot.GetNeighborSlot(range).Contains(target))
                result = true;

            if (type == LastType.LastDestroyed && data.last_destroyed_slot.GetNeighborSlot(range).Contains(target))
                result = true;

            return CompareBool(result, oper);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target)
        {
            return false;
        }
    }
}