using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Places the (condition-selected) target card onto the board at a designated slot.
    /// The target is chosen by the ability's targeting/conditions; this effect only decides WHERE it goes.
    /// Reuses the engine's PlayCard placement (skip_cost), so the original target is moved (not copied).
    ///
    /// If the designated slot is unavailable and a fallback 'direction' is set, the slot steps by that
    /// direction and retries until it can be placed or it runs off the board (then the target is skipped).
    /// If 'direction' is (0,0), there is no fallback: it only tries the designated slot once.
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/SummonSlot", order = 10)]
    public class EffectSummonSlot : EffectData
    {
        public SlotXY position;    //Designated board position (x, y) on the target owner's side
        public SlotXY direction;   //Fallback step (dx, dy) when the slot is taken; (0,0) = no fallback

        private const int max_steps = 64; //Safety bound; the board is finite so the walk always terminates

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            if (target == null)
                return;

            Game data = logic.GetGameData();
            int p = Slot.GetP(target.player_id);

            Slot slot = new Slot(position, p);

            //Fallback only when a direction is provided; otherwise try the designated slot once
            bool has_direction = direction.x != 0 || direction.y != 0;
            if (has_direction)
            {
                int steps = 0;
                while (!data.CanPlayCard(target, slot, true) && slot.IsValid() && steps++ < max_steps)
                    slot = slot.GetSlot((direction.x, direction.y)); //Off-board returns an invalid slot
            }

            if (data.CanPlayCard(target, slot, true))
                logic.PlayCard(target, slot, true); //Moves the original target to the board slot
        }
    }
}
