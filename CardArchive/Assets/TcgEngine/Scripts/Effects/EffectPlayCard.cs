using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effect to play a card from your hand for free
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/PlayCard", order = 10)]
    public class EffectPlayCard : EffectData
    {
        public CardData play_card;

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Slot target)
        {
            Game game = logic.GetGameData();
            Player player = game.GetPlayer(caster.player_id);
            Card card = Card.Create(play_card, caster.VariantData, player);

            player.RemoveCardFromAllGroups(card);
            player.cards_hand.Add(card);

            if (target != Slot.None)
            {
                logic.PlayCard(card, target, true);
            }
        }
    }
}