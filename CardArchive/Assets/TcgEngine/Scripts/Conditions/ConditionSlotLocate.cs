using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// SlotValue compare each slot x and y to a specific value, like slot.x >=3 and slot.y < 5
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/SlotLocate", order = 11)]
    public class ConditionSlotLocate : ConditionData
    {
        [Header("Slot Locate")]
        public bool Inside;
        public bool Outside;
        public bool Neutral;
        
        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target)
        {
            return IsTargetConditionMet(data, ability, caster, target.slot);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target)
        {
            Player player = data.GetPlayer(caster.player_id);
            Player oplayer = data.GetOpponentPlayer(player.player_id);

            if (Inside && (Slot.GetPlayerSelf(player.player_id).Contains(target) || Slot.GetPlayerSelf(oplayer.player_id).Contains(target)))
                return true;

            if (Outside && target.y != 3 && (!Slot.GetPlayerSelf(player.player_id).Contains(target) && !Slot.GetPlayerSelf(oplayer.player_id).Contains(target)))
                return true;
            
            if (Neutral && target.y == 3)
                return true;
            
            return false;
        }
    }
}