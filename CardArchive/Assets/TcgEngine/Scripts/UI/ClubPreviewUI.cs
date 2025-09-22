using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using TcgEngine;
using Unity.VisualScripting.Antlr3.Runtime.Tree;

namespace TcgEngine.UI
{
    /// <summary>
    /// In the game scene, the CardPreviewUI is what shows the card in big with extra info when hovering a card
    /// </summary>

    public class ClubPreviewUI : MonoBehaviour
    {
        public UIPanel ui_panel;
        public CardUI card_ui;
        public Text desc;
        public float hover_delay = 0.0f;
        public float hover_delay_mobile = 0.1f;

        public RectTransform[] side_rows;
        public StatusLine[] status_lines;

        private float preview_timer = 0f;
        private Vector2[] start_pos;
        private Vector2[] final_pos;

        private void Start()
        {
            start_pos = new Vector2[side_rows.Length];
            for (int i = 0; i < side_rows.Length; i++)
            {
                start_pos[i] = side_rows[i].anchoredPosition;
            }
        }

        void Update()
        {
            if (!GameClient.Get().IsReady())
                return;

            foreach (StatusLine line in status_lines)
                line.Hide();

            PlayerControls controls = PlayerControls.Get();
            Game game_data = GameClient.Get().GetGameData();
            ClubUI club_ui = ClubUI.GetFocus();
            Card club_card = club_ui != null ? club_ui.GetCard() : null;

            float delay = club_card != null ? hover_delay : 0.0f;

            if (GameTool.IsMobile())
                delay = hover_delay_mobile;
            

            bool hover_only = !Input.GetMouseButton(0) && !HandCardArea.Get().IsDragging();
            bool should_show_preview = hover_only && !GameUI.IsUIOpened() && club_card != null;
            

            if (should_show_preview)
                preview_timer += Time.deltaTime;
            else
                preview_timer = 0f;

            bool show_preview = should_show_preview && preview_timer >= delay;
            ui_panel.SetVisible(show_preview);

            if (show_preview)
            {
                bool owner_player = club_card.player_id == GameClient.Get().GetPlayerID();
                for (int idx = 0; idx < side_rows.Length; idx++)
                {
                    side_rows[idx].anchoredPosition = owner_player ? start_pos[idx] : -start_pos[idx];
                }

                CardData icard = club_card.CardData;
                //card_ui.SetCard(icard, pcard.VariantData);
                card_ui.SetCard(club_card);

                //string cdesc = icard.GetDesc();
                //string adesc = icard.GetAbilitiesDesc();
                //if (!string.IsNullOrWhiteSpace(cdesc))
                //    this.desc.text = cdesc + "\n\n" + adesc;
                //else
                //    this.desc.text = adesc;

                //Abilities
                int index = 0;
                foreach (AbilityData ability in club_card.GetAbilities())
                {
                    if (index < status_lines.Length)
                    {
                        if (!string.IsNullOrWhiteSpace(ability.desc))
                        //Dont display default ability (GetAbilitiesDesc does that already)
                        //if (!pcard.CardData.HasAbility(ability) && !string.IsNullOrWhiteSpace(ability.desc))
                        {
                            status_lines[index].SetLine(club_card.CardData, ability);
                            index++;
                        }
                    }
                }

                //Status
                foreach (CardStatus status in club_card.GetAllStatus())
                {
                    if (index < status_lines.Length)
                    {
                        StatusData istatus = StatusData.Get(status.type);
                        if (istatus != null && !string.IsNullOrWhiteSpace(istatus.desc))
                        {
                            int ival = Mathf.Max(status.value, Mathf.CeilToInt(status.duration / 2f));
                            status_lines[index].SetLine(istatus, ival);
                            index++;
                        }
                    }
                }
            }

        }
    }
}
