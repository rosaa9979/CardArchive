using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// SlotRange check each axis variable individualy for range between the caster and target
    /// If you want to check the travel distance instead (all at once) use SlotDist
    /// </summary>

    [CreateAssetMenu(fileName = "repeat", menuName = "TcgEngine/RepeatCondition/StaticValue", order = 11)]
    public class RepeatStaticValue : RepeatConditionData
    {
        [Header("Static Value")]
        public int value = 1;

        public override int GetMaxRepeatTimes(Game data, AbilityData ability, Card caster)
        {
            return value;
        }
        
        public override bool IsRepeatConditionMet(Game data, AbilityData ability, int max_repeat_times, int repeat_times)
        {
            return true;
        }

        public override bool IsOngoingRepeatConditionMet(Game data, AbilityData ability, int max_repeat_times, int repeat_times)
        {
            if (repeat_times < value)
                return true;

            return false;
        }
    }
}