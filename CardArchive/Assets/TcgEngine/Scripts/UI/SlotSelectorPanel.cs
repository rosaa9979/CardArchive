using System.Collections;
using System.Collections.Generic;
using TcgEngine.Client;
using UnityEngine.Events;
using UnityEngine;
using UnityEngine.UI;

namespace TcgEngine
{
    public class SlotSelectorPanel : MonoBehaviour
    {
        public GameObject selected_group;
        public Image panel_background;
        public UnityAction<Slot> onSlotSelected;

        private Slot current_selected_slot;

        void Update()
        {
            Game game_data = GameClient.Get().GetGameData();
            HandCard hcard = HandCard.GetDrag();

            if (game_data != null)
            {
                if (hcard != null || game_data.selector == SelectorType.SelectTarget || game_data.selector == SelectorType.SelectorCard || game_data.selector == SelectorType.SelectorChoice)
                {
                    Vector3 board_pos = GameBoard.Get().RaycastMouseBoard();

                    if (current_selected_slot == null || current_selected_slot != GetSelectedSlot(board_pos))
                    {
                        current_selected_slot = GetSelectedSlot(board_pos);

                        onSlotSelected?.Invoke(current_selected_slot);
                    }

                    panel_background.enabled = true;
                }

                else
                {
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
