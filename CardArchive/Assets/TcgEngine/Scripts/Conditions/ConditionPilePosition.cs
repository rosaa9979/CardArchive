using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Condition that checks whether the target card sits at a given position of a pile
    /// (top / bottom / specific index). Reusable for deck-top scry, mill, tutor-from-top,
    /// "bottom card of discard", etc.
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/PilePosition", order = 10)]
    public class ConditionPilePosition : ConditionData
    {
        public enum PosMode { Top = 0, Bottom = 1, Index = 2 }

        [Header("Card is at pile position")]
        public PileType pile = PileType.Deck;
        public PosMode mode = PosMode.Top;
        public int index = 0;                 //Used only when mode == Index
        public ConditionOperatorBool oper;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target)
        {
            if (target == null)
                return CompareBool(false, oper);

            Player player = data.GetPlayer(target.player_id);
            List<Card> list = GetPile(player, pile);
            if (list == null)
                return CompareBool(false, oper);

            int idx = list.IndexOf(target);
            if (idx < 0)
                return CompareBool(false, oper);   //Not in this pile at all

            int pos = mode == PosMode.Top ? 0
                    : mode == PosMode.Bottom ? list.Count - 1
                    : index;

            return CompareBool(idx == pos, oper);
        }

        private List<Card> GetPile(Player player, PileType pile)
        {
            switch (pile)
            {
                case PileType.Deck: return player.cards_deck;
                case PileType.Discard: return player.cards_discard;
                case PileType.Hand: return player.cards_hand;
                case PileType.Board: return player.cards_board;
                case PileType.Secret: return player.cards_secret;
                case PileType.Temp: return player.cards_temp;
                case PileType.Equipped: return player.cards_equip;
                case PileType.Attached: return player.cards_attach;
                case PileType.Club: return player.cards_club;
                case PileType.PlayerAbility: return player.player_ability;
                default: return null;
            }
        }
    }
}
