using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Advances a status value stored on the caster: value = (value + 1) % ability.value.
    /// Used as a cyclic counter (e.g. Tea Party's currently active faction index).
    /// Put this on an EndOfTurn ability of the card that holds the counter (caster = that card).
    ///
    /// Note: Card.AddStatus is additive (value += value), so the new value is assigned directly
    /// to the existing CardStatus instead of being added. The counter is created with duration 0
    /// (permanent) so it is not removed by ReduceStatusDurations.
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/CycleStatus", order = 10)]
    public class EffectCycleStatus : EffectData
    {
        public StatusData status;   //The status used as the cyclic counter

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster)
        {
            if (status == null)
                return;

            int modulo = ability.value;
            if (modulo <= 0)
                return;

            StatusType type = status.effect;

            CardStatus current = caster.GetStatus(type);
            int value = current != null ? current.value : 0;
            int next = (value + 1) % modulo;

            if (current == null)
                caster.AddStatus(type, next, 0); //Create permanent counter (duration 0)
            else
                current.value = next;            //Set directly (AddStatus would accumulate)
        }
    }
}
