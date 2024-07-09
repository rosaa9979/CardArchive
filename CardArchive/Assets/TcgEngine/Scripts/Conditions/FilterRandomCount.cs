using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    //Pick X number of targets at random from the source array (count)

    [CreateAssetMenu(fileName = "filter", menuName = "TcgEngine/Filter/RandomCount", order = 10)]
    public class FilterRandomCount : FilterData
    {
        //public int amount = 1; //Number of random targets selected
        public PileType pile;
        public bool player_self;

        [Header("Count Traits")]
        public CardType has_type;
        //public TeamData has_club;
        public ClubData has_club;
        public TraitData has_trait;

        public override List<Card> FilterTargets(Game data, AbilityData ability, Card caster, List<Card> source, List<Card> dest)
        {
            return GameTool.PickXRandom(source, dest, GetCount(data, caster));
        }

        public override List<Player> FilterTargets(Game data, AbilityData ability, Card caster, List<Player> source, List<Player> dest)
        {
            return GameTool.PickXRandom(source, dest, GetCount(data, caster));
        }

        public override List<Slot> FilterTargets(Game data, AbilityData ability, Card caster, List<Slot> source, List<Slot> dest)
        {
            return GameTool.PickXRandom(source, dest, GetCount(data, caster));
        }

        public override List<CardData> FilterTargets(Game data, AbilityData ability, Card caster, List<CardData> source, List<CardData> dest)
        {
            return GameTool.PickXRandom(source, dest, GetCount(data, caster));
        }

        private int GetCount(Game data, Card caster)
        {
            Player player = data.GetPlayer(caster.player_id);
            Player oplayer = data.GetOpponentPlayer(player.player_id);

            if (player_self)
                return CountPile(player, pile);

            return CountPile(oplayer, pile);
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
