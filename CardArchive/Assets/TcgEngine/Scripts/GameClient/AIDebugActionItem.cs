#if UNITY_EDITOR
using UnityEngine;
using TMPro;

namespace TcgEngine.Client
{
    /// <summary>
    /// EDITOR-ONLY. One row in the AI debug action ranking list (rank / score / description).
    /// Wire the TMP texts by hand; AIDebugPanel fills them via SetData().
    /// The whole file is wrapped in #if UNITY_EDITOR so it is stripped from regular builds.
    /// </summary>
    public class AIDebugActionItem : MonoBehaviour
    {
        public TMP_Text rank_text;   //e.g. "1"
        public TMP_Text score_text;  //minimax score
        public TMP_Text desc_text;   //human-readable action description

        public void SetData(int rank, int score, string desc)
        {
            gameObject.SetActive(true);
            if (rank_text != null)
                rank_text.text = rank.ToString();
            if (score_text != null)
                score_text.text = score.ToString();
            if (desc_text != null)
                desc_text.text = desc;
        }

        public void Clear()
        {
            gameObject.SetActive(false);
        }
    }
}
#endif
