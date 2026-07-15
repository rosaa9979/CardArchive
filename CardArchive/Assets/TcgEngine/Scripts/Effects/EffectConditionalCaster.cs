using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Caster-side variant of EffectConditional: the branch condition is evaluated against the
    /// CASTER (not the per-target), then the chosen effect list still runs on the real target.
    /// Lets you do "if I (the caster) am X, do A to each target, else B" while reusing any existing
    /// target condition (e.g. ConditionStatCustom reads caster.GetTraitValue).
    /// A null condition counts as "met" (runs effects_true).
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/ConditionalCaster", order = 10)]
    public class EffectConditionalCaster : EffectData
    {
        public ConditionData condition;
        public EffectData[] effects_true;
        public EffectData[] effects_false;

        private bool IsMet(GameLogic logic, AbilityData ability, Card caster)
        {
            return condition == null || condition.IsTargetConditionMet(logic.GetGameData(), ability, caster, caster);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            EffectData[] list = IsMet(logic, ability, caster) ? effects_true : effects_false;
            if (list == null) return;
            foreach (EffectData e in list)
                if (e != null) e.DoEffect(logic, ability, caster, target);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            EffectData[] list = IsMet(logic, ability, caster) ? effects_true : effects_false;
            if (list == null) return;
            foreach (EffectData e in list)
                if (e != null) e.DoEffect(logic, ability, caster, target);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Slot target)
        {
            EffectData[] list = IsMet(logic, ability, caster) ? effects_true : effects_false;
            if (list == null) return;
            foreach (EffectData e in list)
                if (e != null) e.DoEffect(logic, ability, caster, target);
        }
    }
}
