using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// SlotRange check each axis variable individualy for range between the caster and target
    /// If you want to check the travel distance instead (all at once) use SlotDist
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/SlotPid", order = 11)]
    public class ConditionSlotPid : ConditionData
    {
        [Header("Slot Pid")]
        public bool player = true;
        public bool opponent = true;
        public bool neutral = true;
        //public ConditionOperatorBool oper;
        
        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target)
        {
            return IsTargetConditionMet(data, ability, caster, target.slot);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target)
        {
            Player p = data.GetPlayer(caster.player_id);
            Player op = data.GetOpponentPlayer(p.player_id);
            
            if (player && target.p == p.player_id)
                return true;

            if (opponent && target.p == op.player_id)
                return true;

            if (neutral && target.p == 2)
                return true;

            return false;
        }
    }
}