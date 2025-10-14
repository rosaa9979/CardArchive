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

            current_type = new BSlotIndicatorTypeNone();

            if (HandCard.GetDrag() != null)
                current_type = new BSlotIndicatorTypeDragCard();

            Vector3 board_pos = GameBoard.Get().RaycastMouseBoard();
            BSlot bslot = BSlot.GetNearest(board_pos);

            if (bslot != prev_bslot)
            {
                prev_bslot = bslot;
                current_type.Execute(game_data, prev_bslot);
            }


            if (current_type.RequireDim())
                ui_panel.Show();
            else
                ui_panel.Hide();
        }

        public static BSlotIndicatorUI Get()
        {
            return _instance;
        }
    }
}
