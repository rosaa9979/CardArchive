using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Compares basic player stats such as attack/hp/mana
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/PlayerStat", order = 10)]
    public class ConditionPlayerStat : ConditionData
    {
        [Header("Card stat is")]
        public ConditionStatType type;
        public ConditionOperatorInt oper;
        public int value;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target, AbilityEventContext context = null)
        {
            Player ptarget = data.GetPlayer(target.player_id);
            return IsTargetConditionMet(data, ability, caster, ptarget, context: context);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target, AbilityEventContext context = null)
        {
            if (type == ConditionStatType.HP)
            {
                return CompareInt(target.hp, oper, value);
            }

            if (type == ConditionStatType.Mana)
            {
                return CompareInt(target.mana, oper, value);
            }

            return false;
        }
    }
}