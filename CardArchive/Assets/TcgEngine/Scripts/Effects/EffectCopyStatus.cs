using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Copies a status value from the caster onto the target.
    /// Tea Party use: on OnAddClubOther, the club card (caster) copies its active-faction status
    /// onto the newly joined member (target = AbilityTriggerer), so the member adopts the current faction.
    ///
    /// - require_club (optional): only act when the target has that club (gate to actual members).
    /// - only_if_missing: skip if the target already has the status, so unrelated events don't overwrite it.
    /// The value is set directly (AddStatus is additive), creating a permanent status (duration 0) if absent.
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/CopyStatus", order = 10)]
    public class EffectCopyStatus : EffectData
    {
        public StatusData status;       //Status whose value is copied from caster to target
        public ClubData require_club;   //Optional: only act if the target has this club
        public bool only_if_missing = true; //Skip if target already has the status

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            if (status == null || target == null)
                return;

            if (require_club != null && !target.HasClub(require_club))
                return;

            StatusType type = status.effect;
            CardStatus existing = target.GetStatus(type);
            if (existing != null && only_if_missing)
                return;

            int value = caster.GetStatusValue(type);
            if (existing == null)
                target.AddStatus(type, value, 0); //Create permanent (duration 0)
            else
                existing.value = value;           //Set directly (AddStatus would accumulate)
        }
    }
}
