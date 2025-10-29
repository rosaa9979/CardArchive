using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Other Ability의 Trigger을 판단한다.
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Triggered", order = 11)]
    public class ConditionTriggered : ConditionData
    {
        [Header("Oper")]
        public ConditionOperatorBool is_oper;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target)
        {
            Card trigger = data.GetCard(data.ability_triggerer);

            return CompareBool(target.uid == trigger.uid, is_oper);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target)
        {
            Card trigger = data.GetCard(data.ability_triggerer);

            return CompareBool(target == trigger.slot, is_oper);
        }
    }
}