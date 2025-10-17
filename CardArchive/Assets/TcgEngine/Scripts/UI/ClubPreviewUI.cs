using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using TcgEngine;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using DG.Tweening;


namespace TcgEngine.UI
{
    /// <summary>
    /// In the game scene, the ClubPreviewUI is what shows the Club in big with extra info when hovering a club icon
    /// </summary>

    public class ClubPreviewUI : MonoBehaviour
    {
        [Header("Display Setting")]
        public CanvasGroup canvas_group;
        public CardUI card_ui;
        public float hover_delay = 0.0f;
        public float hover_delay_mobile = 0.1f;
        public float display_duration;
        public float screen_width_ratio = 0.2f;

        [Header("Main UI")]
        public RectTransform ui_rect;
        [Header("Status UI")]
        public StatusLine[] status_lines;

        private float side_offset;
        private Vector3 ui_start_pos;

        private static ClubPreviewUI _instance;

        private void Awake()
        {
            _instance = this;
        }

        private void Start()
        {
            canvas_group.alpha = 0.0f;
            ui_start_pos = ui_rect.anchoredPosition;
        }

        public void SetInfo(Card club)
        {
            card_ui.SetCard(club);
        }

        public void Show(Card club)
        {
            ui_rect.DOKill(complete: false);
            SetInfo(club);

            // 화면 너비의 비율로 offset 계산
             
            side_offset = Screen.width * screen_width_ratio;

            Vector3 end_position = ui_start_pos;
            Vector3 start_position = end_position;
            start_position.x -= side_offset;

            ui_rect.anchoredPosition = start_position;
            canvas_group.alpha = 1.0f;
            ui_rect.DOAnchorPos3DX(end_position.x, display_duration).SetEase(Ease.OutCubic);
        }

        public void Hide()
        {
            canvas_group.alpha = 0.0f;
        }

        public static ClubPreviewUI Get()
        {
            return _instance;
        }
    }
}
