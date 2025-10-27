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

    public enum ConditionLastType
    {
        None = 0,
        LastAttacked = 1,
        LastTargeted = 2,
        LastSummoned = 3,
        LastDestroyed = 4,
        LastPlayed = 5,
        LastSelected = 6,
    }

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/LastTypeExist", order = 10)]
    public class ConditionLastTypeExist : ConditionData
    {
        [Header("Last Type")]
        public ConditionLastType type;

        public ConditionOperatorBool oper;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            if (type == ConditionLastType.LastSelected)
            {
                return CompareBool(data.last_selected != "", oper);
            }

            return false;
        }
    }
}