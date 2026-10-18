using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effect that removes a card/player marker
    /// </summary>
    
    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/RemoveMarker", order = 10)]
    public class EffectRemoveMarker : EffectData
    {
        [UnityEngine.Serialization.FormerlySerializedAs("trait")]
        public MarkerData marker;

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            target.RemoveMarker(marker.id);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            target.RemoveMarker(marker.id);
        }
    }
}