using System.Collections;
using System.Collections.Generic;
using TcgEngine.Client;
using UnityEngine;
using UnityEngine.UI;

namespace TcgEngine
{
    public class TurnStartUI : MonoBehaviour
    {
        public Image turn_background;
        public Text turn_text;

        public Sprite ally_background;
        public Sprite enemy_background;

        public void SetTurn(int player_id)
        {

            if (turn_background != null)
                turn_background.sprite = player_id == GameClient.Get().GetPlayerID() ? ally_background : enemy_background;

            if (turn_text != null)
                turn_text.text = player_id == GameClient.Get().GetPlayerID() ? "My Phase" : "Enemy Phase";
        }
    }
}