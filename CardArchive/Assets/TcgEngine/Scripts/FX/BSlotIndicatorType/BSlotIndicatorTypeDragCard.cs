using System.Collections;
using System.Collections.Generic;
using TcgEngine.Client;
using UnityEngine;
using System.Linq;
using TcgEngine.UI;


namespace TcgEngine.FX
{
    public class BSlotIndicatorTypeDragCard : BSlotIndicatorType
    {
        public override void Execute(Game game_data, BSlot current_bslot)
        {
            ResetAllFX();

            if (!GameClient.Get().IsYourTurn())
                return;

            HandCard hcard = HandCard.GetDrag();
            Card card = hcard.GetCard();

            if (hcard == null)
                return;


            if (current_bslot != null)
            {
                if (card.CardData.IsBoardCard())
                {
                    List<Slot> range_slots = current_bslot.GetSlot().GetNeighborSlot(card.GetRange());

                    foreach (BoardSlot board_slot in BoardSlot.GetAll())
                    {
                        if (range_slots.Contains(board_slot.GetSlot()))
                        {
                            SetSortingLayer(board_slot, "UI");
                        }
                    }
                }

            }
        }

        public override bool RequireDim(Game game_data)
        {
            if (!GameClient.Get().IsYourTurn())
                return false;

            HandCard hcard = HandCard.GetDrag();
            Card card = hcard.GetCard();

            if (card == null || !card.CardData.IsCitizen())
                return false;
            if (BSlotIndicatorUI.Get().GetCurrentBSlot() == null)
                return false;

            return true;
        }
    }
}
