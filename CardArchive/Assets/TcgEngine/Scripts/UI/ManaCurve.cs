using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace TcgEngine.UI
{
    public class ManaCurve : MonoBehaviour
    {
        [Header("Mana Curve")]
        [SerializeField] private RectTransform mana_graph;
        [SerializeField] private ManaCurveItem[] mana_bars;
        private Dictionary<int, int> mana_distribution;

        void Start()
        {
            mana_distribution = new Dictionary<int, int>();
        }

        void Update()
        {

        }

        public void Refresh(List<UserCardData> deck_info)
        {
            if (deck_info == null)
                return;

            mana_distribution.Clear();
            
            foreach (UserCardData utid in deck_info)
            {
                CardData ucard = CardData.Get(utid.tid);
                int mana = ucard.mana < 10 ? ucard.mana : 10;
                if (mana_distribution.ContainsKey(mana))
                    mana_distribution[mana] += utid.quantity;
                else
                    mana_distribution[mana] = utid.quantity;
            }

            DrawManaCurve();
        }

        public void DrawManaCurve()
        {
            int maxValue = -1;
            foreach (var pair in mana_distribution)
            {
                if (pair.Value > maxValue)
                    maxValue = pair.Value;
            }
            float max_height = mana_graph.rect.height * 0.8f;
            float min_height = mana_graph.rect.height * 0.05f;
            foreach (ManaCurveItem mana_bar in mana_bars)
            {
                int mana = mana_bar.GetValue();
                int quantity = mana_distribution.ContainsKey(mana) ? mana_distribution[mana] : 0;
                float ratio = maxValue > 0 ? (float)quantity / maxValue : 0f;

                mana_bar.SetHeightRatio(max_height, min_height, ratio);
                mana_bar.SetText(quantity.ToString());
            }
        }
    }
}
