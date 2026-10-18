using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effect that adds ability.value to a card/player marker
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/AddMarker", order = 10)]
    public class EffectAddMarker : EffectData
    {
        [UnityEngine.Serialization.FormerlySerializedAs("trait")]
        public MarkerData marker;

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            target.AddMarker(marker.id, ability.value);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            target.AddMarker(marker.id, ability.value);
        }

        public override void DoOngoingEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            target.AddOngoingMarker(marker.id, ability.value);
        }

        public override void DoOngoingEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            target.AddOngoingMarker(marker.id, ability.value);
        }
    }
}