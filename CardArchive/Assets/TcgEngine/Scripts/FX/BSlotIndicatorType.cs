using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using TcgEngine.Client;
using UnityEngine;

namespace TcgEngine.FX
{
    public class BSlotIndicatorType
    {
        public virtual List<BSlot> GetTargetBSlot(BSlot current_bslot)
        {
            return new List<BSlot>();
        }
        public virtual void Execute(Game game_data, BSlot current_bslot)
        {
            // 자식에서 구현
        }

        public virtual bool RequireDim(Game game_data)
        {
            return false;
        }

        public void SetSortingLayer(BoardSlot board_slot, string layer_id)
        {
            Game game_data = GameClient.Get().GetGameData();

            Card card = game_data.GetSlotCard(board_slot.GetSlot());
            BoardCard board_card = card != null ? BoardCard.Get(card.uid) : null;
            board_card?.GetCardFX()?.SetSortingOrder(layer_id);
            
            BoardSlotFX slot_fx = board_slot.GetBoardSlotFX();
            slot_fx.SetSortingLayer(layer_id);
        }

        public void ResetAllFX()
        {
            Game game_data = GameClient.Get().GetGameData();

            foreach (BoardSlot board_slot in BoardSlot.GetAll())
            {

                board_slot?.GetBoardSlotFX().ResetIndicator();

                Card card = game_data.GetSlotCard(board_slot.GetSlot());

                if (card != null)
                {
                    BoardCard board_card = BoardCard.Get(card.uid);

                    board_card?.GetCardFX().SetSortingOrder("Default");
                }
            }
        }
    }
}