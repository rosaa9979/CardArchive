#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TcgEngine.EditorValidation
{
    // Make path-specific omissions visible without forcing every path into the same contract.
    public static class AbilitySchemaValidation
    {
        [MenuItem("TcgEngine/Validate Selected Ability Fields")]
        public static void Run()
        {
            foreach (AbilityData ability in Selection.GetFiltered<AbilityData>(SelectionMode.DeepAssets))
                foreach (string issue in Inspect(ability)) Debug.LogWarning(ability.id + ": " + issue, ability);
        }

        private static bool SupportedContainerTarget(AbilityTarget target)
        {
            return target == AbilityTarget.Self || target == AbilityTarget.PlayerSelf || target == AbilityTarget.PlayerOpponent
                || target == AbilityTarget.AllPlayers || target == AbilityTarget.Club || target == AbilityTarget.EquippedCard
                || target == AbilityTarget.AttachedSlot || target == AbilityTarget.AllCardsAllPiles
                || target == AbilityTarget.AllCardsHand || target == AbilityTarget.AllCardsBoard;
        }

        // A relation condition must read both positions: its own (Slot selected, Slot target) override or a composite evaluator.
        private static bool ReadsPositionPair(ConditionData condition)
        {
            System.Type type = condition.GetType();
            var pair = type.GetMethod(nameof(ConditionData.IsTargetConditionMet), new[] { typeof(Game), typeof(AbilityData), typeof(Card),
                typeof(Slot), typeof(Slot), typeof(AbilityEventContext) });
            var evaluate = type.GetMethod(nameof(ConditionData.Evaluate));
            return pair.DeclaringType != typeof(ConditionData) || evaluate.DeclaringType != typeof(ConditionData);
        }

        private static IEnumerable<string> InspectMove(AbilityData ability)
        {
            var criteria = (ability.conditions_criteria_target ?? new ConditionData[0]).Where(c => c != null).ToList();
            var final = (ability.condition_target ?? new ConditionData[0]).Where(c => c != null).ToList();
            if (!ability.IsMoveSelector())
            {
                if (criteria.Concat(final).Any(c => c is ConditionCanMove))
                    yield return "ConditionCanMove is consumed only by SelectMove.";
                yield break;
            }
            if (criteria.Count != 1 || !(criteria[0] is ConditionCanMove move))
            {
                yield return "SelectMove requires exactly one ConditionCanMove in conditions_criteria_target.";
                yield break;
            }
            if (final.Count > 0)
                yield return "condition_target is not consumed by SelectMove.";
            if (ability.filters_target != null && ability.filters_target.Any(f => f != null))
                yield return "filters_target is not consumed by SelectMove.";
            foreach (ConditionData relation in (move.relation_conditions ?? new ConditionData[0]).Where(c => c != null))
            {
                if (!ReadsPositionPair(relation))
                    yield return "relation_conditions entry " + relation.name + " does not read the (departure, destination) pair.";
            }
            foreach (ConditionData destination in (move.slot_conditions ?? new ConditionData[0]).Where(c => c != null))
            {
                if (destination is ConditionOwner || destination is ConditionCardType || destination is ConditionCanMove)
                    yield return "slot_conditions entry " + destination.name + " checks an occupant; the destination is always empty.";
            }
        }

        public static IEnumerable<string> Inspect(AbilityData ability)
        {
            bool ongoing = ability.trigger == AbilityTrigger.Ongoing;
            if (ability.data_containers != null && ability.data_containers.Any(c => c != null))
            {
                if (!ongoing) yield return "data_containers requires an Ongoing ability; it is not an Effect or a trigger.";
                foreach (DataContainer container in ability.data_containers)
                {
                    if (container == null) continue;
                    if (!(container is NumericModifierData numeric))
                    {
                        yield return "No interpreter is registered for this data container.";
                        continue;
                    }
                    string error = null;
                    try { Gameplay.GameLogic.ValidateNumericModifier(numeric); }
                    catch (System.ArgumentException exception) { error = exception.Message; }
                    if (error != null) yield return error;
                }
                if (!SupportedContainerTarget(ability.criteria_target))
                    yield return "data_containers does not support this ongoing recipient path.";
            }
            bool players = ability.criteria_target == AbilityTarget.PlayerSelf
                || ability.criteria_target == AbilityTarget.PlayerOpponent || ability.criteria_target == AbilityTarget.AllPlayers;
            bool noTarget = ability.criteria_target == AbilityTarget.None;
            bool directOngoing = ongoing && ability.criteria_target != AbilityTarget.AllCardsBoard
                && ability.criteria_target != AbilityTarget.AllCardsAllPiles;
            bool selectedNow = !ability.IsPlaySelectTarget() && ability.IsTargetSelector();
            foreach (string issue in InspectMove(ability)) yield return issue;
            if ((noTarget || (players && !ongoing) || (ongoing && ability.criteria_target == AbilityTarget.AttachedSlot))
                && ability.conditions_criteria_target != null && ability.conditions_criteria_target.Length > 0)
                yield return "conditions_criteria_target is not consumed by this execution path.";
            if ((noTarget || players || directOngoing || ability.criteria_target == AbilityTarget.AllCardData)
                && ability.condition_target != null && ability.condition_target.Length > 0)
                yield return "condition_target is not consumed by this execution path.";
            if ((noTarget || ongoing || selectedNow) && ability.filters_target != null && ability.filters_target.Any(f => f != null))
                yield return "filters_target is not consumed by this execution path.";
            bool area = ability.condition_wide_range != null && ability.condition_wide_range.directions != null
                && (ability.condition_wide_range.directions.Count != 1
                    || ability.condition_wide_range.directions[0].dx != 0 || ability.condition_wide_range.directions[0].dy != 0);
            if (area && (players || noTarget || directOngoing || ability.criteria_target == AbilityTarget.ParentSlotPlayers
                || ability.criteria_target == AbilityTarget.AllCardData))
                yield return "condition_wide_range is not consumed by this execution path.";
            if (!System.Enum.IsDefined(typeof(AbilityEffectTarget), ability.effect_target))
                yield return "Unknown spatial effect_target.";
            if ((ongoing || !ability.UsesSpatialTargets()) && ability.effect_target != AbilityEffectTarget.Slot)
                yield return "effect_target is consumed only by non-ongoing spatial criteria.";
            if (ability.UsesSpatialTargets() && ability.conditions_criteria_target != null
                && ability.conditions_criteria_target.OfType<ConditionTarget>().Any(c => c.type == ConditionTargetType.Card && c.oper == ConditionOperatorBool.IsTrue))
                yield return "Spatial criteria receive positions. Use occupant conditions for unit selection.";
            if (ongoing && ability.HasEffectOrStatus() && ability.conditions_trigger != null && ability.conditions_trigger.Any(c => c != null
                && c.evaluation == ConditionEvaluationPolicy.EvaluateOnce))
                yield return "Ongoing recalculation has no invocation cache. Define its lifetime before using EvaluateOnce.";
        }
    }
}
#endif
