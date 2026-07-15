using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using DG.Tweening;

namespace TcgEngine.UI
{
    public class CardMulligan : MonoBehaviour
    {
        public CardUI card_ui;
        public Image x_img;

        private Card card;
        private int index;
        private Vector2 target_pos;

        public UnityAction<CardMulligan> onClick;

        private void Awake()
        {
            if (x_img != null)
                x_img.enabled = false;

            card_ui.onClick += OnClick;
        }

        public void DoShow(Vector2 position, float delay = 0f, float slideDistance = 200, float duration = 0.6f)
        {
            RectTransform card_rect = GetComponent<RectTransform>();
            CanvasGroup canvas_group = GetComponent<CanvasGroup>();

            Vector2 targetPos = position;
            Vector2 startPos = targetPos;
            startPos.y -= slideDistance;

            card_rect.anchoredPosition = startPos;
            canvas_group.alpha = 0f;
            card_rect.localScale = Vector3.one * 0.7f;

            Sequence sequence = DOTween.Sequence();
            sequence.SetDelay(delay);
            sequence.Append(card_rect.DOAnchorPos(targetPos, duration).SetEase(Ease.OutExpo));
            //sequence.Join(card_rect.DOScale(0.7f, duration));
            sequence.Join(canvas_group.DOFade(1f, duration).SetEase(Ease.OutExpo));
        }

        public void DoHide(Vector2 position, float delay = 0f, float slideDistance = 200, float duration = 0.4f)
        {
            RectTransform card_rect = GetComponent<RectTransform>();
            CanvasGroup canvas_group = GetComponent<CanvasGroup>();

            Vector2 targetPos = position;
            targetPos.y -= slideDistance;

            canvas_group.alpha = 1f;
            card_rect.localScale = Vector3.one * 0.7f;

            Sequence sequence = DOTween.Sequence();
            sequence.SetDelay(delay);
            sequence.Append(card_rect.DOAnchorPos(targetPos, duration).SetEase(Ease.InQuad));
            //sequence.Join(card_rect.DOScale(0.7f, duration));
            sequence.Join(canvas_group.DOFade(0f, duration).SetEase(Ease.InExpo));
            
            sequence.OnComplete(() => 
            {
                if (gameObject != null)
                    Destroy(gameObject);
            });
        }

        public void DoMove(Vector2 position, float duration = 0.3f)
        {
            RectTransform card_rect = GetComponent<RectTransform>();
            card_rect.DOAnchorPos(position, duration).SetEase(Ease.OutQuad);
        }

        // RectTransform of the actual visible card, used to align the spawned HandCard during the mulligan handoff
        public RectTransform GetVisualRect()
        {
            if (card_ui != null)
                return card_ui.GetComponent<RectTransform>();
            return GetComponent<RectTransform>();
        }

        public int GetIndex()
        {
            return index;
        }

        public Vector2 GetTargetPos()
        {
            return target_pos;
        }

        public void SetCard(Card card)
        {
            this.card = card;
            card_ui.SetCard(card.CardData, card.VariantData);
            gameObject.SetActive(true);
        }

        public void SetSelected(bool discard)
        {
            if (x_img != null)
                x_img.enabled = discard;
        }

        public void SetIndex(int new_index)
        {
            index = new_index;
        }

        public void SetTargetPos(Vector2 new_pos)
        {
            target_pos = new_pos;
        }

        public void SetTargetScale(Vector3 new_scale)
        {
            transform.localScale = new_scale;
        }

        public void Hide()
        {
            CanvasGroup canvas_group = GetComponent<CanvasGroup>();
            canvas_group.alpha = 0.0f;
        }

        public bool IsSelected()
        {
            if (x_img != null)
                return x_img.enabled;
            return false;
        }

        public Card GetCard()
        {
            return card;
        }

        private void OnClick(CardUI card_ui)
        {
            onClick?.Invoke(this);
        }

    }
}