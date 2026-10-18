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
    public class EffectConditionalCaster : EffectConditional
    {
        protected override object GetConditionTarget(Card caster, object target) { return caster; }
    }
}
