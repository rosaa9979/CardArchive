using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace TcgEngine
{
    public enum ConditionPlayerType
    {
        Self = 0,
        Opponent = 1,
        Both = 2,
    }

    /// <summary>
    /// Trigger condition that count the amount of cards in pile of your choise (deck/discard/hand/board...)
    /// Can also only count cards of a specific type/team/trait
    /// 리스트 항목은 모두 or 계산, 즉 리스트 중 하나라도 있으면 카운트한다.
    /// </summary>

    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Count", order = 10)]
    public class ConditionCount : ConditionData
    {
        [Header("Count cards of type")]
        public ConditionPlayerType target;
        public PileType pile;
        public ConditionOperatorInt oper;
        public int value;

        [Header("Traits")]
        public List<CardType> has_type;
        //public TeamData has_team;
        public List<ClubData> has_club;
        public List<TraitData> has_trait;
        public List<CardData> has_card;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            int count = 0;
            if (target == ConditionPlayerType.Self || target == ConditionPlayerType.Both)
            {
                Player player = data.GetPlayer(caster.player_id);
                count += CountPile(player, pile);
            }
            if (target == ConditionPlayerType.Opponent || target == ConditionPlayerType.Both)
            {
                Player player = data.GetOpponentPlayer(caster.player_id);
                count += CountPile(player, pile);
            }
            return CompareInt(count, oper, value);
        }

        private int CountPile(Player player, PileType pile)
        {
            List<Card> card_pile = null;

            if (pile == PileType.Hand)
                card_pile = player.cards_hand;

            if (pile == PileType.Board)
                card_pile = player.cards_board;

            if (pile == PileType.Equipped)
                card_pile = player.cards_equip;

            if (pile == PileType.Deck)
                card_pile = player.cards_deck;

            if (pile == PileType.Discard)
                card_pile = player.cards_discard;

            if (pile == PileType.Secret)
                card_pile = player.cards_secret;

            if (pile == PileType.Temp)
                card_pile = player.cards_temp;

            if (card_pile != null)
            {
                int count = 0;
                foreach (Card card in card_pile)
                {
                    if (IsTrait(card))
                        count++;
                }
                return count;
            }
            return 0;
        }

        private bool IsTrait(Card card)
        {
            bool is_type = has_type.Contains(card.CardData.type) || has_type.Count == 0;
            //bool is_type = card.CardData.type == has_type || has_type == CardType.None;
            bool is_club = has_club.Any(clubData => card.GetAllClubs().Any(club => club.ClubData == clubData)) || has_club.Count == 0;
            //bool is_team = card.CardData.team == has_team || has_team == null;
            bool is_trait = has_trait.Any(traitData => card.GetAllTraits().Any(club => club.TraitData == traitData)) || has_trait.Count == 0;
            //return (is_type && is_team && is_trait);
            bool is_card = has_card.Any(cardId => card.card_id == cardId.id) || has_card.Count == 0;
            return (is_type && is_club && is_trait && is_card);
        }
    }
}