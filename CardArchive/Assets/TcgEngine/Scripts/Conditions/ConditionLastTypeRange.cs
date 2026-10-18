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
        public ConditionLastType type;
        public int range;

        public ConditionOperatorBool oper;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target, AbilityEventContext context = null)
        {
            if (type == ConditionLastType.LastPlayed)
                return CompareBool(((context as AbilityExecutionContext)?.last_played ?? data.last_played) == target.uid, oper);

            //Other types check the card's slot against the last slot (range 0 = that card's own slot)
            return IsTargetConditionMet(data, ability, caster, target.slot, context: context);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target, AbilityEventContext context = null)
        {
            bool result = false;
            
            if (type == ConditionLastType.LastPlayed)
                return false;

            if (type == ConditionLastType.LastAttacked)
            {
                if (!((context as AbilityExecutionContext)?.last_player_attacked ?? data.last_player_attacked) && ((context as AbilityExecutionContext)?.last_attacked_slot ?? data.last_attacked_slot).GetNeighborSlot(range).Contains(target))
                    result = true;
            }

            if (type == ConditionLastType.LastTargeted && ((context as AbilityExecutionContext)?.reference_target_slot ?? data.last_targeted_slot).GetNeighborSlot(range).Contains(target))
                result = true;

            if (type == ConditionLastType.LastSummoned)
            {
                Slot summoned_slot = (context as AbilityExecutionContext)?.last_summoned_slot ?? data.last_summoned_slot;
                if (summoned_slot.GetNeighborSlot(range).Contains(target))
                    result = true;
            }


            if (type == ConditionLastType.LastDestroyed && ((context as AbilityExecutionContext)?.last_destroyed_slot ?? data.last_destroyed_slot).GetNeighborSlot(range).Contains(target))
                result = true;

            return CompareBool(result, oper);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target, AbilityEventContext context = null)
        {
            return false;
        }
    }
}