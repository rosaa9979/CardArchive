using System.Collections.Generic;
using TcgEngine.Client;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Tutorial encounter definition. Mirrors LevelData for the fields a tutorial
    /// match needs, plus the in-game guide prefab (formerly LevelData.tuto_prefab).
    /// Tutorial.cs instantiates tuto_prefab when a match starts with
    /// GameType.Tutorial. Implements IGameSetupProvider so first_player / mulligan
    /// flow through the same setup pipeline as Adventure and Total Assault.
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialData", menuName = "TcgEngine/TutorialData", order = 7)]
    public class TutorialData : ScriptableObject, IGameSetupProvider, IGameTypeView
    {
        public string id;
        public int order;

        [Header("Display")]
        public string title;
        public Sprite icon;
        [TextArea(2, 6)] public string description;

        [Header("Gameplay")]
        public string scene;
        public DeckData player_deck;
        public DeckData ai_deck;
        public int ai_level = 1;
        public LevelFirst first_player;
        public bool mulligan = true;

        [Header("Tutorial")]
        public GameObject tuto_prefab;

        [Header("Rewards")]
        public int reward_xp = 0;
        public int reward_coins = 0;
        public PackData[] reward_packs;
        public CardData[] reward_cards;

        public static List<TutorialData> tutorial_list = new List<TutorialData>();

        public static void Load(string folder = "")
        {
            if (tutorial_list.Count == 0)
            {
                tutorial_list.AddRange(Resources.LoadAll<TutorialData>(folder));
                tutorial_list.Sort((a, b) => a.order.CompareTo(b.order));
            }
        }

        public static TutorialData Get(string id)
        {
            foreach (TutorialData data in tutorial_list)
            {
                if (data.id == id)
                    return data;
            }
            return null;
        }

        public static List<TutorialData> GetAll()
        {
            return tutorial_list;
        }

        //--- IGameSetupProvider (match-level: Tutorial) ---
        public int? GetStartHp(Player player) { return null; }
        public int? GetStartMana(Player player) { return null; }
        public int? GetStartHand(Player player) { return null; }
        public LevelFirst? GetFirstPlayer() { return first_player; }
        public bool? GetMulligan() { return mulligan; }
        public IEnumerable<CardData> GetExtraClubs(Player player) { return null; }

        //--- IGameTypeView (menu display + launch) ---
        public string GetTitle() { return title; }
        public Sprite GetIcon() { return icon; }
        public DeckData GetDisplayDeck() { return player_deck; }
        public string GetId() { return id; }
        public GameType GetGameType() { return GameType.Tutorial; }

        public void ApplyGameSettings()
        {
            GameClient.game_settings.level = id;
            GameClient.game_settings.scene = scene;
            GameClient.player_settings.deck = new UserDeckData(player_deck);
            GameClient.ai_settings.deck = new UserDeckData(ai_deck);
            GameClient.ai_settings.ai_level = ai_level;
        }
    }
}
