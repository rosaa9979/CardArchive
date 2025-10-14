using System.Collections;
using System.Collections.Generic;
using TcgEngine.Client;
using UnityEngine;
using System.Linq;


namespace TcgEngine.FX
{
    public class BSlotIndicatorTypeDragCard : BSlotIndicatorType
    {
        public override void Execute(Game game_data, BSlot current_bslot)
        {
            ResetAllFX();

            HandCard hcard = HandCard.GetDrag();
            Card card = hcard.GetCard();

            if (hcard == null || !card.CardData.IsBoardCard())
                return;


            foreach (BoardSlot board_slot in BoardSlot.GetAll())
            {
                if (game_data.CanPlaceCard(card, board_slot.GetSlot()))
                {
                    BoardSlotFX fx = board_slot.GetBoardSlotFX();
                    fx.SetSortingLayer("UI");
                }
            }


            if (current_bslot != null)
            {
                List<Slot> range_slots = current_bslot.GetSlot().GetNeighborSlot(card.GetAttack());

                foreach (BoardSlot board_slot in BoardSlot.GetAll())
                {
                    if (range_slots.Contains(board_slot.GetSlot()))
                    {
                        BoardSlotFX fx = board_slot.GetBoardSlotFX();
                        fx.SetSortingLayer("UI");
                        fx.SetAnimParameter(true);
                    }
                }
            }
        }

        public override bool RequireDim()
        {
            return true;
        }
    }
}
