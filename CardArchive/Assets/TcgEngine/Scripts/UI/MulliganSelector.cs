using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TcgEngine.Client;
using UnityEngine.Events;
using UnityEngine.UI;
using DG.Tweening;

namespace TcgEngine.UI
{

    public class MulliganSelector : SelectorPanel
    {
        public RectTransform content;
        public GameObject mulligan_template;
        public Button select_button;
        public Text select_text;
        public Text description_text;
        public Sprite after_select;


        [Header("Handoff")]
        public int handoff_wait_ms = 250;       //Pause after both players are ready, before the handoff sequence starts
        public int handoff_room_ms = 1000;      //Time for existing cards to compress and open the bonus slot (slower = gentler)
        public int handoff_bonus_ms = 300;      //Time for the bonus card to appear in the opened slot
        public int handoff_move_delay_ms = 300; //Pause after everything is in place, just before the cards glide into hand
        public float handoff_move_speed = 3f;   //HandCard lerp speed while gliding from mulligan slot into the hand

        private List<CardMulligan> cards = new List<CardMulligan>();

        public UnityAction<Card> onMulliganSelect;
        public UnityAction onMulliganConfirm;

        private float interval;

        private bool local_done = false;        //Local player's own mulligan swap animation finished
        private bool handoff_started = false;   //Both players ready -> handoff sequence is running
        private bool hand_revealed = false;     //Mulligan cards have been swapped for the real hand cards

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

            //Opponent finished: only re-evaluate whether the handoff can start now
            if (player.player_id != player_id)
            {
                TryStartHandoff();
                return;
            }

            select_button.image.sprite = after_select;
            select_text.color = new Color(1, 1, 1);

            Sequence sequence = DOTween.Sequence();
            sequence.Append(description_text.transform.DOScale(0.1f, 0.2f))
                    .AppendCallback(() => description_text.text = "상대방이 카드를 선택하고 있습니다")
                    .Append(description_text.transform.DOScale(1, 0.2f).SetEase(Ease.OutBack));

            foreach (CardMulligan card in cards)
                card.onClick -= OnClickCard;

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

                    GameObject mulligan_card = Instantiate(mulligan_template, content.transform);
                    mulligan_card.SetActive(true);
                    RectTransform card_rect = mulligan_card.GetComponent<RectTransform>();
                    CardMulligan mcard = mulligan_card.GetComponent<CardMulligan>();

                    mcard.SetCard(new_card);
                    mcard.SetIndex(new_index);
                    mcard.Hide();

                    //Server keeps the mulliganed slot's index, so drop the new card into that same UI slot
                    //(the old card there was already hidden/destroyed by DoHide). No Add/Insert -> no dupes.
                    cards[new_index] = mcard;

                    mcard.DoShow(GetCardPos(mcard));

                    await TimeTool.Delay(150);
                }
            }

            local_done = true;
            TryStartHandoff();
        }

        //Starts the handoff once BOTH players are ready and the local swap animation has finished.
        private void TryStartHandoff()
        {
            if (handoff_started || !local_done)
                return;

            Game game_data = GameClient.Get().GetGameData();
            if (game_data == null || !game_data.AreAllPlayersReady())
                return;

            handoff_started = true;
            DoHandoff();
        }

        //True only once the real hand cards have taken over from the mulligan cards.
        //Until then HandCardArea keeps the hand hidden so nothing shows in two places.
        public bool IsHandReady()
        {
            return hand_revealed;
        }

        private async void DoHandoff()
        {
            Player player = GameClient.Get().GetPlayer();
            string bonus_id = GameplayData.Get().second_bonus != null ? GameplayData.Get().second_bonus.id : "";

            //The swap step above can leave duplicate references in 'cards'; keep only distinct entries
            //so the count/spacing math and the handoff loop below run once per card.
            cards = cards.Where(c => c != null).Distinct().ToList();

            foreach (CardMulligan card in cards)
                card.onClick -= OnClickCard;

            //Wait a beat before the cards leave the mulligan panel
            await TimeTool.Delay(handoff_wait_ms);

            //Bonus card was never part of the mulligan UI: make room by compressing the existing
            //cards, then spawn it into the opened slot so it flies to hand together with the rest.
            Card bonus_card = player.cards_hand.FirstOrDefault(c => c.card_id == bonus_id);
            if (bonus_card != null && cards.All(c => c.GetCard().uid != bonus_card.uid))
            {
                int total = cards.Count + 1;
                interval = content.rect.width / (total + 1);

                for (int i = 0; i < cards.Count; i++)
                {
                    cards[i].SetIndex(i);
                    cards[i].DoMove(GetCardPos(cards[i]), handoff_room_ms / 1000f);
                }
                await TimeTool.Delay(handoff_room_ms);

                GameObject mulligan_card = Instantiate(mulligan_template, content.transform);
                mulligan_card.SetActive(true);
                CardMulligan mcard = mulligan_card.GetComponent<CardMulligan>();
                mcard.SetCard(bonus_card);
                mcard.SetIndex(cards.Count);
                mcard.Hide();
                cards.Add(mcard);
                mcard.DoShow(GetCardPos(mcard));

                await TimeTool.Delay(handoff_bonus_ms);
            }

            //Hold a beat with everything in place before the cards leave for the hand
            await TimeTool.Delay(handoff_move_delay_ms);

            //Swap each mulligan card for the real hand card at the same position/size,
            //then let HandCard.Update() carry it into its hand slot. Revealing here (rather than
            //when the handoff started) keeps the hand hidden during the bonus-card animation above.
            hand_revealed = true;
            foreach (CardMulligan mc in cards)
            {
                if (mc == null)
                    continue;

                HandCard hc = HandCard.Get(mc.GetCard().uid);
                if (hc != null)
                {
                    hc.gameObject.SetActive(true);
                    hc.StartHandoffFrom(mc.GetVisualRect());
                    hc.SetMoveSpeed(handoff_move_speed, true);  //Slow glide into hand, restore default on arrival
                }

                mc.gameObject.SetActive(false);
            }

            cards.Clear();

            //Close the mulligan panel now so it disappears together with the cards leaving for the hand,
            //instead of lingering until the server's first-turn buffer elapses.
            Hide();
        }

        private async void RefreshMulligan()
        {
            Player player = GameClient.Get().GetPlayer();
            string bonus_id = GameplayData.Get().second_bonus != null ? GameplayData.Get().second_bonus.id : "";

            local_done = false;
            handoff_started = false;
            hand_revealed = false;
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

                await TimeTool.Delay(50);
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

        public override bool ShouldShow()
        {
            //Once the cards have been handed off to the hand, keep the panel closed even though the
            //mulligan phase is still active (GameUI would otherwise re-show it during the turn buffer).
            if (hand_revealed)
                return false;

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