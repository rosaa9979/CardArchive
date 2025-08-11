using System.Collections;
using System.Collections.Generic;
using TcgEngine.Client;
using UnityEngine.Events;
using UnityEngine;
using UnityEngine.UI;
using Unity.VisualScripting;

namespace TcgEngine
{
    public class SlotSelectorPanel : MonoBehaviour
    {
        public GameObject selected_group;
        public Image panel_background;
        public UnityAction<Card, Slot> onSlotSelectedByCard;
        public UnityAction<Card, Slot> onSlotSelectedByBoardCard;
        public UnityAction<AbilityData, Slot> onSlotSelectedByAbility;
        public UnityAction onSlotSelectedClear;

        private Slot current_selected_slot;

        void Update()
        {
            Game game_data = GameClient.Get().GetGameData();
            HandCard hcard = HandCard.GetDrag();
            BoardCard bcard = BoardCard.GetFocus();

            if (game_data != null)
            {
                Vector3 board_pos = GameBoard.Get().RaycastMouseBoard();

                current_selected_slot = GetSelectedSlot(board_pos);

                if (hcard != null && hcard.CardData.IsBoardCard() && GameClient.Get().IsYourTurn())
                {
                    onSlotSelectedByCard?.Invoke(hcard.GetCard(), current_selected_slot);

                    panel_background.enabled = true;
                }

                else if (game_data.selector == SelectorType.SelectTarget && GameClient.Get().IsYourTurn())
                {
                    AbilityData ability = AbilityData.Get(game_data.selector_ability_id);
                    onSlotSelectedByAbility?.Invoke(ability, current_selected_slot);

                    panel_background.enabled = true;
                }

                else if (bcard != null)
                {
                    onSlotSelectedByBoardCard?.Invoke(bcard.GetCard(), current_selected_slot);
                    panel_background.enabled = false;
                }

                else
                {
                    onSlotSelectedClear?.Invoke();
                    panel_background.enabled = false;
                }
            }

        }

        public Slot GetSelectedSlot(Vector3 board_pos)
        {
            BSlot bslot = BSlot.GetNearest(board_pos);

            Slot slot = Slot.None;
            if (bslot != null)
            {
                slot = bslot.GetEmptySlot(board_pos);
            }

            if (bslot != null)
            {
                slot = bslot.GetSlot(board_pos);
            }

            return slot;
        }
    }

}
