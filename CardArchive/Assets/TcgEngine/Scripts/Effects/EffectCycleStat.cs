using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Advances a trait value stored on the caster: value = (value + 1) % ability.value.
    /// Used as a cyclic counter (e.g. Tea Party's currently active host index).
    /// Put this on an EndOfTurn ability of the card that holds the counter (caster = that card).
    /// SetTrait sets the value directly (no accumulation).
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/CycleStat", order = 10)]
    public class EffectCycleStat : EffectData
    {
        public TraitData trait;   //The trait used as the cyclic counter

        //criteria_target = None
        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster)
        {
            Cycle(ability, caster);
        }

        //criteria_target = Self (target == caster); cycle the caster's own counter
        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            Cycle(ability, caster);
        }

        private void Cycle(AbilityData ability, Card caster)
        {
            if (trait == null)
                return;

            int modulo = ability.value;
            if (modulo <= 0)
                return;

            CardTrait current = caster.GetTrait(trait.id);
            int value = current != null ? current.value : 0;
            int next = (value + 1) % modulo;

            caster.SetTrait(trait.id, next);
        }
    }
}
