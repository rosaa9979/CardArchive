using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Per-player loadout override. Extends DeckData (cards + clubs/synergy + hero)
    /// with starting stats (HP / mana / hand), pre-placed board cards, and avatar.
    /// Picked up at match start when a player's deck id resolves to a PlayerSetupData
    /// (see GameLogic) and contributed as an IGameSetupProvider for that player.
    /// Mode-data (LevelData / TutorialData / TotalAssaultData) carries only mode-rule
    /// fields and delegates per-player setup here.
    /// </summary>

    [System.Serializable]
    public class DeckCardSlot
    {
        public CardData card;
        public SlotXY slot;
    }

    [CreateAssetMenu(fileName = "PlayerSetupData", menuName = "TcgEngine/PlayerSetupData", order = 7)]
    public class PlayerSetupData : DeckData, IGameSetupProvider
    {
        public DeckCardSlot[] board_cards;
        public int start_cards = 5;
        public int start_mana = 2;
        public int start_hp = 20;
        public bool dont_shuffle_deck;

        [Header("Identity")]
        public AvatarData avatar;

        public static new PlayerSetupData Get(string id)
        {
            foreach (DeckData deck in GetAll())
            {
                if (deck.id == id && deck is PlayerSetupData)
                    return (PlayerSetupData) deck;
            }
            return null;
        }

        //--- IGameSetupProvider (per-player; player param is always this deck's owner) ---
        public int? GetStartHp(Player player) { return start_hp; }
        public int? GetStartMana(Player player) { return start_mana; }
        public int? GetStartHand(Player player) { return start_cards; }
        public LevelFirst? GetFirstPlayer() { return null; }
        public bool? GetMulligan() { return null; }
        public IEnumerable<CardData> GetExtraClubs(Player player) { return null; }
    }
}
