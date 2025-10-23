using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using TcgEngine;
using System.Diagnostics.SymbolStore;
using DG.Tweening;
using System;

namespace TcgEngine.UI
{
    /// <summary>
    /// Focus된 패를 보는 기능
    /// </summary>

    public class HandCardPreviewUI : MonoBehaviour
    {
        public UIPanel ui_panel;
        public CardUI card_ui;
        public GameObject status_ui;
        private HandCard current_focus;

        public RectTransform ui_row;
        public RectTransform status_row;
        public StatusLine[] status_lines;

        private Vector2 ui_start_pos;
        private Vector2 status_start_pos;
        private float offset;

        [Header("Default Setting")]
        public ConditionWideAreaRange default_wide_area_range;

        void Start()
        {
            ui_start_pos = ui_row.anchoredPosition;
            status_start_pos = status_row.anchoredPosition;
            offset = Math.Abs(ui_row.gameObject.transform.position.x - status_row.gameObject.transform.position.x);
        }

        void Update()
        {
            HandCard hcard = HandCard.GetFocus();

            bool visible = false;

            if (hcard != null)
            {
                visible = true;

                if (hcard != current_focus)
                {
                    current_focus = hcard;

                    SetCard();
                }

                Vector3 ui_position = new Vector3(current_focus.gameObject.transform.position.x, card_ui.gameObject.transform.position.y, card_ui.gameObject.transform.position.z);
                card_ui.gameObject.transform.position = ui_position;

                Vector3 status_position = new Vector3(current_focus.gameObject.transform.position.x + offset, status_ui.gameObject.transform.position.y, status_ui.gameObject.transform.position.z);
                status_ui.gameObject.transform.position = status_position;

                Card pcard = hcard.GetCard();

                int index = 0;
                foreach (AbilityData ability in pcard.GetAbilities())
                {
                    if (index < status_lines.Length)
                    {
                        if (ability.condition_wide_range != default_wide_area_range)
                        //Dont display default ability (GetAbilitiesDesc does that already)
                        //if (!pcard.CardData.HasAbility(ability) && !string.IsNullOrWhiteSpace(ability.desc))
                        {
                            status_lines[index].SetLine(pcard.CardData, ability.condition_wide_range.thumnail);
                            index++;
                        }
                    }
                }

                foreach (AbilityData ability in pcard.GetAbilities())
                {
                    if (index < status_lines.Length)
                    {
                        if (!string.IsNullOrWhiteSpace(ability.desc))
                        //Dont display default ability (GetAbilitiesDesc does that already)
                        //if (!pcard.CardData.HasAbility(ability) && !string.IsNullOrWhiteSpace(ability.desc))
                        {
                            status_lines[index].SetLine(pcard.CardData, ability);
                            index++;
                        }
                    }
                }

                //Status
                foreach (CardStatus status in pcard.GetAllStatus())
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

            else
                current_focus = null;



            if (visible)
                ui_panel.Show(true);
            else
                ui_panel.Hide(true);
        }
        
        public void SetCard()
        {
            ui_row.DOKill(false);

            card_ui.SetCard(current_focus.GetCard());

            Vector2 final_pos = ui_start_pos;
            Vector2 start_pos = final_pos;
            start_pos.y -= 25.0f;

            ui_row.anchoredPosition = start_pos;
            ui_row.DOAnchorPos(final_pos, 0.5f).SetEase(Ease.OutExpo);
        }
    }
}
