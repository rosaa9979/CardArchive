using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using TcgEngine;
using System.Diagnostics.SymbolStore;
using DG.Tweening;

namespace TcgEngine.UI
{
    /// <summary>
    /// Focus된 패를 보는 기능
    /// </summary>

    public class HandCardPreviewUI : MonoBehaviour
    {
        public UIPanel ui_panel;
        public CardUI card_ui;
        private HandCard current_focus;

        public RectTransform ui_row;
        public RectTransform status_row;
        public StatusLine[] status_lines;

        private Vector2 ui_start_pos;
        private Vector2 status_start_pos;

        void Start()
        {
            ui_start_pos = ui_row.anchoredPosition;
            status_start_pos = status_row.transform.position;
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

                Vector3 position = new Vector3(current_focus.gameObject.transform.position.x, card_ui.gameObject.transform.position.y, card_ui.gameObject.transform.position.z);
                card_ui.gameObject.transform.position = position;
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
