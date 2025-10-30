using System.Collections;
using System.Collections.Generic;
using TcgEngine.Client;
using TcgEngine.UI;
using UnityEngine;


namespace TcgEngine.FX
{
    public class BSlotIndicatorTypeHoverUnit : BSlotIndicatorType
    {
        public override void Execute(Game game_data, BSlot current_bslot)
        {
            ResetAllFX();

            if (current_bslot == null)
                return;

            Card card = game_data.GetSlotCard(current_bslot.GetSlot());

            if (card == null || !card.CardData.IsCitizen())
                return;

            List<Slot> range_slots = current_bslot.GetSlot().GetNeighborSlot(card.GetRange());

            foreach (BoardSlot board_slot in BoardSlot.GetAll())
            {
                if (range_slots.Contains(board_slot.GetSlot()))
                {
                    SetSortingLayer(board_slot, "UI");
                    //fx.SetAnimParameter(true);
                }
            }
        }

        public override bool RequireDim(Game game_data)
        {
            Card card = game_data.GetSlotCard(BSlotIndicatorUI.Get().GetCurrentBSlot().GetSlot());

            if (card == null || !card.CardData.IsCitizen())
                return false;
            return true;
        }
    }
}
