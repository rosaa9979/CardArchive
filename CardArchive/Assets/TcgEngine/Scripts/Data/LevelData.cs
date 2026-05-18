using System.Collections;
using System.Collections.Generic;
using TcgEngine.Client;
using UnityEngine;

namespace TcgEngine
{

    [CreateAssetMenu(fileName = "LevelData", menuName = "TcgEngine/LevelData", order = 7)]
    public class LevelData : ScriptableObject, IGameSetupProvider, IGameTypeView
    {
        public string id;
        public int level;

        [Header("Display")]
        public string title;
        public Sprite icon;

        [Header("Gameplay")]
        public string scene;
        public DeckData player_deck;
        public DeckData ai_deck;
        public int ai_level = 10; //From 1 to 10
        public LevelFirst first_player;
        public bool mulligan = true;

        [Header("Rewards")]
        public int reward_xp = 100;
        public int reward_coins = 100;
        public PackData[] reward_packs;
        public CardData[] reward_cards;

        public static List<LevelData> level_list = new List<LevelData>();

        public static void Load(string folder = "")
        {
            if (level_list.Count == 0)
            {
                level_list.AddRange(Resources.LoadAll<LevelData>(folder));
                level_list.Sort((LevelData a, LevelData b) => { return a.level.CompareTo(b.level); });
            }
        }

        public static LevelData Get(string id)
        {
            foreach (LevelData level in GetAll())
            {
                if (level.id == id)
                    return level;
            }
            return null;
        }

        public static List<LevelData> GetAll()
        {
            return level_list;
        }

        //--- IGameSetupProvider (match-level: Adventure) ---
        public int? GetStartHp(Player player) { return null; }
        public int? GetStartMana(Player player) { return null; }
        public int? GetStartHand(Player player) { return null; }
        public LevelFirst? GetFirstPlayer() { return first_player; }
        public bool? GetMulligan() { return mulligan; }
        public IEnumerable<CardData> GetExtraClubs(Player player) { return null; }
        public bool? GetDrawsPerTurn(Player player) { return null; }
        public bool? GetManaGrowsPerTurn(Player player) { return null; }

        //--- IGameTypeView (menu display + launch) ---
        public string GetTitle() { return title; }
        public Sprite GetIcon() { return icon; }
        public DeckData GetDisplayDeck() { return player_deck; }
        public string GetId() { return id; }
        public GameType GetGameType() { return GameType.Adventure; }

        public void Launch()
        {
            ApplyGameSettings();
            TcgEngine.UI.MainMenu.Get().StartGame(GameType.Adventure, GameMode.Casual);
        }

        private void ApplyGameSettings()
        {
            GameClient.game_settings.level = id;
            GameClient.game_settings.scene = scene;
            GameClient.player_settings.deck = new UserDeckData(player_deck);
            GameClient.ai_settings.deck = new UserDeckData(ai_deck);
            GameClient.ai_settings.ai_level = ai_level;
        }
    }

    public enum LevelFirst
    {
        Random = 0,
        Player = 10,
        AI = 20,
    }
}
