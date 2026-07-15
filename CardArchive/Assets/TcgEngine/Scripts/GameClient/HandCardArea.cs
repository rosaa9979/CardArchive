using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using TcgEngine.UI;
using Unity.VisualScripting;

namespace TcgEngine.Client
{
    /// <summary>
    /// Area where all the hand cards are
    /// Will take card of spawning/despawning hand cards based on the refresh data received from server
    /// </summary>

    public class HandCardArea : MonoBehaviour
    {
        public GameObject card_prefab;
        public RectTransform card_area;
        public float card_spacing = 100f;
        public float card_angle = 10f;
        public float card_offset_y = 10f;

        private List<HandCard> cards = new List<HandCard>();

        private bool is_dragging;

        private string last_destroyed;
        private float last_destroyed_timer = 0f;

        private static HandCardArea _instance;

        void Awake()
        {
            _instance = this;
        }

        void Update()
        {
            if (!GameClient.Get().IsReady())
                return;

            int player_id = GameClient.Get().GetPlayerID();
            Game data = GameClient.Get().GetGameData();
            Player player = data.GetPlayer(player_id);

            last_destroyed_timer += Time.deltaTime;

            //Add new cards
            foreach (Card card in player.cards_hand)
            {
                if (!HasCard(card.uid))
                    SpawnNewCard(card, player.cards_hand.IndexOf(card));
            }

            //Remove destroyed cards
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                HandCard card = cards[i];
                if (card == null || player.GetHandCard(card.GetCard().uid) == null)
                {
                    cards.RemoveAt(i);
                    if (card != null)
                        card.Kill();
                }
            }

            //Keep the hand ordered exactly like the server's cards_hand, regardless of how the local
            //list was built (it can diverge - e.g. mulligan inserts new cards before removing old ones).
            cards.Sort(SortFunc);

            //Set card index
            int index = 0;
            float count_half = cards.Count / 2f;
            foreach (HandCard card in cards)
            {
                card.deck_position = new Vector2((index - count_half) * card_spacing, (index - count_half) * (index - count_half) * -card_offset_y);
                card.deck_angle = (index - count_half) * -card_angle;
                index++;
            }

            HandCard selected_card = HandCard.GetDrag();
            List<HandCard> card_list = HandCard.GetAll();

            //During mulligan selection the hand is shown through the mulligan UI only.
            //This also covers the GameStart phase (starting hand is drawn there, just before the
            //mulligan phase begins) so the freshly drawn cards don't flash in hand first.
            //Once the handoff reveals them, the mulligan cards turn into these real hand cards.
            bool mulligan_flow = GameplayData.Get() != null && GameplayData.Get().mulligan;
            bool mulligan_hold = mulligan_flow
                && (data.phase == GamePhase.Mulligan || data.phase == GamePhase.GameStart)
                && (MulliganSelector.Get() == null || !MulliganSelector.Get().IsHandReady());

            foreach (HandCard hcard in card_list)
            {
                bool visible = false;
                if (!mulligan_hold && !GameUI.Get().GetHideUI())
                {
                    if (selected_card == null)
                        visible = true;
                    else if (selected_card == hcard)
                        visible = true;
                }

                hcard.SetHide(visible);
            }

            //Set target forcus
            HandCard drag_card = HandCard.GetDrag();
            is_dragging = drag_card != null;
        }

        public void SpawnNewCard(Card card)
        {
            GameObject card_obj = Instantiate(card_prefab, card_area.transform);
            card_obj.GetComponent<HandCard>().SetCard(card);
            card_obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -100f);
            cards.Add(card_obj.GetComponent<HandCard>());
        }

        public void SpawnNewCard(Card card, int index)
        {
            GameObject card_obj = Instantiate(card_prefab, card_area.transform);
            card_obj.GetComponent<HandCard>().SetCard(card);
            card_obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -100f);
            cards.Insert(index, card_obj.GetComponent<HandCard>());

            SortCards();
        }

        public void DelayRefresh(Card card)
        {
            last_destroyed_timer = 0f;
            last_destroyed = card.uid;
        }

		public void SortCards()
        {
            cards.Sort(SortFunc);

            int i = 0;
            foreach (HandCard acard in cards)
            {
                acard.transform.SetSiblingIndex(i);
                acard.SetHide(true);
                i++;
            }
        }

        private int SortFunc(HandCard a, HandCard b)
        {
            return HandIndex(a).CompareTo(HandIndex(b));
        }

        //Position of a hand card within the server's cards_hand (the authoritative hand order).
        //Cards not currently in hand sort to the end.
        private int HandIndex(HandCard card)
        {
            if (card == null)
                return int.MaxValue;
            Card c = card.GetCard();
            Player player = c != null ? GameClient.Get().GetPlayer() : null;
            int idx = player != null ? player.cards_hand.FindIndex(hc => hc.uid == c.uid) : -1;
            return idx >= 0 ? idx : int.MaxValue;
        }

        public bool HasCard(string card_uid)
        {
            HandCard card = HandCard.Get(card_uid);
            bool just_destroyed = card_uid == last_destroyed && last_destroyed_timer < 0.7f;
            return card != null || just_destroyed;
        }

        public bool IsDragging()
        {
            return is_dragging;
        }


        public static HandCardArea Get()
        {
            return _instance;
        }
    }
}