using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.AI;

namespace TcgEngine
{
    /// <summary>
    /// Condition that compares the target category of an ability to the actual target (card, player or slot)
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/LastType", order = 10)]
    public class ConditionLastType : ConditionData
    {
        [Header("Last Type")]
        public LastType type;

        public ConditionOperatorBool oper;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target)
        {
            bool result = false;

            if (type == LastType.LastPlayed && data.last_played == target.uid)
                result = true;
            if (type == LastType.LastAttacked && data.last_attacked == target.uid)
                result = true;
            if (type == LastType.LastTargeted && data.last_target == target.uid)
                result = true;
            if (type == LastType.LastSummoned && data.last_summoned == target.uid)
                result = true;
            if (type == LastType.LastDestroyed && data.last_destroyed == target.uid)
                result = true;

            return CompareBool(result, oper);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target)
        {
            if (type == LastType.LastPlayed)
                return false;

            bool result = false;

            if (type == LastType.LastAttacked && data.last_attacked_slot == target)
                result = true;
            if (type == LastType.LastTargeted && data.last_targeted_slot == target)
                result = true;
            if (type == LastType.LastSummoned && data.last_summoned_slot == target)
                result = true;
            if (type == LastType.LastDestroyed && data.last_destroyed_slot == target)
                result = true;

            return CompareBool(result, oper);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target)
        {
            return false;
        }
    }
}