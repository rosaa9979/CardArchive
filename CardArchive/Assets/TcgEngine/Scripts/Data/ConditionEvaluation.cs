using System;

namespace TcgEngine
{
    public enum ConditionEvaluationPolicy { Revalidate = 0, EvaluateOnce = 1 }
    public enum ConditionStage { Trigger, Criteria, Target, Range, Effect }

    // The same evaluator is used for leaf conditions and logical compositions.
    public static class ConditionEvaluation
    {
        public static bool Matches(ConditionData condition, Game data, AbilityData ability, Card caster,
            object target, ConditionStage stage, AbilityEventContext context = null, object selected = null)
        {
            if (condition == null) return true;
            var execution = context as AbilityExecutionContext;
            Func<bool> predicate = () => condition.Evaluate(data, ability, caster, target, stage, context, selected);
            if (execution == null) return predicate();
            string key = stage + ":" + (stage == ConditionStage.Trigger ? 0 : execution.iteration)
                + ":" + Identity(target) + ":" + Identity(selected);
            return execution.Evaluate(condition, key, predicate);
        }

        private static string Identity(object target)
        {
            if (target is Card card) return "card/" + card.uid;
            if (target is Player player) return "player/" + player.player_id;
            if (target is Slot slot) return "slot/" + slot.x + "/" + slot.y + "/" + slot.p;
            if (target is CardData definition) return "definition/" + definition.id;
            return "none";
        }
    }
}
