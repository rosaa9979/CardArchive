using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TcgEngine.Client;
using UnityEngine.Events;

namespace TcgEngine.UI
{

    public class MulliganSelector : SelectorPanel
    {
        public RectTransform content;
        public GameObject mulligan_template;
        public List<CardMulligan> cards = new List<CardMulligan>();

        public UnityAction<Card> onMulliganSelect;
        public UnityAction onMulliganConfirm;

        private float interval;

        private static MulliganSelector instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        protected override void Start()
        {
            base.Start();

            GameClient.Get().onMulligan += OnMulligan;
        }

        private async void OnMulligan(int player_id)
        {
            Player player = GameClient.Get().GetPlayer();
            Game game_data = GameClient.Get().GetGameData();
            string bonus_id = GameplayData.Get().second_bonus != null ? GameplayData.Get().second_bonus.id : "";

            if (player.player_id != player_id)
                return;

            Queue<int> removed_card_index = new Queue<int>();

            foreach (CardMulligan card in cards)
            {
                bool isCardInList = player.cards_hand.Any(icard => icard.uid == card.GetCard().uid);
                if (!isCardInList)
                {
                    removed_card_index.Enqueue(cards.IndexOf(card));
                    card.DoHide(GetCardPos(card));
                    await TimeTool.Delay(150);
                }
            }

            await TimeTool.Delay(1000);

            foreach (Card new_card in player.cards_hand)
            {
                if (!cards.Any(icard => icard.GetCard().uid == new_card.uid) && new_card.card_id != bonus_id)
                {
                    int new_index = removed_card_index.Dequeue();
                    cards.RemoveAt(new_index);

                    GameObject mulligan_card = Instantiate(mulligan_template, content.transform);
                    mulligan_card.SetActive(true);
                    RectTransform card_rect = mulligan_card.GetComponent<RectTransform>();
                    CardMulligan mcard = mulligan_card.GetComponent<CardMulligan>();

                    mcard.SetCard(new_card);
                    mcard.SetIndex(new_index);
                    mcard.Hide();
                    mcard.onClick += OnClickCard;
                    cards.Add(mcard);

                    cards.Insert(new_index, mcard);

                    mcard.DoShow(GetCardPos(mcard));

                    await TimeTool.Delay(150);
                }
            }
        }

        private async void RefreshMulligan()
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
                    mcard.Hide();
                    mcard.onClick += OnClickCard;
                    cards.Add(mcard);

                    index++;
                }
            }

            interval = content.rect.width / (cards.Count + 1);

            foreach (CardMulligan card in cards)
            {
                card.DoShow(GetCardPos(card));

                await TimeTool.Delay(150);
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
            if (!Tutorial.Get().CanDo(TutoEndTrigger.MulliganSelect, card_ui.GetCard()))
                return;
            card_ui.SetSelected(!card_ui.IsSelected());
            onMulliganSelect?.Invoke(card_ui.GetCard());
        }

        public void OnClickOK()
        {
            if (!Tutorial.Get().CanDo(TutoEndTrigger.MulliganConfirm))
                return;

            List<string> selected_cards = new List<string>();

            foreach (CardMulligan acard in cards)
            {
                if (acard.IsSelected())
                    selected_cards.Add(acard.GetCard().uid);
            }

            GameClient.Get().Mulligan(selected_cards.ToArray());
            onMulliganConfirm?.Invoke();
            //Hide();
        }

        public override void Show(bool instant = false)
        {
            base.Show(instant);
            RefreshMulligan();
        }

        public override void Hide(bool instant = false)
        {
            base.Hide(instant);
            Debug.Log("언제 꺼짐?");

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