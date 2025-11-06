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
    /// Focus된 패를 보는 기능 (RectTransform 기준으로 리팩토링됨)
    /// </summary>

    public class HandCardPreviewUI : MonoBehaviour
    {
        public UIPanel ui_panel;
        public GameObject card_outline;
        public GameObject card_ui;
        public GameObject status_ui;
        private HandCard current_focus;

        public RectTransform status_row;
        public StatusLine[] status_lines;

        private CardUI cardui;

        // --- RectTransform 관련 변수 ---
        private RectTransform card_ui_rect;
        private RectTransform status_ui_rect;
        private RectTransform parent_canvas_rect; // 최상위 캔버스의 RectTransform
        private Camera main_camera;              // 월드 카드를 비추는 메인 카메라
        private Camera canvas_camera;            // 캔버스 모드에 따라 UI 좌표 변환에 사용할 카메라

        private float card_ui_target_y;     // card_ui의 고정될 Y축 anchoredPosition
        private float status_ui_target_y;   // status_ui의 고정될 Y축 anchoredPosition
        private float status_card_x_offset; // card_ui와 status_ui 사이의 X축 간격
        private float animation_y_offset = 100f; // 애니메이션 Y축 오프셋 (캔버스 단위)

        [Header("Default Setting")]
        public ConditionWideAreaRange default_wide_area_range;

        void Start()
        {
            // RectTransform 컴포넌트 가져오기
            card_ui_rect = card_ui.GetComponent<RectTransform>();
            status_ui_rect = status_ui.GetComponent<RectTransform>();
            
            // 캔버스 및 카메라 설정
            Canvas parent_canvas = card_ui.GetComponentInParent<Canvas>();
            parent_canvas_rect = parent_canvas.GetComponent<RectTransform>();
            main_camera = Camera.main;

            // 캔버스 렌더 모드에 따라 좌표 변환에 사용할 카메라 결정
            // (Screen Space - Overlay의 경우 null, 그 외에는 카메라 필요)
            if (parent_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                canvas_camera = null;
            else
                canvas_camera = main_camera; // ScreenSpaceCamera 또는 WorldSpace인 경우
                                             // (만약 UI 전용 카메라가 따로 있다면 parent_canvas.worldCamera를 사용해야 할 수도 있음)

            cardui = card_ui.GetComponentInChildren<CardUI>();

            // UI가 에디터에서 배치된 Y값을 기준으로 고정 Y값 저장 (anchoredPosition 기준)
            card_ui_target_y = card_ui_rect.anchoredPosition.y;
            status_ui_target_y = status_ui_rect.anchoredPosition.y;

            // card_ui와 status_ui 간의 X축 간격(offset)을 캔버스 기준으로 저장
            status_card_x_offset = status_ui_rect.anchoredPosition.x - card_ui_rect.anchoredPosition.x;

            // 기존 card_ui_y_offset (0.5f)는 월드 유닛이었으므로, 캔버스 유닛에 맞는 새 값(예: 100)으로 설정합니다.
            // 이 값은 필요시 인스펙터에서 public 변수로 빼서 조절하세요.
            // animation_y_offset = 100f; // (클래스 멤버 변수로 이동시킴)
        }

        void Update()
        {
            if (!GameClient.Get().IsReady())
                return;

            foreach (StatusLine line in status_lines)
                line.Hide();

            Game game_data = GameClient.Get().GetGameData();
            HandCard hcard = HandCard.GetFocus();

            bool visible = false;
            if (hcard != null && HandCard.GetDrag() == null)
            {
                visible = true;

                if (hcard != current_focus)
                {
                    current_focus = hcard;
                    SetCard(); // 애니메이션 시작
                }

                // --- UI 위치 계산 (RectTransform 기준) ---

                // 1. 포커스된 카드의 월드 좌표를 스크린 좌표로 변환
                Vector2 screen_point = main_camera.WorldToScreenPoint(current_focus.transform.position);
                
                // 2. 스크린 좌표를 캔버스(parent_canvas_rect) 기준의 로컬 좌표(anchoredPosition)로 변환
                Vector2 canvas_pos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent_canvas_rect, 
                    screen_point, 
                    canvas_camera, // Start에서 설정한 캔버스 모드에 맞는 카메라
                    out canvas_pos
                );
                
                float target_x = canvas_pos.x;

                // 3. card_ui의 X좌표 업데이트
                // Y 좌표는 애니메이션 중일 수 있으므로 현재 Y 값을 유지
                card_ui_rect.anchoredPosition = new Vector2(
                    target_x,
                    card_ui_rect.anchoredPosition.y
                );
                
                // 4. status_ui의 위치 설정 (card_ui 기준 offset 적용, Y는 고정)
                status_ui_rect.anchoredPosition = new Vector2(
                    target_x + status_card_x_offset,
                    status_ui_target_y
                );

                // --- (이하 능력/상태 표시 로직은 기존과 동일) ---
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
                
                bool is_outline_enabled = GameClient.Get().IsYourTurn() && game_data.phase == GamePhase.Main && game_data.CanPlay(hcard.GetCard());
                card_outline.SetActive(is_outline_enabled); 
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
            card_ui_rect.DOKill(false); // RectTransform의 트윈 중단

            cardui.SetCard(current_focus.GetCard());

            // X 좌표 계산 (Update와 동일 로직)
            Vector2 screen_point = main_camera.WorldToScreenPoint(current_focus.transform.position);
            Vector2 canvas_pos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent_canvas_rect, 
                screen_point, 
                canvas_camera, 
                out canvas_pos
            );
            float target_x = canvas_pos.x;
            
            // 최종 위치 (X는 계산값, Y는 저장된 고정값)
            Vector2 final_pos = new Vector2(target_x, card_ui_target_y);
            
            // 시작 위치 (Y만 오프셋 적용)
            Vector2 start_pos = final_pos;
            start_pos.y -= animation_y_offset; // 캔버스 단위 오프셋

            // card_ui 위치 설정 및 애니메이션 (anchoredPosition 기준)
            card_ui_rect.anchoredPosition = start_pos;
            card_ui_rect.DOAnchorPos(final_pos, 0.2f).SetEase(Ease.OutExpo);
        }
    }
}