using System.Collections.Generic;
using TcgEngine.Client;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Encounter definition for the Total Assault (총력전) singleplayer mode.
    /// Mirrors LevelData but carries the dedicated rule-set fields:
    /// custom HP, passive cards that occupy the synergy slot,
    /// dedicated decks and avatars.
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
        public DeckData player_deck;
        public DeckData boss_deck;
        public LevelFirst first_player;
        public bool mulligan = true;

        [Header("Avatars")]
        public AvatarData player_avatar;
        public AvatarData boss_avatar;

        [Header("Stat Table Overrides")]
        public int player_start_hp = 30;
        public int player_hand_size = 5;
        public int boss_start_hp = 80;
        public int boss_hand_size = 8;

        [Header("Boss Gauges (max + start)")]
        public int skill_gauge_max = 100;
        public int skill_gauge_start = 0;
        public int atg_gauge_max = 100;
        public int atg_gauge_start = 0;
        public int groggy_gauge_max = 100;
        public int groggy_gauge_start = 0;

        [Header("Boss Mana Progression")]
        public bool boss_mana_increases_per_turn = true;

        //Appended to boss cards_club at game start. To make these the boss's ONLY
        //synergy cards, leave boss_deck.clubs empty.
        [Header("Boss Passives (appended to boss synergy / cards_club slot)")]
        public CardData[] boss_passives;

        [Header("Phase System")]
        public TotalAssaultPhase[] phases;

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

        //--- IGameSetupProvider (match-level; boss vs player split via player.is_ai) ---
        public int? GetStartHp(Player player) { return player.is_ai ? boss_start_hp : player_start_hp; }
        public int? GetStartMana(Player player) { return null; }
        public int? GetStartHand(Player player) { return player.is_ai ? boss_hand_size : player_hand_size; }
        public LevelFirst? GetFirstPlayer() { return first_player; }
        public bool? GetMulligan() { return mulligan; }
        public IEnumerable<CardData> GetExtraClubs(Player player) { return player.is_ai ? boss_passives : null; }

        //--- IGameTypeView (menu display + launch) ---
        public string GetTitle() { return title; }
        public Sprite GetIcon() { return icon; }
        public DeckData GetDisplayDeck() { return player_deck; }
        public string GetId() { return id; }
        public GameType GetGameType() { return GameType.TotalAssault; }

        public void ApplyGameSettings()
        {
            GameClient.game_settings.level = id;
            GameClient.game_settings.scene = scene;
            if (player_deck != null)
                GameClient.player_settings.deck = new UserDeckData(player_deck);
            if (boss_deck != null)
                GameClient.ai_settings.deck = new UserDeckData(boss_deck);
            GameClient.ai_settings.ai_level = 10;
            if (player_avatar != null)
                GameClient.player_settings.avatar = player_avatar.id;
            if (boss_avatar != null)
                GameClient.ai_settings.avatar = boss_avatar.id;
        }
    }

    /// <summary>
    /// One phase of a Total Assault encounter. Phases are entered when the boss's
    /// HP percentage drops below trigger_hp_percent (evaluated from highest threshold first).
    /// Passives in this phase are added on top of base boss_passives.
    /// </summary>
    [System.Serializable]
    public class TotalAssaultPhase
    {
        public string phase_id;
        [Range(0, 100)] public int trigger_hp_percent = 100;
        public AbilityData[] enter_abilities;
        public AbilityData[] passives;
    }
}
