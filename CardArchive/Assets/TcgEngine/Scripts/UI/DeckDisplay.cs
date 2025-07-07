using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TcgEngine.UI
{
    /// <summary>
    /// Can display a deck in the UI
    /// Only shows a few cards and the total amount of cards
    /// </summary>

    public class DeckDisplay : MonoBehaviour
    {
        public Text deck_title;
        public Text card_count;
        public Image[] ui_clubs;
        public CardUI[] ui_cards;

        [SerializeField]
        private Color available_color;
        [SerializeField]
        private Color unavailable_color;
        private string deck_id;

        void Awake()
        {
            Clear();
        }

        void Update()
        {

        }

        public void Clear()
        {
            if (deck_title != null)
                deck_title.text = "";
            if (card_count != null)
                card_count.text = "";
            foreach (CardUI card in ui_cards)
                card.Hide();
        }

        public void SetDeck(string tid)
        {
            UserData user = Authenticator.Get().UserData;
            UserDeckData udeck = user.GetDeck(tid);
            DeckData ddeck = DeckData.Get(tid);
            if (udeck != null)
                SetDeck(udeck);
            else if (ddeck != null)
                SetDeck(ddeck);
            else
                Clear();
        }

        public void SetDeck(UserDeckData deck)
        {
            Clear();

            if (deck != null)
            {
                deck_id = deck.tid;

                if (deck_title != null)
                    deck_title.text = deck.title;

                if (card_count != null)
                {
                    card_count.text = deck.GetQuantity().ToString() + " / " + GameplayData.Get().deck_size.ToString();
                    card_count.color = deck.GetQuantity() >= GameplayData.Get().deck_size ? available_color : unavailable_color;
                }

                List<CardDataQ> cards = new List<CardDataQ>();
                foreach (UserCardData ucard in deck.cards)
                {
                    CardDataQ card = new CardDataQ();
                    card.card = CardData.Get(ucard.tid);
                    card.variant = VariantData.Get(ucard.variant);
                    card.quantity = ucard.quantity;
                    if (card.card != null)
                        cards.Add(card);
                }

                List<CardDataQ> clubs = new List<CardDataQ>();
                foreach (UserCardData ucard in deck.clubs)
                {
                    CardDataQ card = new CardDataQ();
                    card.card = CardData.Get(ucard.tid);
                    card.variant = VariantData.Get(ucard.variant);
                    card.quantity = ucard.quantity;
                    if (card.card != null)
                        cards.Add(card);
                }

                ShowCards(clubs);
            }

            gameObject.SetActive(deck != null);
        }

        public void SetDeck(DeckData deck)
        {
            Clear();

            if (deck != null)
            {
                deck_id = deck.id;

                if (deck_title != null)
                    deck_title.text = deck.title;

                if (card_count != null)
                {
                    card_count.text = deck.GetQuantity().ToString() + " / " + GameplayData.Get().deck_size.ToString();
                    card_count.color = deck.GetQuantity() >= GameplayData.Get().deck_size ? available_color : unavailable_color;
                }

                List<CardDataQ> dcards = new List<CardDataQ>();
                VariantData dvariant = VariantData.GetDefault();
                foreach (CardData icard in deck.cards)
                {
                    if (icard != null)
                    {
                        CardDataQ card = new CardDataQ();
                        card.card = icard;
                        card.variant = dvariant;
                        card.quantity = 1;
                        dcards.Add(card);
                    }
                }

                if (deck is DeckPuzzleData)
                {
                    DeckPuzzleData pdeck = (DeckPuzzleData)deck;
                    foreach (DeckCardSlot slot in pdeck.board_cards)
                    {
                        if (slot.card != null)
                        {
                            CardDataQ card = new CardDataQ();
                            card.card = slot.card;
                            card.variant = dvariant;
                            card.quantity = 1;
                            dcards.Add(card);
                        }
                    }
                }

                List<CardDataQ> ccards = new List<CardDataQ>();
                VariantData cvariant = VariantData.GetDefault();
                foreach (CardData icard in deck.clubs)
                {
                    if (icard != null)
                    {
                        CardDataQ card = new CardDataQ();
                        card.card = icard;
                        card.variant = cvariant;
                        card.quantity = 1;
                        ccards.Add(card);
                    }
                }
            

                ShowCards(ccards);
                ShowClubs(deck.clubs);
            }

            gameObject.SetActive(deck != null);
        }

        public void ShowCards(List<CardDataQ> cards)
        {
            cards.Sort((CardDataQ a, CardDataQ b) => { return b.card.mana.CompareTo(a.card.mana); });

            int index = 0;
            foreach (CardDataQ icard in cards)
            {
                for (int i = 0; i < icard.quantity; i++)
                {
                    if (index < ui_cards.Length)
                    {
                        CardUI card_ui = ui_cards[index];
                        card_ui.SetCard(icard.card, icard.variant);
                        index++;
                    }
                }
            }
        }

        public void ShowClubs(CardData[] clubs)
        {
            int index = 0;
            foreach (CardData icard in clubs)
            {
                if (index < ui_clubs.Length)
                {
                    Image club_ui = ui_clubs[index];
                    club_ui.sprite = icard.clubs[0].icon;
                    index++;
                }
            }
        }
        

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public string GetDeck()
        {
            return deck_id;
        }
    }
}
