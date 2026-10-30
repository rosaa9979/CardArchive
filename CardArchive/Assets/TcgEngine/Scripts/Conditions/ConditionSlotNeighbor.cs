using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// SlotNeighbor checks if the target is within a fixed hex distance (range) of the caster slot.
    /// With a selected slot (move relation, area center), the distance is measured from the selected slot instead.
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/SlotNeighbor", order = 11)]
    public class ConditionSlotNeighbor : ConditionData
    {
        [Header("Slot Range")]
        public int range = 1;
        
        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target, AbilityEventContext context = null)
        {
            return IsTargetConditionMet(data, ability, caster, target.slot, context: context);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target, AbilityEventContext context = null)
        { 
            List<Slot> cslot_neighbor = caster.slot.GetNeighborSlot(range);

            return cslot_neighbor.Contains(target);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot selected, Slot target, AbilityEventContext context = null)
        {
            return selected.GetNeighborSlot(range).Contains(target);
        }
    }
}