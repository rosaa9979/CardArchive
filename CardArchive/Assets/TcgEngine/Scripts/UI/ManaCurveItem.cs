using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TcgEngine.UI
{
    public class ManaCurveItem : MonoBehaviour
    {
        [SerializeField] private int value;
        [SerializeField] private Text bar_text;
        [SerializeField] private Image bar;

        public void SetHeightRatio(float max_height, float ratio)
        {
            float new_height = max_height * ratio;

            if (bar != null)
            {
                Vector2 size = bar.rectTransform.sizeDelta;
                size.y = new_height;
                bar.rectTransform.sizeDelta = size;
            }
        }

        public void SetText(string text)
        {
            if (bar_text != null)
                bar_text.text = text;
        }

        public int GetValue()
        {
            return value;
        }
    }
}

