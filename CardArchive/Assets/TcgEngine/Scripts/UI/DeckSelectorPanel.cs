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
        public UnityAction<string> onChange;

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
            if (!GameClient.player_settings.deck.IsValid())
                return;
                
            GameMode current_game_mode = GameClient.game_settings.game_mode;
            GameType current_game_type = GameClient.game_settings.game_type;

            if (current_game_type == GameType.Solo)
            {
                GameClient.player_settings.deck.tid = GetDeckID();
                GameClient.ai_settings.deck.tid = GameplayData.Get().GetRandomAIDeck();
                GameClient.ai_settings.ai_level = GameplayData.Get().ai_level;
                GameClient.game_settings.scene = GameplayData.Get().GetRandomArena();

                MainMenu.Get().StartGame(GameType.Solo, GameMode.Casual);
            }

            if (current_game_type == GameType.Multiplayer)
            {
                if (GameClient.game_settings.game_mode == GameMode.Ranked)
                {
                    MainMenu.Get().StartMathmaking(current_game_mode, "");
                }

                if (GameClient.game_settings.game_mode == GameMode.Casual)
                {
                    string game_code = JoinCodePanel.Get().GetCode();
                    MainMenu.Get().StartMathmaking(current_game_mode, "code_" + game_code);
                }
            }

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
