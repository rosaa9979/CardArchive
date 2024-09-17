using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Checks if a slot contains a card or not
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/SlotAttachmentEmpty", order = 11)]
    public class ConditionSlotAttachmentEmpty : ConditionData
    {
        [Header("Slot Attachment Is Empty")]
        public ConditionOperatorBool oper;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target)
        {
            return CompareBool(false, oper); //Target is not empty slot
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target)
        {
            return CompareBool(false, oper); //Target is not empty slot
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target)
        { 
            Card attach_card = data.GetAttachCard(target);
            return CompareBool(attach_card == null, oper);
        }
    }
}