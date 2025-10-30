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
        public GameObject card_ui;
        public GameObject status_ui;
        private HandCard current_focus;

        public RectTransform status_row;
        public StatusLine[] status_lines;

        private CardUI cardui;
        private Vector3 card_ui_start_pos;  // Vector2 대신 Vector3 사용 (world position)
        private Vector2 status_start_pos;
        private float offset;
        private float card_ui_y_offset = 0.5f;  // 애니메이션 offset 상수화

        [Header("Default Setting")]
        public ConditionWideAreaRange default_wide_area_range;

        void Start()
        {
            card_ui_start_pos = card_ui.gameObject.transform.position;
            status_start_pos = status_row.anchoredPosition;

            cardui = card_ui.GetComponentInChildren<CardUI>();
            offset = Math.Abs(card_ui.gameObject.transform.position.x - status_row.gameObject.transform.position.x);
        }

        void Update()
        {
            if (!GameClient.Get().IsReady())
                return;

            foreach (StatusLine line in status_lines)
                line.Hide();

            HandCard hcard = HandCard.GetFocus();

            bool visible = false;
            if (hcard != null && HandCard.GetDrag() == null)
            {
                visible = true;

                if (hcard != current_focus)
                {
                    current_focus = hcard;
                    SetCard();
                }

                Vector3 ui_position = new Vector3(
                    current_focus.gameObject.transform.position.x, 
                    card_ui_start_pos.y,
                    card_ui_start_pos.z   
                );
                
                if (!DOTween.IsTweening(card_ui.transform))
                {
                    card_ui.gameObject.transform.position = new Vector3(
                        ui_position.x,
                        card_ui.gameObject.transform.position.y,
                        ui_position.z
                    );
                }
                else
                {
                    Vector3 current_pos = card_ui.gameObject.transform.position;
                    card_ui.gameObject.transform.position = new Vector3(
                        ui_position.x,
                        current_pos.y,
                        ui_position.z
                    );
                }

                Vector3 status_position = new Vector3(
                    current_focus.gameObject.transform.position.x + offset, 
                    status_ui.gameObject.transform.position.y, 
                    status_ui.gameObject.transform.position.z
                );
                status_ui.gameObject.transform.position = status_position;

                Card pcard = hcard.GetCard();

                int index = 0;
                foreach (AbilityData ability in pcard.GetAbilities())
                {
                    if (index < status_lines.Length)
                    {
                        if (ability.condition_wide_range != default_wide_area_range)
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
                        {
                            status_lines[index].SetLine(pcard.CardData, ability);
                            index++;
                        }
                    }
                }

                // Status
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
            {
                current_focus = null;
            }

            if (visible)
                ui_panel.Show(true);
            else
                ui_panel.Hide(true);
        }
        
        public void SetCard()
        {
            card_ui.transform.DOKill(false);

            cardui.SetCard(current_focus.GetCard());

            float target_x = current_focus.gameObject.transform.position.x;
            
            Vector3 final_pos = new Vector3(target_x, card_ui_start_pos.y, card_ui_start_pos.z);
            
            Vector3 start_pos = final_pos;
            start_pos.y -= card_ui_y_offset;

            // card_ui 위치 설정 및 애니메이션
            card_ui.gameObject.transform.position = start_pos;
            card_ui.transform.DOMove(final_pos, 0.2f).SetEase(Ease.OutExpo);
        }
    }
}