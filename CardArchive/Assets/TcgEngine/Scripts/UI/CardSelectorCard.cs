using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine;

namespace TcgEngine.UI
{
    /// <summary>
    /// One card in the CardSelector
    /// </summary>

    public class CardSelectorCard : MonoBehaviour
    {
        public CardUI card_ui;
        public bool selectable = false;
        public Text selection_text;
        public Image selection_icon;
        public Sprite allow_icon;
        public Sprite deny_icon;

        [HideInInspector] 
        public bool selected = false;

        private int index;
        private Vector2 target_pos;
        private Vector3 target_scale;

        private Card card;

        private RectTransform rect;

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
        }

        private void Start()
        {
            transform.localScale = target_scale;
        }

        private void Update()
        {
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, target_pos, 5f * Time.deltaTime);
            transform.localScale = Vector2.Lerp(transform.localScale, target_scale, 2f * Time.deltaTime);
        }

        public void OnClick()
        {
            if (selectable)
            {
                Color color = selection_icon.color;
                selected = !selected;
                //selection_icon.gameObject.SetActive(selected);
                if (selected)
                    color.a = 1f;
                else
                    color.a = 0f;
                selection_icon.color = color;
                selection_text.gameObject.SetActive(selected);
                //card_ui.GetComponent<CanvasGroup>().alpha = selected ? 0.5f : 1f;
            }
        }

        public void SetCard(Card card)
        {
            this.card = card;
            CardData icard = CardData.Get(card.card_id);
            card_ui.SetCard(icard, card.VariantData);
        }

        public void SetIndex(int index)
        {
            this.index = index;
        }

        public void SetTargetPos(Vector3 pos)
        {
            target_pos = pos;
        }

        public void SetTargetScale(Vector3 scale)
        {
            target_scale = scale;
        }

        public Card GetCard()
        {
            return card;
        }

        public int GetIndex()
        {
            return index;
        }

        public Vector3 GetTargetPos()
        {
            return target_pos;
        }

        public Vector3 GetTargetScale()
        {
            return target_scale;
        }
    }
}
