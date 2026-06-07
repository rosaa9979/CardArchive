using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Moves the TOP card of a pile to the BOTTOM of the same pile (the caster's owner).
    /// Target-independent: the chosen/target card is ignored. Useful for "send the next
    /// card to the bottom" effects where the selected card is a proxy/token rather than
    /// the card that actually gets moved.
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/MovePileTopToBottom", order = 10)]
    public class EffectMovePileTopToBottom : EffectData
    {
        public PileType pile = PileType.Deck;

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster)
        {
            Move(logic, caster);
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            Move(logic, caster);   //target ignored on purpose
        }

        private void Move(GameLogic logic, Card caster)
        {
            Player player = logic.GetGameData().GetPlayer(caster.player_id);
            List<Card> list = GetPile(player, pile);
            if (list == null || list.Count < 2)
                return;   //Nothing to reorder

            Card top = list[0];
            list.RemoveAt(0);
            list.Add(top);
        }

        private List<Card> GetPile(Player player, PileType pile)
        {
            switch (pile)
            {
                case PileType.Deck: return player.cards_deck;
                case PileType.Discard: return player.cards_discard;
                case PileType.Hand: return player.cards_hand;
                default: return null;
            }
        }
    }
}
