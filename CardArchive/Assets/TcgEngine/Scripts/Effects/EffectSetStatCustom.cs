using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effect that sets custom stats to a specific value
    /// </summary>
    
    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/SetStatCustom", order = 10)]
    public class EffectSetStatCustom : EffectData
    {
        public TraitData trait;

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            target.SetTrait(trait.id, ability.value);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            target.SetTrait(trait.id, ability.value);
        }

        //[stored_slot exception] When the target is a SLOT, store the encoded slot coordinate on the
        //CASTER's trait (slot-memory, e.g. "포격"). The slot is encoded via Slot.ToTraitCode().
        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Slot target)
        {
            if (trait == null)
                return;
            caster.SetTrait(trait.id, target.ToTraitCode());
        }

        public override void DoOngoingEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            target.SetTrait(trait.id, ability.value);
        }

        public override void DoOngoingEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            target.SetTrait(trait.id, ability.value);
        }
    }
}