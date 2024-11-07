using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TcgEngine.Client
{
    /// <summary>
    /// Same as HandCard, but simpler version for the opponent's cards
    /// </summary>

    public class HandCardBack : MonoBehaviour
    {
        public Image card_sprite;

        private RectTransform rect;

        private static List<HandCardBack> card_list = new List<HandCardBack>();

        void Awake()
        {
            card_list.Add(this);
            rect = GetComponent<RectTransform>();
            SetCardback(null);
        }

        void Update()
        {
            if (HandCard.hide == true)
                SetOpacity(0f);
            else
                SetOpacity(1f);
        }

        private void OnDestroy()
        {
            card_list.Remove(this);
        }

        public void SetCardback(CardbackData cb)
        {
            if (cb != null && cb.cardback != null)
                card_sprite.sprite = cb.cardback;
        }

        public RectTransform GetRect()
        {
            if (rect == null)
                return GetComponent<RectTransform>();
            return rect;
        }

        private void SetOpacity(float opacity)
        {
            if (card_sprite != null)
                card_sprite.color = new Color(card_sprite.color.r, card_sprite.color.g, card_sprite.color.b, opacity);
        }
    }
}
