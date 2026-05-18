using System.Collections.Generic;
using TcgEngine.Client;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Encounter definition for the Total Assault (총력전) singleplayer mode.
    /// The boss's full loadout (deck, clubs, hp, mana, hand size, board cards,
    /// avatar) is delegated to a PlayerSetupData asset. TotalAssaultData itself
    /// only carries the encounter-scope fields: which boss, match rules, boss
    /// gauges, rewards.
    /// The player always brings their own deck / avatar / global default stats —
    /// the encounter only specifies the boss they're fighting.
    /// GameLogic consumes this through IGameSetupProvider — no mode branching.
    /// </summary>
    [CreateAssetMenu(fileName = "TotalAssaultData", menuName = "TcgEngine/TotalAssaultData", order = 8)]
    public class TotalAssaultData : ScriptableObject, IGameSetupProvider, IGameTypeView
    {
        public string id;
        public int order;

        [Header("Display")]
        public string title;
        public Sprite icon;
        [TextArea(2, 6)] public string description;

        [Header("Gameplay")]
        public string scene;
        public PlayerSetupData boss_deck;
        public LevelFirst first_player;
        public bool mulligan = true;

        [Header("Boss Gauges (max + start)")]
        public int skill_gauge_max = 100;
        public int skill_gauge_start = 0;
        public int atg_gauge_max = 100;
        public int atg_gauge_start = 0;
        public int groggy_gauge_max = 100;
        public int groggy_gauge_start = 0;

        [Header("Boss Mana Progression")]
        public bool boss_mana_increases_per_turn = true;

        [Header("Rewards")]
        public int reward_xp = 200;
        public int reward_coins = 200;
        public PackData[] reward_packs;
        public CardData[] reward_cards;

        public static List<TotalAssaultData> assault_list = new List<TotalAssaultData>();

        public static void Load(string folder = "")
        {
            if (assault_list.Count == 0)
            {
                assault_list.AddRange(Resources.LoadAll<TotalAssaultData>(folder));
                assault_list.Sort((a, b) => a.order.CompareTo(b.order));
            }
        }

        public static TotalAssaultData Get(string id)
        {
            foreach (TotalAssaultData data in assault_list)
            {
                if (data.id == id)
                    return data;
            }
            return null;
        }

        public static List<TotalAssaultData> GetAll()
        {
            return assault_list;
        }

        //--- IGameSetupProvider (encounter-level rules; per-player setup delegated to boss_deck PlayerSetupData) ---
        public int? GetStartHp(Player player) { return null; }
        public int? GetStartMana(Player player) { return null; }
        public int? GetStartHand(Player player) { return null; }
        public LevelFirst? GetFirstPlayer() { return first_player; }
        public bool? GetMulligan() { return mulligan; }
        public IEnumerable<CardData> GetExtraClubs(Player player) { return null; }
        public bool? GetDrawsPerTurn(Player player) { return player.is_ai ? false : (bool?)null; }
        public bool? GetManaGrowsPerTurn(Player player) { return player.is_ai ? boss_mana_increases_per_turn : (bool?)null; }

        //--- IGameTypeView (menu display + launch) ---
        public string GetTitle() { return title; }
        public Sprite GetIcon() { return icon; }
        public DeckData GetDisplayDeck() { return boss_deck; }   //menu shows the boss the player will fight
        public string GetId() { return id; }
        public GameType GetGameType() { return GameType.TotalAssault; }

        public void Launch()
        {
            //Boss matches go through DeckSelectorPanel so the player picks their own deck.
            GameClient.game_settings.game_type = GameType.TotalAssault;
            GameClient.game_settings.game_mode = GameMode.Casual;

            TcgEngine.UI.DeckSelectorPanel panel = TcgEngine.UI.DeckSelectorPanel.Get();
            panel.onConfirm = (deck_id) =>
            {
                GameClient.player_settings.deck.tid = deck_id;
                ApplyGameSettings();
                TcgEngine.UI.MainMenu.Get().StartGame(GameType.TotalAssault, GameMode.Casual);
            };
            panel.Show();
        }

        private void ApplyGameSettings()
        {
            GameClient.game_settings.level = id;
            GameClient.game_settings.scene = scene;
            //Player keeps their own deck + avatar (no override here).
            if (boss_deck != null)
            {
                GameClient.ai_settings.deck = new UserDeckData(boss_deck);
                if (boss_deck.avatar != null)
                    GameClient.ai_settings.avatar = boss_deck.avatar.id;
            }
            GameClient.ai_settings.ai_level = 10;
        }
    }
}
