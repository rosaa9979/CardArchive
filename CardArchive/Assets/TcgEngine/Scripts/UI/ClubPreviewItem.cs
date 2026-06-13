using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace TcgEngine.UI
{
    public class ClubPreviewItem : MonoBehaviour
    {
        public bool is_opponent;
        private CanvasGroup canvas_group;
        public CardUI card_ui;

        [Header("Main UI")]
        public RectTransform[] ui_rects;
        [Header("Status UI")]
        public StatusLine[] status_lines;

        private Dictionary<RectTransform, Vector3> end_positions = new Dictionary<RectTransform, Vector3>();

        private static List<ClubPreviewItem> preview_item_list = new List<ClubPreviewItem>();

        private void Awake()
        {
            preview_item_list.Add(this);            
        }

        private void Start()
        {
            canvas_group = GetComponent<CanvasGroup>();


            foreach (RectTransform ui_rect in ui_rects)
            {
                end_positions[ui_rect] = ui_rect.anchoredPosition;
            }
            canvas_group.alpha = 0f;
        }

        public void Show(Card club)
        {
            if (club == null)
                return;

            foreach (RectTransform ui_rect in ui_rects)
            {
                ui_rect.DOKill(complete: false);
            }

            //패널을 먼저 보이게 한 뒤 내용을 채운다.
            //SetClubInfo(CardUI.SetCard) 중 예외가 나도 패널 자체는 떠 있도록.
            canvas_group.alpha = 1.0f;

            SetClubInfo(club);

            float display_duration = ClubPreviewUI.Get().display_duration;

            foreach (RectTransform ui_rect in ui_rects)
            {
                Vector3 end_position = end_positions[ui_rect];
                Vector3 start_position = end_position;
                start_position.x = is_opponent ? start_position.x + ClubPreviewUI.Get().side_offset : start_position.x - ClubPreviewUI.Get().side_offset;
                
                ui_rect.anchoredPosition = start_position;
                ui_rect.DOAnchorPos3DX(end_position.x, display_duration).SetEase(Ease.OutCubic);
            }
        }

        public void Hide()
        {
            canvas_group.alpha = 0f;
        }
        
        private void SetClubInfo(Card club)
        {
            if (card_ui != null)
                card_ui.SetCard(club);
        }

        public static ClubPreviewItem Get(bool opponent)
        {
            foreach (ClubPreviewItem ui in preview_item_list)
            {
                if (ui.is_opponent == opponent)
                    return ui;
            }
            return null;
        }
        
        public static List<ClubPreviewItem> Get()
        {
            return preview_item_list;
        }
    }
}