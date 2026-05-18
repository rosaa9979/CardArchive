using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Data.Common;
using TcgEngine.Client;

namespace TcgEngine.UI
{

    public class DeckSelectorPanel : UIPanel
    {
        public DeckDisplay[] deck_list;
        public Text warning_text;
        public UnityAction<string> onChange;
        public UnityAction<string> onConfirm;   //Fires on Play with the selected deck id. Each entry registers its own.

        private static DeckSelectorPanel instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        protected override void Start()
        {
            base.Start();

            //foreach (TabButton Tab in TabButton.GetAll())
            //{
            //    Tab.onClick += OnClickTab;
            //}

            foreach (DeckDisplay deck in deck_list)
            {
                deck.onDeckClicked += SelectDeck;
            }

            warning_text.enabled = false;
        }

        protected override void Update()
        {
            base.Update();
        }

        public override void Show(bool instant = false)
        {
            base.Show(instant);

            RefreshDeckList();
        }

        public void RefreshDeckList()
        {
            Clear();

            UserData udata = Authenticator.Get().UserData;
            if (udata != null)
            {
                int idx = 0;
                foreach (UserDeckData deck in udata.decks)
                {
                    if (idx < deck_list.Length && deck_list[idx] != null)
                    {
                        deck_list[idx].SetDeck(deck);
                        idx += 1;
                    }
                }

                for (int i = idx; i < deck_list.Length; i++)
                {
                    deck_list[i].gameObject.SetActive(false);
                }

                bool hasNoDecks = udata.decks.Length == 0;
                warning_text.enabled = hasNoDecks;
            }
        }

        public void SelectDeck(string tid)
        {
            UserData user = Authenticator.Get().UserData;

            foreach (DeckDisplay deck in deck_list)
            {
                if (deck.GetDeckID() == tid)
                    deck.SetSelected(true);
                    
                else
                    deck.SetSelected(false);
            }

            onChange?.Invoke(tid);
        }

        public string GetDeckID()
        {
            foreach (DeckDisplay deck in deck_list)
            {
                if (deck.IsSelected())
                {
                    return deck.GetDeckID();
                }
            }

            return "";
        }

        public UserDeckData GetDeck()
        {
            UserData user = Authenticator.Get().UserData;
            UserDeckData udeck = user.GetDeck(GetDeckID()); //Check for user custom deck
            DeckData deck = DeckData.Get(GetDeckID());     //Check for deck presets
            if (udeck != null)
                return udeck;
            else if (deck != null)
                return new UserDeckData(deck);
            return null;
        }

        public void OnClickPlay()
        {
            if (GetDeck() == null || !GetDeck().IsValid())
                return;

            UnityAction<string> cb = onConfirm;
            onConfirm = null;   //one-shot — next opener registers its own
            cb?.Invoke(GetDeckID());

            Hide();
        }

        private void OnClickTab()
        {
            Hide();
        }

        private void Clear()
        {
            foreach (DeckDisplay deck in deck_list)
            {
                deck.Clear();
            }
        }

        public static DeckSelectorPanel Get()
        {
            return instance;
        }

    }
}
