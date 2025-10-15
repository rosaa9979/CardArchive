using System.Collections;
using System.Collections.Generic;
using TcgEngine.Client;
using UnityEngine.Events;
using UnityEngine;
using UnityEngine.UI;
using Unity.VisualScripting;
using System.Runtime.CompilerServices;
using TcgEngine.FX;
using UnityEngine.Rendering.UI;

namespace TcgEngine.UI
{
    public class BSlotIndicatorUI : MonoBehaviour
    {
        public UIPanel ui_panel;

        private BSlot prev_bslot = null;
        private BSlotIndicatorType current_type;

        private static BSlotIndicatorUI _instance;

        public void Awake()
        {
            _instance = this;
        }

        public void Start()
        {
            
        }

        public void Update()
        {
            if (!GameClient.Get().IsReady())
                return;

            Game game_data = GameClient.Get().GetGameData();

            Vector3 board_pos = GameBoard.Get().RaycastMouseBoard();
            BSlot bslot = BSlot.GetNearest(board_pos);

            current_type = SetCurrentType(game_data, bslot);

            BSlotIndicatorType new_type = SetCurrentType(game_data, bslot);

            //if (bslot != prev_bslot)
            //{
                prev_bslot = bslot;
                current_type.Execute(game_data, prev_bslot);
            //}


            if (current_type.RequireDim(game_data))
                ui_panel.Show();
            else
                ui_panel.Hide();
        }

        public BSlot GetCurrentBSlot()
        {
            return prev_bslot;
        }

        private BSlotIndicatorType SetCurrentType(Game game_data, BSlot current_slot)
        {

            HandCard hcard = HandCard.GetDrag();

            if (hcard != null)
                return new BSlotIndicatorTypeDragCard();

            if (current_slot != null)
            {
                Card current_hovering_unit = game_data.GetSlotCard(current_slot.GetSlot());

                if (current_hovering_unit != null && current_hovering_unit.CardData.IsCitizen())
                    return new BSlotIndicatorTypeHoverUnit();
            }

            return new BSlotIndicatorTypeNone();
        }

        public static BSlotIndicatorUI Get()
        {
            return _instance;
        }
    }
}
