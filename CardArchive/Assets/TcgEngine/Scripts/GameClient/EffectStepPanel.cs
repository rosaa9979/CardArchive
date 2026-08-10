#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TcgEngine.Client
{
    /// <summary>
    /// EDITOR-ONLY 치트 패널: 효과 처리 순서 시각화.
    ///
    /// 토글을 켜면 효과가 **한 항목씩** 멈춘다 (어빌리티 1개 / 유발 공격 1개 / 사망 처리 1회 /
    /// 공격 마이크로스텝 1개 / 턴 진행 콜백 1개 = ResolveQueue가 한 번에 소진하는 단위).
    /// 다음 버튼을 눌러야 다음 항목으로 넘어가고, 상단에 지금 대기 중인 효과 목록이 뜬다.
    /// 직전 스텝에서 실제로 실행된 EffectData 목록도 함께 보여준다.
    ///
    /// 파일 전체가 #if UNITY_EDITOR라 빌드에서 통째로 사라진다. 실제 게이트/훅은
    /// TcgEngine.EffectStepDebug + ResolveQueue 쪽에 있고, 그쪽도 전부 에디터 전용이다.
    ///
    /// 씬/UI 배선은 AIDebugPanel과 같은 방식으로 손으로 한다:
    ///  - 이 컴포넌트는 항상 켜져 있는 오브젝트(예: 캔버스 루트)에 붙인다
    ///  - canvas_group  : 패널 내용. 켜짐/꺼짐만 토글한다 (없어도 동작)
    ///  - toggle_button : 치트 on/off 버튼 → OnClickToggle()   (canvas_group 바깥에 두 것)
    ///  - next_button   : 다음 효과 버튼   → OnClickNext()
    ///  - items         : 대기 목록 행들 (EffectStepItem 하나당 한 줄, 리스트 길이 = 표시 상한)
    ///  - empty_text    : 대기 항목이 없을 때만 켜지는 안내 문구
    ///  - effect_text   : 직전 스텝에서 실행된 EffectData 목록 (단일 텍스트)
    ///  - status_text   : 상태 한 줄
    ///  - 모든 참조는 선택 사항이다. 키보드(F9/F10)만으로도 쓸 수 있다.
    ///
    /// 주의: 로컬(솔로) 게임 전용이다. ResolveQueue는 서버 측 객체라 온라인 매치에서는
    /// 클라이언트에 없고, 그때는 "게임 없음"으로만 표시된다.
    /// </summary>
    public class EffectStepPanel : MonoBehaviour
    {
        [Header("UI refs (set up by hand)")]
        public CanvasGroup canvas_group;      //패널 내용 (없어도 됨)
        public Button toggle_button;          //선택: 코드로 리스너를 붙여준다
        public Button next_button;            //선택: 코드로 리스너를 붙여준다
        public List<EffectStepItem> items = new List<EffectStepItem>(); //대기 목록 행들 (개수 = 표시 상한)
        public TMP_Text empty_text;           //선택: 대기 항목이 없을 때만 켜지는 안내 문구
        public TMP_Text effect_text;          //직전 스텝의 EffectData 목록
        public TMP_Text status_text;          //상태 한 줄
        public TMP_Text toggle_label;         //선택: 토글 버튼 라벨

        [Header("Settings")]
        public bool start_hidden = true;      //Awake에서 패널을 숨긴다
        public float refresh_interval = 0.1f; //목록 갱신 주기 (초)
        public KeyCode toggle_key = KeyCode.F9;
        public KeyCode step_key = KeyCode.F10;

        private readonly StringBuilder sb = new StringBuilder();
        private float refresh_timer;
        private bool visible;

        void Awake()
        {
            //치트는 항상 꺼진 상태에서 시작한다 (도메인 리로드를 꺼둔 경우의 잔존 상태 방지).
            EffectStepDebug.SetEnabled(false);

            if (toggle_button != null)
                toggle_button.onClick.AddListener(OnClickToggle);
            if (next_button != null)
                next_button.onClick.AddListener(OnClickNext);

            visible = !start_hidden;
            SetGroupVisible(visible);
            Refresh();
        }

        void OnDestroy()
        {
            //패널이 사라지면 치트도 반드시 풀린다 — 멈춰 있던 큐를 다시 굴려준다.
            EffectStepDebug.SetEnabled(false);
        }

        void OnDisable()
        {
            EffectStepDebug.SetEnabled(false);
            SetToggleLabel();
        }

        void Update()
        {
            if (Input.GetKeyDown(toggle_key))
                OnClickToggle();
            if (Input.GetKeyDown(step_key))
                OnClickNext();

            refresh_timer -= Time.unscaledDeltaTime;
            if (refresh_timer <= 0f)
            {
                refresh_timer = Mathf.Max(0.02f, refresh_interval);
                Refresh();
            }
        }

        //--- Button hooks ---------------------------------------------------

        public void OnClickToggle()
        {
            bool on = !EffectStepDebug.IsEnabled;
            EffectStepDebug.SetEnabled(on);

            //치트를 켜면 패널을 같이 띄운다. 끌 때는 패널을 남겨둔다 (마지막 결과 확인용).
            if (on && !visible)
            {
                visible = true;
                SetGroupVisible(true);
            }
            Refresh();
        }

        public void OnClickNext()
        {
            EffectStepDebug.Step();
            Refresh();
        }

        public void ShowPanel()
        {
            visible = true;
            SetGroupVisible(true);
        }

        public void ClosePanel()
        {
            visible = false;
            SetGroupVisible(false);
        }

        //--- Display --------------------------------------------------------

        private void Refresh()
        {
            SetToggleLabel();

            //패널이 닫혀 있으면 큐를 훑지 않는다 (문자열 생성 비용 0).
            if (!visible)
                return;

            if (status_text != null)
                status_text.text = EffectStepDebug.GetStatusText();

            if (next_button != null)
                next_button.interactable = EffectStepDebug.IsEnabled && EffectStepDebug.HasQueue;

            RefreshItems();

            if (effect_text != null)
            {
                List<string> log = EffectStepDebug.GetEffectLog();
                if (!EffectStepDebug.IsEnabled)
                {
                    effect_text.text = "(치트를 켜면 기록됩니다)";
                }
                else if (log.Count == 0)
                {
                    effect_text.text = "(직전 스텝에서 실행된 EffectData 없음)";
                }
                else
                {
                    sb.Length = 0;
                    for (int i = 0; i < log.Count; i++)
                    {
                        if (i > 0)
                            sb.Append('\n');
                        sb.Append("• ").Append(log[i]);
                    }
                    effect_text.text = sb.ToString();
                }
            }
        }

        //대기 목록 행 채우기. 1번 행이 "다음에 처리될 항목"이다.
        private void RefreshItems()
        {
            if (items.Count == 0)
                return;

            int count = 0;
            if (EffectStepDebug.HasQueue)
            {
                EffectStepDebug.PendingList pending = EffectStepDebug.GetPending(items.Count);
                count = pending.Count;
                for (int i = 0; i < count; i++)
                {
                    EffectStepItem item = items[i];
                    if (item != null)
                        item.SetData(i + 1, pending[i], i == 0);
                }
            }

            for (int i = count; i < items.Count; i++)
            {
                if (items[i] != null)
                    items[i].Clear();
            }

            if (empty_text != null)
            {
                empty_text.gameObject.SetActive(count == 0);
                if (count == 0)
                    empty_text.text = EffectStepDebug.HasQueue ? "(대기 중인 효과 없음)" : "(게임 진행 중이 아닙니다)";
            }
        }

        private void SetToggleLabel()
        {
            if (toggle_label != null)
                toggle_label.text = EffectStepDebug.IsEnabled ? "효과 스텝: ON" : "효과 스텝: OFF";
        }

        private void SetGroupVisible(bool show)
        {
            if (canvas_group == null)
                return;
            canvas_group.alpha = show ? 1f : 0f;
            canvas_group.interactable = show;
            canvas_group.blocksRaycasts = show;
        }
    }
}
#endif
