using System.Collections;
using System.Collections.Generic;
using TcgEngine.Client;
using UnityEngine.Events;
using UnityEngine;
using UnityEngine.UI;
using Unity.VisualScripting;
using System.Runtime.CompilerServices;
using TcgEngine.UI;

namespace TcgEngine.UI
{
    public enum TargetSelectorUIType
    {
        None = 0,
        AbilitySelector = 10,
        DragHandCard = 20,
        HoverBoardCard = 30
    }

    public class TargetSelectorUI : MonoBehaviour
    {
        public UIPanel ui_panel;
        public UnityAction<TargetSelectorUIType, BSlot> onCurrentSlotChanged;

        private TargetSelectorUIType current_type;
        private BSlot current_slot = null;

        private static TargetSelectorUI _instance;

        public void Awake()
        {
            _instance = this;
        }

        public void Update()
        {
            if (!GameClient.Get().IsReady())
                return;

            TargetSelectorUIType current_type = TargetSelectorUIType.None;
            Game game_data = GameClient.Get().GetGameData();

            if (game_data.selector == SelectorType.SelectTarget && game_data.selector_player_id == GameClient.Get().GetPlayerID())
                current_type = TargetSelectorUIType.AbilitySelector;

            HandCard drag_card = HandCard.GetDrag();
            if (drag_card != null && drag_card.CardData.IsBoardCard())
                current_type = TargetSelectorUIType.DragHandCard;


            Vector3 board_pos = GameBoard.Get().RaycastMouseBoard();
            BSlot bslot = BSlot.GetNearest(board_pos);

            if (bslot != current_slot && current_type != TargetSelectorUIType.None)
            {
                current_slot = bslot;
                onCurrentSlotChanged?.Invoke(current_type, current_slot);
            }

            //if (current_type != TargetSelectorUIType.None)
            //    ui_panel.Show();
            //else
            //    ui_panel.Hide();
        }

        public static TargetSelectorUI Get()
        {
            return _instance;
        }
    }
}
