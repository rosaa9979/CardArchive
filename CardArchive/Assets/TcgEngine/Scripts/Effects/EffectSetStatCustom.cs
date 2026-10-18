using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effect that sets a marker to a specific value
    /// </summary>
    
    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/SetStatCustom", order = 10)]
    public class EffectSetStatCustom : EffectData
    {
        [UnityEngine.Serialization.FormerlySerializedAs("trait")]
        public MarkerData marker;

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            target.SetMarker(marker.id, ability.value);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            target.SetMarker(marker.id, ability.value);
        }

        //[stored_slot exception] When the target is a SLOT, store the encoded slot coordinate on the
        //CASTER's marker (slot-memory, e.g. "포격"). The slot is encoded via Slot.ToMarkerCode().
        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Slot target)
        {
            if (marker == null)
                return;
            caster.SetMarker(marker.id, target.ToMarkerCode());
        }

        public override void DoOngoingEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            target.SetMarker(marker.id, ability.value);
        }

        public override void DoOngoingEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            target.SetMarker(marker.id, ability.value);
        }
    }
}