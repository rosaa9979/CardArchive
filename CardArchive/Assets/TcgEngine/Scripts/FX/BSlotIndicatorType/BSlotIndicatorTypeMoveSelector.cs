using System.Collections;
using System.Collections.Generic;
using TcgEngine.Client;
using UnityEngine;


namespace TcgEngine.FX
{
    //Move selection: dim the screen and raise every board tile above the dim
    public class BSlotIndicatorTypeMoveSelector : BSlotIndicatorType
    {
        public override void Execute(Game game_data, BSlot current_bslot)
        {
            ResetAllFX(game_data);

            if (!IsSelecting(game_data))
                return;

            foreach (BoardSlot board_slot in BoardSlot.GetAll())
                SetSortingLayer(board_slot, "UI");
        }

        public override bool RequireDim(Game game_data)
        {
            return IsSelecting(game_data);
        }

        // The preview belongs to the selecting client only.
        private bool IsSelecting(Game game_data)
        {
            GameClient client = GameClient.Get();
            return client != null && !client.IsObserveMode() && game_data.selector == SelectorType.SelectMove
                && game_data.IsPlayerSelectorTurn(game_data.GetPlayer(client.GetPlayerID()));
        }
    }
}
