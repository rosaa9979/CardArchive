using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// SlotRange check each axis variable individualy for range between the caster and target
    /// If you want to check the travel distance instead (all at once) use SlotDist
    /// </summary>
    /// 

    [CreateAssetMenu(fileName = "repeat", menuName = "TcgEngine/RepeatCondition/CountType", order = 11)]
    public class RepeatCountType : RepeatConditionData
    {
        [Header("Count Traits")]
        public PileType pile;
        public ConditionPlayerType player;

        [Space(10)]
        public CardType has_type;
        //public TeamData has_club;
        public ClubData has_club;
        public TraitData has_trait;

        public override int GetMaxRepeatTimes(Game data, AbilityData ability, Card caster)
        {
            return GetCount(data, caster);
        }
        
        public override bool IsRepeatConditionMet(Game data, AbilityData ability, int max_repeat_times, int repeat_times)
        {
            return true;
        }

        public override bool IsOngoingRepeatConditionMet(Game data, AbilityData ability, int max_repeat_times, int repeat_times)
        { 
            Debug.Log(max_repeat_times);
            if (repeat_times < max_repeat_times)
                return true;

            return false;
        }

        private int GetCount(Game data, Card caster)
        {
            Player p = data.GetPlayer(caster.player_id);
            Player op = data.GetOpponentPlayer(p.player_id);

            int val = 0;

            if (player == ConditionPlayerType.Self || player == ConditionPlayerType.Both)
                val += CountPile(p, pile);
            if (player == ConditionPlayerType.Opponent || player == ConditionPlayerType.Both)
                val += CountPile(op, pile);

            return val;
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
            bool is_type = card.CardData.type == has_type || has_type == CardType.None;
            bool is_club = card.HasClub(has_club) || has_club == null;
            bool is_trait = card.HasTrait(has_trait) || has_trait == null;
            //return (is_type && is_team && is_trait);
            return (is_type && is_club && is_trait);
        }
    }
}