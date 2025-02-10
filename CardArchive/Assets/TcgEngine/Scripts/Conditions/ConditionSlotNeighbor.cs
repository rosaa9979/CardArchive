using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// SlotRange check each axis variable individualy for range between the caster and target
    /// If you want to check the travel distance instead (all at once) use SlotDist
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/SlotNeighbor", order = 11)]
    public class ConditionSlotNeighbor : ConditionData
    {
        [Header("Slot Range")]
        public int range = 1;
        public LastType last_type;
        
        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target)
        {
            return IsTargetConditionMet(data, ability, caster, target.slot);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target)
        { 
            List<Slot> cslot_neighbor = caster.slot.GetNeighborSlot(range);

            return cslot_neighbor.Contains(target);
        }
    }
}