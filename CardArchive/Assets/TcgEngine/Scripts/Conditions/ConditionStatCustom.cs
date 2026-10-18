using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Compares a card's or player's marker value
    /// </summary>
    
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/StatCustom", order = 10)]
    public class ConditionStatCustom : ConditionData
    {
        [Header("Marker value is")]
        [UnityEngine.Serialization.FormerlySerializedAs("trait")]
        public MarkerData marker;
        public ConditionOperatorInt oper;
        public int value;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target, AbilityEventContext context = null)
        {
            return CompareInt(target.GetMarkerValue(marker), oper, value);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target, AbilityEventContext context = null)
        {
            return CompareInt(target.GetMarkerValue(marker), oper, value);
        }
    }
}