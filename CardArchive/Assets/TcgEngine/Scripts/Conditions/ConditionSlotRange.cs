using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// SlotRange checks if the target is within the caster's current range (hex distance, Card.GetRange()).
    /// Use SlotNeighbor for a fixed distance instead.
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/SlotRange", order = 11)]
    public class ConditionSlotRange : ConditionData
    {
        [Header("Slot in Range")]
        public ConditionOperatorBool oper;
        
        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target, AbilityEventContext context = null)
        {
            return IsTargetConditionMet(data, ability, caster, target.slot, context: context);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target, AbilityEventContext context = null)
        {
            List<Slot> range_slots = caster.slot.GetNeighborSlot(caster.GetRange());

            return CompareBool(range_slots.Contains(target), oper);
        }
    }
}