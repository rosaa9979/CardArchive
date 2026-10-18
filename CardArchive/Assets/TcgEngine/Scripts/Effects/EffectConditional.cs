using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Runs one of two effect lists depending on whether a condition is met on the target.
    /// Lets you branch without a bespoke effect ("if target is X do A, else do B").
    /// A null condition counts as "met" (runs effects_true).
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/Conditional", order = 10)]
    public class EffectConditional : EffectData
    {
        public ConditionData condition;
        public EffectData[] effects_true;
        public EffectData[] effects_false;

        protected virtual object GetConditionTarget(Card caster, object target) { return target; }

        private EffectData[] SelectEffects(GameLogic logic, AbilityData ability, Card caster, object target, AbilityEventContext context)
        {
            return ConditionEvaluation.Matches(condition, logic.GameData, ability, caster,
                GetConditionTarget(caster, target), ConditionStage.Effect, context) ? effects_true : effects_false;
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            DoEffect(logic, ability, caster, target, null);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target, AbilityEventContext context)
        {
            EffectData[] branch = SelectEffects(logic, ability, caster, target, context);
            if (branch == null) return;
            foreach (EffectData effect in branch) effect?.DoEffect(logic, ability, caster, target, context);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            DoEffect(logic, ability, caster, target, null);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target, AbilityEventContext context)
        {
            EffectData[] branch = SelectEffects(logic, ability, caster, target, context);
            if (branch == null) return;
            foreach (EffectData effect in branch) effect?.DoEffect(logic, ability, caster, target, context);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Slot target)
        {
            DoEffect(logic, ability, caster, target, null);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Slot target, AbilityEventContext context)
        {
            EffectData[] branch = SelectEffects(logic, ability, caster, target, context);
            if (branch == null) return;
            foreach (EffectData effect in branch) effect?.DoEffect(logic, ability, caster, target, context);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, CardData target)
        {
            DoEffect(logic, ability, caster, target, null);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, CardData target, AbilityEventContext context)
        {
            EffectData[] branch = SelectEffects(logic, ability, caster, target, context);
            if (branch == null) return;
            foreach (EffectData effect in branch) effect?.DoEffect(logic, ability, caster, target, context);
        }

    }
}
