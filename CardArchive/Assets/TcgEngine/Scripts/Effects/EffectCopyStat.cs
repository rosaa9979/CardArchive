using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Copies a marker value from the caster onto the target.
    /// Tea Party use: on OnAddClubOther, the club card (caster) copies its active-host marker
    /// onto the newly joined member (target = AbilityTriggerer), so the member adopts the current host.
    ///
    /// - require_club (optional): only act when the target has that club (gate to actual members).
    /// - only_if_missing: skip if the target already has the marker, so unrelated events don't overwrite it.
    /// SetMarker sets the value directly (creates the marker if absent).
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/CopyStat", order = 10)]
    public class EffectCopyStat : EffectData
    {
        [UnityEngine.Serialization.FormerlySerializedAs("trait")]
        public MarkerData marker;       //Marker whose value is copied from caster to target
        public ClubData require_club;   //Optional: only act if the target has this club
        public bool only_if_missing = true; //Skip if target already has the marker

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            if (marker == null || target == null)
                return;

            if (require_club != null && !target.HasClub(require_club))
                return;

            CardMarker existing = target.GetMarker(marker.id);
            if (existing != null && only_if_missing)
                return;

            int value = caster.GetMarkerValue(marker);
            target.SetMarker(marker.id, value);
        }
    }
}
