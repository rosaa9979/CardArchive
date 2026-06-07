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

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            bool met = condition == null || condition.IsTargetConditionMet(logic.GetGameData(), ability, caster, target);
            Run(met ? effects_true : effects_false, logic, ability, caster, target);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            bool met = condition == null || condition.IsTargetConditionMet(logic.GetGameData(), ability, caster, target);
            EffectData[] list = met ? effects_true : effects_false;
            if (list == null) return;
            foreach (EffectData e in list)
                if (e != null) e.DoEffect(logic, ability, caster, target);
        }

        private void Run(EffectData[] list, GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            if (list == null) return;
            foreach (EffectData e in list)
                if (e != null) e.DoEffect(logic, ability, caster, target);
        }
    }
}
