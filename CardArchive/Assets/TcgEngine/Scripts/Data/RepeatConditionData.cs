using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Base class for all ability conditions, override the IsConditionMet function
    /// </summary>

    public class RepeatConditionData : ScriptableObject
    {
        public virtual int GetMaxRepeatTimes(Game data, AbilityData ability, Card caster)
        {
            return 1;
        }
        public virtual bool IsRepeatConditionMet(Game data, AbilityData ability, int max_repeat_times, int repeat_times)
        {
            return true; //Override this, applies to any target, always checked
        }

        public virtual bool IsOngoingRepeatConditionMet(Game data, AbilityData ability, int max_repeat_times, int repeat_times)
        {
            return true; //Override this, applies to any target, always checked
        }

        public bool CompareBool(bool condition, ConditionOperatorBool oper)
        {
            if (oper == ConditionOperatorBool.IsFalse)
                return !condition;
            return condition;
        }

        public bool CompareInt(int ival1, ConditionOperatorInt oper, int ival2)
        {
            if (oper == ConditionOperatorInt.Equal)
            {
                return ival1 == ival2;
            }
            if (oper == ConditionOperatorInt.NotEqual)
            {
                return ival1 != ival2;
            }
            if (oper == ConditionOperatorInt.GreaterEqual)
            {
                return ival1 >= ival2;
            }
            if (oper == ConditionOperatorInt.LessEqual)
            {
                return ival1 <= ival2;
            }
            if (oper == ConditionOperatorInt.Greater)
            {
                return ival1 > ival2;
            }
            if (oper == ConditionOperatorInt.Less)
            {
                return ival1 < ival2;
            }
            return false;
        }
    }
}