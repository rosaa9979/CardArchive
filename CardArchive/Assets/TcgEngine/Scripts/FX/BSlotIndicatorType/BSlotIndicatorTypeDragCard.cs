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
                //Only show unit range over real board slots. The player zone's slot is (0,0,pid)
                //(IsPlayerSlot), whose neighbor BFS would bogusly originate the range near field (1,1).
                if (card.CardData.IsBoardCard() && !current_bslot.GetSlot().IsPlayerSlot())
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

                if (card.CardData.IsRequireTargetSpell())
                {
                    AbilityData ability = card.GetAbility(AbilityTarget.PlayTarget);

                    if (ability != null && ability.CanTarget(game_data, card, current_bslot.GetSlot()))
                    {
                        foreach (BoardSlot board_slot in BoardSlot.GetAll())
                        {
                            if (ability.AreWideRangeConditionsMet(game_data, card, current_bslot.GetSlot(), board_slot.GetSlot()) && ability.AreTargetConditionsMet(game_data, card, board_slot.GetSlot()))
                            {
                                BoardSlotFX fx = board_slot.GetBoardSlotFX();
                                fx.SetAnimParameter(true);
                            }
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

            if (card != null && card.CardData.IsBoardCard())
            {
                //Don't dim when hovering the player zone (slot (0,0,pid)) — a unit can't be placed there.
                BSlot current = BSlotIndicatorUI.Get().GetCurrentBSlot();
                if (current != null && !current.GetSlot().IsPlayerSlot())
                    return true;
                return false;
            }

            return false;
        }
    }
}
