using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effects that creates a new card from a CardData
    /// Use for discover effects
    /// Use AbiliityTarget None
    /// Unlike EffectSummon, this effect targets the card data you want to create, and goes into the pile selected here
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/CreateCard", order = 10)]
    public class EffectCreateCard : EffectData
    {
        public List<WeightedCard> create_card;
        public PileType create_pile;   //Better to not select Board here, for placing a card on board or in secret area, would suggest instead EffectSummon, or EffectPlay as chain after Create
        public bool is_same_possibility;
        public bool create_opponent;       //Add to opponent?

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster)
        {
            Player player = logic.GameData.GetPlayer(caster.player_id);
            if (create_opponent)
                player = logic.GameData.GetOpponentPlayer(caster.player_id);

            CardData selected_card = GetRandomCardData();
            Card card = Card.Create(selected_card, caster.VariantData, player);
            logic.GameData.last_summoned = card.uid;
            
            if (create_pile == PileType.Deck)
                player.cards_deck.Add(card);

            if (create_pile == PileType.Discard)
                player.cards_discard.Add(card);

            if (create_pile == PileType.Hand)
                player.cards_hand.Add(card);

            if (create_pile == PileType.Temp)
                player.cards_temp.Add(card);

            if (create_pile == PileType.PlayerAbility)
                player.player_ability.Add(card);
        }

        public CardData GetRandomCardData()
        {
            if (is_same_possibility)
            {
                int random = UnityEngine.Random.Range(0, create_card.Count);

                return create_card[random].card;
            }

            else
            {
                float current = 0f;
                float random = UnityEngine.Random.Range(0.0f, 1.0f);

                foreach(var candidate in create_card)
                {
                    current += candidate.weight;

                    if (random <= current)
                        return candidate.card;
                }

                return create_card[0].card;
            }
        }

        [System.Serializable]
        public struct WeightedCard
        {
            public CardData card;
            public float weight;
        }
    }
}