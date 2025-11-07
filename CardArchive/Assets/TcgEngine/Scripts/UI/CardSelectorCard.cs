using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine;
using DG.Tweening;
using UnityEngine.Events;

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
        public UnityAction<CardSelectorCard> onClick;

        [HideInInspector] 
        public bool selected = false;

        private int index;
        private Vector2 target_pos;
        private Vector3 target_scale;

        private Card card;

        private RectTransform rect;
        private CanvasGroup canvas_group;

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
            canvas_group = GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            transform.localScale = target_scale;
        }

        private void Update()
        {
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, target_pos, 5f * Time.deltaTime);
            //transform.localScale = Vector2.Lerp(transform.localScale, target_scale, 2f * Time.deltaTime);
        }

        public void OnClick()
        {
            if (selectable)
            {
                /*
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
                */
                onClick.Invoke(this);
            }
        }

        public void DoShow(float duration = 0.4f)
        {
            RectTransform card_rect = GetComponent<RectTransform>();
            CanvasGroup canvas_group = GetComponent<CanvasGroup>();

            Vector2 targetPos = target_pos;
            Vector2 startPos = targetPos;


            rect.anchoredPosition = startPos;
            canvas_group.alpha = 1f;
            rect.localScale = Vector3.one;

            //Sequence sequence = DOTween.Sequence();
            //sequence.Append(card_rect.DOAnchorPos(targetPos, duration).SetEase(Ease.OutExpo));
            //sequence.Append(card_rect.DOPunchScale(Vector3.one * 0.1f, duration, 1));
            rect.DOPunchScale(Vector3.one * 0.1f, duration, 1).SetLink(gameObject);
            //sequence.Join(canvas_group.DOFade(1f, duration).SetEase(Ease.OutExpo));
        }

        public async Task DoHideAsync(bool is_selected = false, float duration = 0.4f)
        {
            Vector2 targetPos = target_pos;
            Vector2 startPos = targetPos;


            rect.anchoredPosition = startPos;
            canvas_group.alpha = 1f;
            rect.localScale = Vector3.one;

            Sequence sequence = DOTween.Sequence();

            if (is_selected)
            {
                sequence.Append(transform.DOScale(0.95f, 0.1f))    // 눌림
                        .Append(transform.DOScale(1.05f, 0.1f))    // 튕김
                        .Append(transform.DOScale(1f, 0.1f));  
            }

            else
            {
                sequence.Append(canvas_group.DOFade(0.0f, duration));
            }

            await sequence.AsyncWaitForCompletion();         
        }

        public void SetCard(Card card)
        {
            this.card = card;
            CardData icard = CardData.Get(card.card_id);
            //card_ui.SetCard(icard, card.VariantData);
            card_ui.SetCard(card);
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
