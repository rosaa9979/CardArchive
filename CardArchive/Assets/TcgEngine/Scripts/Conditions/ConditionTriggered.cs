using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// ability의 widerangearea condition에서만 사용하며, 선택한 슬롯을 기점으로 효과 범위를 정의한다
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Triggered", order = 11)]
    public class ConditionTriggered : ConditionData
    {
        [Header("Oper")]
        public ConditionOperatorBool is_oper;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target)
        {
            Card trigger = data.GetCard(data.ability_triggerer);

            return CompareBool(target == trigger.slot, is_oper);

        }
    }
}