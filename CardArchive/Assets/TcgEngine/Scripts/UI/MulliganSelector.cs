using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Client;

namespace TcgEngine.UI
{

    public class MulliganSelector : SelectorPanel
    {
        public RectTransform content;
        public GameObject mulligan_template;
        public List<CardMulligan> cards = new List<CardMulligan>();

        private float interval;

        private static MulliganSelector instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        private void RefreshMulligan()
        {
            Player player = GameClient.Get().GetPlayer();
            string bonus_id = GameplayData.Get().second_bonus != null ? GameplayData.Get().second_bonus.id : "";

            cards.Clear();

            int index = 0;
            foreach (Card card in player.cards_hand)
            {
                if (card.card_id != bonus_id)
                {
                    GameObject mulligan_card = Instantiate(mulligan_template, content.transform);
                    mulligan_card.SetActive(true);
                    RectTransform card_rect = mulligan_card.GetComponent<RectTransform>();
                    CardMulligan mcard = mulligan_card.GetComponent<CardMulligan>();

                    mcard.SetCard(card);
                    mcard.SetIndex(index);
                    mcard.onClick += OnClickCard;
                    cards.Add(mcard);

                    index++;
                }
            }

            interval = content.rect.width / (cards.Count + 1);

            foreach (CardMulligan card in cards)
            {
                RectTransform card_rect = card.GetComponent<RectTransform>();

                card.SetTargetPos(GetCardPos(card));
                card.SetTargetScale(Vector3.one * 0.7f);
                card_rect.anchoredPosition = card.GetTargetPos();
            }
        }

        private Vector2 GetCardPos(CardMulligan card)
        {
            float xPos = interval * (card.GetIndex()+1) - (content.rect.width / 2);
            Vector2 position = new Vector2(xPos, 0);
            return position;
        }

        private void OnClickCard(CardMulligan card_ui)
        {
            card_ui.SetSelected(!card_ui.IsSelected());
        }

        public void OnClickOK()
        {
            List<string> selected_cards = new List<string>();

            foreach (CardMulligan acard in cards)
            {
                if (acard.IsSelected())
                    selected_cards.Add(acard.GetCard().uid);
            }

            GameClient.Get().Mulligan(selected_cards.ToArray());
            Hide();
        }

        public override void Show(bool instant = false)
        {
            base.Show(instant);
            RefreshMulligan();
        }

        public override bool ShouldShow()
        {
            Game gdata = GameClient.Get().GetGameData();
            Player player = GameClient.Get().GetPlayer();
            return gdata.IsPlayerMulliganTurn(player);
        }

        public static MulliganSelector Get()
        {
            return instance;
        }
    }
}