using UnityEngine;
using TcgEngine.AI;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    public class HeadlessGame
    {
        private const int SettleSafety = 1000;
        private const int TurnLoopSafety = 4;

        private Game game_data;
        private GameLogic gameplay;
        private ISyncAIPlayer ai0;
        private ISyncAIPlayer ai1;
        private int max_turns;

        public HeadlessGame(SimConfig cfg, int game_id)
        {
            max_turns = cfg.max_turns;

            game_data = new Game("sim_" + game_id, 2);
            gameplay = new GameLogic(true); // skip_delay = true (immediate resolve)
            gameplay.SetData(game_data);

            SetupPlayer(0, cfg.player0);
            SetupPlayer(1, cfg.player1);

            ai0 = CreateAI(cfg.player0, 0);
            ai1 = CreateAI(cfg.player1, 1);
        }

        private void SetupPlayer(int idx, SimPlayerConfig pcfg)
        {
            Player p = game_data.players[idx];
            p.username = pcfg.username;
            p.is_ai = true;
            p.ai_level = pcfg.ai_level;
            p.ready = true;

            DeckData deck = ResolveDeck(pcfg);
            if (deck == null)
                throw new System.Exception("[HeadlessGame] No deck for player " + idx +
                                           " (deck_id='" + pcfg.deck_id + "', deck_asset=" +
                                           (pcfg.deck_asset != null ? pcfg.deck_asset.name : "null") + ")");

            gameplay.SetPlayerDeck(p, deck);
        }

        private static DeckData ResolveDeck(SimPlayerConfig pcfg)
        {
            if (!string.IsNullOrEmpty(pcfg.deck_id))
            {
                DeckData d = DeckData.Get(pcfg.deck_id);
                if (d != null) return d;
            }
            return pcfg.deck_asset;
        }

        private ISyncAIPlayer CreateAI(SimPlayerConfig pcfg, int id)
        {
            if (pcfg.ai_type == SimAIType.Random)
                return new AIPlayerRandomSync(gameplay, id, pcfg.ai_level);
            return new AIPlayerMMSync(gameplay, id, pcfg.ai_level);
        }

        // Returns the winner player_id (0 or 1), or -1 if truncated/draw.
        public int RunOneGame()
        {
            gameplay.StartGame();
            Settle();

            // Auto-mulligan: keep all
            if (game_data.phase == GamePhase.Mulligan)
            {
                foreach (Player p in game_data.players)
                    gameplay.Mulligan(p, new string[0]);
                Settle();
            }

            int safety = max_turns * TurnLoopSafety;
            while (safety-- > 0 && !game_data.HasEnded())
            {
                if (game_data.turn_count > max_turns)
                    break;

                ISyncAIPlayer ai = game_data.current_player == 0 ? ai0 : ai1;
                ai.PlaySync();
                Settle();
            }

            // EndGame() in GameLogic sets current_player = winner.
            return game_data.HasEnded() ? game_data.current_player : -1;
        }

        private void Settle()
        {
            int s = SettleSafety;
            while (s-- > 0 && gameplay.IsResolving())
                gameplay.Update(1f);
        }
    }
}
