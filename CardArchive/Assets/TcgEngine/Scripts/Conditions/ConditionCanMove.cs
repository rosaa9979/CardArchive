using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Move selection (AbilityTarget.SelectMove): checks one (from, to) pair.
    /// selected = the departure slot, target = the destination slot.
    /// The engine always checks Game.CanMoveCard(unit, to) separately (empty slot, territory, place restriction).
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/CanMove", order = 11)]
    public class ConditionCanMove : ConditionData
    {
        [Header("Unit to move (Card)")]
        public ConditionData[] unit_conditions;

        [Header("Destination (Slot)")]
        public ConditionData[] slot_conditions;

        [Header("Departure -> Destination (Slot selected, Slot target)")]
        public ConditionData[] relation_conditions;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot selected, Slot target, AbilityEventContext context = null)
        {
            Card unit = data.GetSlotCard(selected);
            if (unit == null)
                return false;

            if (unit_conditions != null)
            {
                foreach (ConditionData cond in unit_conditions)
                {
                    if (cond != null && !ConditionEvaluation.Matches(cond, data, ability, caster, unit, ConditionStage.Criteria, context))
                        return false;
                }
            }

            if (slot_conditions != null)
            {
                foreach (ConditionData cond in slot_conditions)
                {
                    if (cond != null && !ConditionEvaluation.Matches(cond, data, ability, caster, target, ConditionStage.Criteria, context))
                        return false;
                }
            }

            if (relation_conditions != null)
            {
                foreach (ConditionData cond in relation_conditions)
                {
                    if (cond != null && !ConditionEvaluation.Matches(cond, data, ability, caster, target, ConditionStage.Criteria, context, selected))
                        return false;
                }
            }

            return true;
        }

        //Only the (from, to) pair is supported, other target types never pass
        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target, AbilityEventContext context = null)
        {
            return false;
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target, AbilityEventContext context = null)
        {
            return false;
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Slot target, AbilityEventContext context = null)
        {
            return false;
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, CardData target, AbilityEventContext context = null)
        {
            return false;
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card selected, Slot target, AbilityEventContext context = null)
        {
            return false;
        }
    }
}
