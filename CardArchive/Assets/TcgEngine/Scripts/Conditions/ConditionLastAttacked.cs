using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.AI;

namespace TcgEngine
{
    /// <summary>
    /// Condition that compares the target category of an ability to the actual target (card, player or slot)
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/LastAttacked", order = 10)]
    public class ConditionLastAttacked : ConditionData
    {

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target)
        {
            if (!data.last_player_attacked && data.last_attacked == target.uid)
                return true;
            return false; //Is Card
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target)
        {
            return data.last_player_attacked; //Is Player
        }
    }
}