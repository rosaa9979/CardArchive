using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TcgEngine.Client;
using TMPro;
using TcgEngine;
using UnityEngine;

namespace TcgEngine.UI
{
    /// <summary>
    /// Matchmaking panel is just a loading panel that displays how many players are found yet
    /// </summary>

    public class MatchmakingPanel : UIPanel
    {
        public TMP_Text text;
        public Text players_txt;
        public Text code_txt;
        public Image code_bg;

        private int dot_count = 0;
        private float dot_timer = 0.0f;
        private float dot_interval = 0.8f;
        private string previous_text = "";


        private static MatchmakingPanel instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        protected override void Start()
        {
            base.Start();
            code_txt.text = "";
        }

        protected override void Update()
        {
            base.Update();

            string current_text;

            if (GameClientMatchmaker.Get().IsConnected())
                current_text = "매치 중";
            else
                current_text = "접속 중";

            if (current_text != previous_text)
            {
                dot_count = 1;
                dot_timer = 0.0f;
                previous_text = current_text;
            }

            dot_timer += Time.deltaTime;

            if (dot_timer >= dot_interval)
            {
                dot_timer = 0.0f;
                dot_count++;

                if (dot_count > 3)
                    dot_count = 1;
            }

            string dots = new string('.', dot_count);
            text.text = current_text + dots;

            code_txt.text = "";

            string group = GameClientMatchmaker.Get().GetGroup();
            if (group != null && group.StartsWith("code_"))
                code_txt.text = group.Replace("code_", "");

            code_bg.enabled = !string.IsNullOrWhiteSpace(code_txt.text);
        }

        public void SetCount(int players)
        {
            if (players_txt != null)
                players_txt.text = players.ToString() + "/" + GameClientMatchmaker.Get().GetNbPlayers();
        }

        public void OnClickCancel()
        {
            GameClientMatchmaker.Get().StopMatchmaking();
            Hide();
        }

        public override void Show(bool instant = false)
        {
            base.Show(instant);
            if (players_txt != null)
                players_txt.text = "";
        }

        public static MatchmakingPanel Get()
        {
            return instance;
        }
    }
}