using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine.AI
{
    public class AIPlayerRandomSync : AIPlayer, ISyncAIPlayer
    {
        private const int CardsPerTurn = 3;
        private const int SelectorSafety = 10;
        private const int SettleSafety = 1000;

        private System.Random rand = new System.Random();

        public AIPlayerRandomSync(GameLogic gameplay, int id, int level)
        {
            this.gameplay = gameplay;
            player_id = id;
            ai_level = level;
        }

        public void PlaySync()
        {
            Game game_data = gameplay.GetGameData();
            Player player = game_data.GetPlayer(player_id);

            if (game_data.IsPlayerMulliganTurn(player) && !player.ready)
            {
                gameplay.Mulligan(player, new string[0]);
                Settle();
                return;
            }

            if (!CanPlay() || !game_data.IsPlayerTurn(player))
                return;

            DrainSelector();

            for (int i = 0; i < CardsPerTurn; i++)
            {
                if (!CanPlay() || !game_data.IsPlayerActionTurn(player))
                    break;
                PlayRandomCard();
                Settle();
                DrainSelector();
            }

            if (CanPlay() && game_data.IsPlayerActionTurn(player))
            {
                gameplay.StartAttackPhase();
                Settle();
            }
        }

        private void DrainSelector()
        {
            Game game_data = gameplay.GetGameData();
            int s = SelectorSafety;
            while (s-- > 0 && CanPlay()
                   && game_data.selector != SelectorType.None
                   && game_data.selector_player_id == player_id)
            {
                if (game_data.selector == SelectorType.SelectorCard)
                    SelectRandomCard();
                else if (game_data.selector == SelectorType.SelectTarget)
                    SelectRandomTarget();
                else if (game_data.selector == SelectorType.SelectorChoice)
                    SelectRandomChoice();
                else
                    gameplay.CancelSelection();

                Settle();
            }
        }

        private void PlayRandomCard()
        {
            Game game_data = gameplay.GetGameData();
            Player player = game_data.GetPlayer(player_id);
            if (player.cards_hand.Count == 0)
                return;

            Card card = player.GetRandomCard(player.cards_hand, rand);
            if (card == null)
                return;

            Slot slot = player.GetRandomEmptySlot(rand);

            if (card.CardData.IsPlace())
                slot = player.GetRandomEmptyOutsideSlots(rand);
            else if (card.CardData.IsRequireTargetSpell())
                slot = game_data.GetRandomSlot(rand);
            else if (card.CardData.IsEquipment())
                slot = player.GetRandomOccupiedSlot(rand);

            gameplay.PlayCard(card, slot);
        }

        private void SelectRandomCard()
        {
            Game game_data = gameplay.GetGameData();
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);
            Card caster = game_data.GetCard(game_data.selector_caster_uid);
            if (ability == null || caster == null)
            {
                gameplay.CancelSelection();
                return;
            }

            List<Card> list = ability.GetCardTargets(game_data, caster);
            if (list != null && list.Count > 0)
                gameplay.SelectCard(list[rand.Next(0, list.Count)]);
            else
                gameplay.CancelSelection();
        }

        private void SelectRandomTarget()
        {
            Game game_data = gameplay.GetGameData();
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);
            if (ability != null && ability.criteria_target == AbilityTarget.SelectTarget)
            {
                Slot s = Slot.GetRandom(rand);
                if (s != Slot.None)
                {
                    gameplay.SelectSlot(s);
                    return;
                }
            }
            gameplay.CancelSelection();
        }

        private void SelectRandomChoice()
        {
            Game game_data = gameplay.GetGameData();
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);
            if (ability != null && ability.chain_abilities != null && ability.chain_abilities.Length > 0)
                gameplay.SelectChoice(rand.Next(0, ability.chain_abilities.Length));
            else
                gameplay.CancelSelection();
        }

        private void Settle()
        {
            int s = SettleSafety;
            while (s-- > 0 && gameplay.IsResolving())
                gameplay.Update(1f);
        }
    }
}
