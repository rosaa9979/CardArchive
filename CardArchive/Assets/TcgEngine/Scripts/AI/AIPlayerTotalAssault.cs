using System.Collections;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine.AI
{
    /// <summary>
    /// AI used for the boss player in Total Assault matches. Inherits directly
    /// from AIPlayer (no minimax) — boss behavior is intended to be driven by
    /// custom gauge / pattern logic that lives entirely in this class.
    ///
    /// Currently implements only the groggy-skip hook. Actual boss action
    /// selection (which card to play, when, based on gauges/phase) is a TODO
    /// to be filled in as the boss design solidifies.
    /// </summary>
    public class AIPlayerTotalAssault : AIPlayer
    {
        private bool is_playing = false;

        public AIPlayerTotalAssault(GameLogic gameplay, int id, int level)
        {
            this.gameplay = gameplay;
            player_id = id;
            ai_level = Mathf.Clamp(level, 1, 10);
        }

        public override void Update()
        {
            if (!CanPlay())
                return;

            //Groggy: skip our turn (still received start-of-turn resources from StartTurn).
            if (TryConsumeGroggySkip())
                return;

            //Normal turn — kick off boss decision sequence (single in-flight at a time).
            if (!is_playing)
            {
                is_playing = true;
                TimeTool.StartCoroutine(BossTurn());
            }
        }

        private bool TryConsumeGroggySkip()
        {
            Game game_data = gameplay.GetGameData();
            BossState state = game_data.boss_state;
            if (state == null)
                return false;
            if (player_id != state.player_id)
                return false;
            if (!state.skip_next_turn)
                return false;

            Player player = game_data.GetPlayer(player_id);
            if (!game_data.IsPlayerTurn(player) || gameplay.IsResolving())
                return false;

            state.skip_next_turn = false;
            EndTurn();
            return true;
        }

        private IEnumerator BossTurn()
        {
            yield return new WaitForSeconds(1f);

            //TODO: boss action selection driven by skill / atg gauges + phase.
            //For now, the boss takes no actions on its normal turn — replace
            //this with pattern logic when the boss design is filled in.

            EndTurn();

            yield return new WaitForSeconds(0.5f);
            is_playing = false;
        }

        private void EndTurn()
        {
            if (CanPlay())
                gameplay.StartAttackPhase();
        }
    }
}
