#if UNITY_EDITOR
using UnityEngine;
using TMPro;

namespace TcgEngine.Client
{
    /// <summary>A prefab-backed queue card, coloured by the caster's actual owner.</summary>
    public class EffectStepItem : MonoBehaviour
    {
        public TMP_Text index_text;
        public TMP_Text tag_text;
        public TMP_Text desc_text;
        public TMP_Text source_text;
        public TMP_Text owner_text;
        public GameObject next_marker;
        public UnityEngine.UI.Image background;
        public UnityEngine.UI.Image border;
        public float indent_step = 16f;
        public Color normal_color = new Color32(22, 31, 47, 255);
        public Color next_color = new Color32(36, 49, 69, 255);

        public void SetData(int index, EffectStepDebug.PendingEntry entry, bool is_next)
        {
            gameObject.SetActive(true);
            int local = GameClient.Get() != null ? GameClient.Get().GetPlayerID() : 0;
            bool system = entry.owner_id < 0;
            bool mine = entry.owner_id == local;
            Color side = system ? new Color32(147, 164, 187, 255)
                : mine ? new Color32(78, 167, 255, 255) : new Color32(255, 103, 123, 255);
            if (index_text != null) index_text.text = index.ToString("00");
            if (tag_text != null) tag_text.text = entry.tag + (entry.depth > 0 ? " · 연쇄 깊이 " + entry.depth : "");
            if (source_text != null) source_text.text = string.IsNullOrEmpty(entry.source) ? "게임 진행" : entry.source;
            if (desc_text != null) desc_text.text = entry.title;
            if (owner_text != null)
            {
                owner_text.text = system ? "공통 처리" : mine ? "내 효과" : "상대 효과";
                owner_text.color = side;
            }
            if (border != null) border.color = side;
            if (next_marker != null) next_marker.SetActive(is_next);
            if (background != null) background.color = is_next ? next_color : normal_color;
        }

        public void Clear() { gameObject.SetActive(false); }
    }
}
#endif