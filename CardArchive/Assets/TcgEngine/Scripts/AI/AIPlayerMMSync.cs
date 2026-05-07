using System.Threading;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine.AI
{
    public interface ISyncAIPlayer
    {
        void PlaySync();
    }

    public class AIPlayerMMSync : AIPlayer, ISyncAIPlayer
    {
        private const int MaxActionsPerTurn = 50;
        private const int SettleSafety = 1000;

        private AILogic ai_logic;

        public AIPlayerMMSync(GameLogic gameplay, int id, int level)
        {
            this.gameplay = gameplay;
            player_id = id;
            ai_level = Mathf.Clamp(level, 1, 10);
            ai_logic = AILogic.Create(id, ai_level);
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

            if (!game_data.IsPlayerTurn(player))
                return;

            int actions = MaxActionsPerTurn;
            while (actions-- > 0 && !game_data.HasEnded() && CanPlay() && game_data.IsPlayerTurn(player))
            {
                ai_logic.RunAI(game_data);
                while (ai_logic.IsRunning())
                    Thread.Sleep(0);

                AIAction best = ai_logic.GetBestAction();
                ai_logic.ClearMemory();

                if (best == null)
                    break;

                bool turn_done = ExecuteAction(best);
                Settle();
                if (turn_done)
                    return;
            }

            // Safety: force end turn if loop exhausted while still our turn
            if (CanPlay() && game_data.IsPlayerTurn(player))
            {
                gameplay.StartAttackPhase();
                Settle();
            }
        }

        private bool ExecuteAction(AIAction action)
        {
            Game game_data = gameplay.GetGameData();

            if (action.type == GameAction.PlayCard)
            {
                Card card = game_data.GetCard(action.card_uid);
                if (card != null)
                    gameplay.PlayCard(card, action.slot);
                return false;
            }

            if (action.type == GameAction.Move)
            {
                Card card = game_data.GetCard(action.card_uid);
                if (card != null)
                    gameplay.MoveCard(card, action.slot);
                return false;
            }

            if (action.type == GameAction.CastAbility)
            {
                Card caster = game_data.GetCard(action.card_uid);
                AbilityData ability = AbilityData.Get(action.ability_id);
                if (caster != null && ability != null)
                    gameplay.CastAbility(caster, ability);
                return false;
            }

            if (action.type == GameAction.SelectCard)
            {
                Card target = game_data.GetCard(action.target_uid);
                if (target != null)
                    gameplay.SelectCard(target);
                return false;
            }

            if (action.type == GameAction.SelectPlayer)
            {
                Player target = game_data.GetPlayer(action.target_player_id);
                if (target != null)
                    gameplay.SelectPlayer(target);
                return false;
            }

            if (action.type == GameAction.SelectSlot)
            {
                if (action.slot != Slot.None)
                    gameplay.SelectSlot(action.slot);
                return false;
            }

            if (action.type == GameAction.SelectChoice)
            {
                gameplay.SelectChoice(action.value);
                return false;
            }

            if (action.type == GameAction.CancelSelect)
            {
                gameplay.CancelSelection();
                return false;
            }

            if (action.type == GameAction.EndTurn)
            {
                if (CanPlay())
                    gameplay.StartAttackPhase();
                return true;
            }

            if (action.type == GameAction.Resign)
            {
                int other = player_id == 0 ? 1 : 0;
                gameplay.EndGame(other);
                return true;
            }

            // Attack / AttackPlayer: in this engine attacks are handled
            // automatically during the Attack phase, so these are no-ops
            // (matches existing AIPlayerMM behavior).
            return false;
        }

        private void Settle()
        {
            int s = SettleSafety;
            while (s-- > 0 && gameplay.IsResolving())
                gameplay.Update(1f);
        }
    }
}
