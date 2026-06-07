using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;
using Unity.VisualScripting;
using TcgEngine.Client;

namespace TcgEngine
{
    //Sends the target card to a pile of your choice (deck/discard/hand)
    //Dont use to send to board since it needs a slot, use EffectPlay instead to send to board
    //Also dont send to discard from the board because it wont trigger OnKill effects, use EffectDestroy instead

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/SendPile", order = 10)]
    public class EffectSendPile : EffectData
    {
        public PileType pile;

        [Header("Deck insert position (only used when pile == Deck)")]
        public DeckInsert insert = DeckInsert.Bottom;

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            Game data = logic.GetGameData();
            Player player = data.GetPlayer(target.player_id);

            if (pile == PileType.Deck)
            {
                player.RemoveCardFromAllGroups(target);
                if (insert == DeckInsert.Top)
                    player.cards_deck.Insert(0, target);   //Top = next card to be drawn
                else
                    player.cards_deck.Add(target);         //Bottom (default, backward compatible)
                target.Clear();
            }

            if (pile == PileType.Hand)
            {
                player.RemoveCardFromAllGroups(target);

                if (player.cards_hand.Count < GameplayData.Get().cards_max)
                {
                    player.cards_hand.Add(target);
                }

                else
                {
                    player.cards_discard.Add(target);

                    logic.onCardDissolved?.Invoke(target, player.player_id);
                }

                target.Clear();
            }

            if (pile == PileType.Discard)
            {
                player.RemoveCardFromAllGroups(target);
                player.cards_discard.Add(target);
                target.Clear();
            }

            if (pile == PileType.Temp)
            {
                player.RemoveCardFromAllGroups(target);
                player.cards_temp.Add(target);
                target.Clear();
            }

            if (pile == PileType.PlayerAbility)
            {
                player.RemoveCardFromAllGroups(target);
                player.player_ability.Add(target);
                target.Clear();
            }
        }
    }

    public enum DeckInsert
    {
        Bottom = 0,   //Added to the end of the deck (bottom)
        Top = 1,      //Inserted at index 0 (top, next to be drawn)
    }

    public enum PileType
    {
        None = 0,
        Club = 5,
        Board = 10,
        Hand = 20,
        Deck = 30,
        Discard = 40,
        Secret = 50,
        Equipped = 60,
        Attached = 70,
        Temp = 90,
        PlayerAbility = 100,
    }

}
