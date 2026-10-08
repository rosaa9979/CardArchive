#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace TcgEngine.Client
{
    /// <summary>Editor-only ranked action row; hover for the report, scroll for additional pages.</summary>
    public class AIDebugActionItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IScrollHandler
    {
        public TMP_Text rank_text;
        public TMP_Text score_text;
        public TMP_Text desc_text;

        private AIDebugPanel owner;
        private string details;
        private UnityEngine.UI.Image background;
        private Color restingColor;
        public string Summary { get; private set; }

        void Awake()
        {
            background = GetComponent<UnityEngine.UI.Image>();
            if (background == null)
                background = gameObject.AddComponent<UnityEngine.UI.Image>();
            background.raycastTarget = true;
            ConfigureText(rank_text, TextAlignmentOptions.Center);
            ConfigureText(score_text, TextAlignmentOptions.MidlineRight);
            ConfigureText(desc_text, TextAlignmentOptions.MidlineLeft);
        }

        private void ConfigureText(TMP_Text text, TextAlignmentOptions alignment)
        {
            if (text == null)
                return;
            text.alignment = alignment;
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = 26f;
            text.color = new Color32(230, 237, 247, 255);
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.Normal;
        }

        public void SetData(int rank, int score, string desc, string details, AIDebugPanel owner, int bestScore)
        {
            gameObject.SetActive(true);
            this.details = details;
            this.owner = owner;
            bool best = score == bestScore;
            restingColor = best ? new Color32(29, 67, 78, 255)
                : rank % 2 == 0 ? new Color32(27, 36, 53, 255) : new Color32(21, 29, 44, 255);
            if (background != null)
                background.color = restingColor;
            string difference = best ? "최고 점수" : "최고점 대비 −" + ((long)bestScore - score).ToString("N0");
            Summary = "<b>" + rank + "위 · " + AIDebugPanel.EscapeText(desc) + "</b>\n"
                + AIDebugPanel.ScoreText(score) + "  <color=#9CAEC5>" + difference + "</color>";
            if (rank_text != null)
            {
                rank_text.text = rank.ToString("00");
                rank_text.color = best ? new Color32(105, 224, 205, 255) : new Color32(156, 174, 197, 255);
            }
            if (score_text != null)
                score_text.text = "<b>" + AIDebugPanel.ScoreText(score) + "</b>";
            if (desc_text != null)
                desc_text.text = AIDebugPanel.EscapeText(desc);
        }

        public void Clear()
        {
            details = null;
            Summary = null;
            gameObject.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (background != null)
                background.color = new Color32(44, 64, 87, 255);
            if (owner != null)
                owner.ShowDetail(this, details);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (background != null)
                background.color = restingColor;
            if (owner != null)
                owner.HideDetail(this);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (owner != null)
                owner.ScrollDetail(eventData.scrollDelta.y);
        }
    }
}
#endif
