using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// SlotRange check each axis variable individualy for range between the caster and target
    /// If you want to check the travel distance instead (all at once) use SlotDist
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
    }
}