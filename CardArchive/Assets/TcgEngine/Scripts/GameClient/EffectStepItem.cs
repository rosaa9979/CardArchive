#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TcgEngine.Client
{
    /// <summary>
    /// EDITOR-ONLY. 효과 처리 대기 목록의 한 행 (순번 / 태그 / 설명).
    /// TMP 참조는 손으로 배선하고, EffectStepPanel이 SetData()로 채운다.
    /// AIDebugActionItem과 같은 패턴이다.
    ///
    /// - depth(Phase 트리 깊이)만큼 desc_text의 좌측 여백이 들어가 연쇄 구조가 눈에 보인다.
    /// - 다음에 처리될 항목(index 1)은 next_marker / 배경색으로 강조된다.
    /// 파일 전체가 #if UNITY_EDITOR라 빌드에서 제외된다.
    /// </summary>
    public class EffectStepItem : MonoBehaviour
    {
        [Header("UI refs (set up by hand)")]
        public TMP_Text index_text;      //"1"
        public TMP_Text tag_text;        //"[유발]"
        public TMP_Text desc_text;       //"전투의 함성  ← 고블린"
        public GameObject next_marker;   //선택: 다음 처리 항목에만 켜지는 화살표 등
        public Image background;         //선택: 다음 처리 항목 강조용

        [Header("Settings")]
        public float indent_step = 16f;              //depth 1당 들여쓰기 픽셀
        public Color normal_color = new Color(0f, 0f, 0f, 0f);
        public Color next_color = new Color(1f, 0.85f, 0.3f, 0.25f);

        public void SetData(int index, EffectStepDebug.PendingEntry entry, bool is_next)
        {
            gameObject.SetActive(true);

            if (index_text != null)
                index_text.text = index.ToString();

            if (tag_text != null)
                tag_text.text = string.IsNullOrEmpty(entry.tag) ? "" : "[" + entry.tag + "]";

            if (desc_text != null)
            {
                desc_text.text = string.IsNullOrEmpty(entry.source)
                    ? entry.title
                    : entry.title + "  ← " + entry.source;
                //Phase 깊이만큼 좌측 여백. 레이아웃 구성에 의존하지 않도록 TMP margin으로 준다.
                Vector4 m = desc_text.margin;
                m.x = entry.depth * indent_step;
                desc_text.margin = m;
            }

            if (next_marker != null)
                next_marker.SetActive(is_next);
            if (background != null)
                background.color = is_next ? next_color : normal_color;
        }

        public void Clear()
        {
            gameObject.SetActive(false);
        }
    }
}
#endif
