using System.Collections;
using System.Collections.Generic;
using TcgEngine.Client;
using UnityEngine.Events;
using UnityEngine;
using UnityEngine.UI;
using Unity.VisualScripting;
using System.Runtime.CompilerServices;
using TcgEngine.UI;

namespace TcgEngine
{
    public class TargetSelectorUI : MonoBehaviour
    {
        public UIPanel ui_panel;

        private BoardSlot current_slot = null;

        public void Update()
        {
            if (!GameClient.Get().IsReady())
                return;

            Game game_data = GameClient.Get().GetGameData();
            bool should_show = false;

            if (game_data.selector == SelectorType.SelectTarget && game_data.selector_player_id == GameClient.Get().GetPlayerID())
                should_show = true;

            if (should_show)
                ui_panel.Show();
            else
                ui_panel.Hide();
        }
    }
}
