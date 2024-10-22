using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effect that damages a card or a player (lose hp)
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/StoreCount", order = 10)]
    public class EffectStoreCount : EffectData
    {
        [Header("Count Traits")]
        public PileType pile;
        public EffectPlayerType player;

        [Space(10)]
        public CardType has_type;
        //public TeamData has_club;
        public ClubData has_club;
        public TraitData has_trait;

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            int val = GetCount(logic.GetGameData(), caster);

            Debug.Log(val);

            target.AddStatus(StatusType.StoreValue, val, 0);
        }

        private int GetCount(Game data, Card caster)
        {
            Player p = data.GetPlayer(caster.player_id);
            Player op = data.GetOpponentPlayer(p.player_id);

            int val = 0;

            if (player == EffectPlayerType.Player || player == EffectPlayerType.All)
                val += CountPile(p, pile);
            if (player == EffectPlayerType.Opponent || player == EffectPlayerType.All)
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