using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TcgEngine
{
    public class HeadlessSimulator : MonoBehaviour
    {
        [Header("Run")]
        [SerializeField] private int num_games = 10;
        [SerializeField] private int max_turns = 50;

        [Header("Players")]
        [SerializeField] private SimPlayerConfig player0 = new SimPlayerConfig { username = "P0" };
        [SerializeField] private SimPlayerConfig player1 = new SimPlayerConfig { username = "P1" };

        private IEnumerator Start()
        {
            // Wait one frame so DataLoader.Awake (in the same scene) finishes.
            yield return null;

            if (DeckData.GetAll().Count == 0)
            {
                Debug.LogError("[HeadlessSim] No DeckData loaded. Add a DataLoader to this scene.");
                Quit(1);
                yield break;
            }

            SimConfig cfg = BuildConfig();
            SimConfig.ApplyCLI(cfg, Environment.GetCommandLineArgs());

            Debug.Log(string.Format(
                "[HeadlessSim] Starting {0} games (max_turns={1})  |  P0={2}:{3} deck={4}  vs  P1={5}:{6} deck={7}",
                cfg.num_games, cfg.max_turns,
                cfg.player0.ai_type, cfg.player0.ai_level, DeckIdOf(cfg.player0),
                cfg.player1.ai_type, cfg.player1.ai_level, DeckIdOf(cfg.player1)));

            int p0_wins = 0, p1_wins = 0, draws = 0;
            Stopwatch sw = Stopwatch.StartNew();

            for (int i = 0; i < cfg.num_games; i++)
            {
                int winner = -1;
                long t0 = sw.ElapsedMilliseconds;
                Exception err = null;

                try
                {
                    HeadlessGame game = new HeadlessGame(cfg, i);
                    winner = game.RunOneGame();
                }
                catch (Exception ex)
                {
                    err = ex;
                }

                if (err != null)
                {
                    Debug.LogError("[HeadlessSim] Game " + i + " threw: " + err);
                    draws++;
                }
                else if (winner == 0) p0_wins++;
                else if (winner == 1) p1_wins++;
                else draws++;

                Debug.Log(string.Format("[HeadlessSim] Game {0}/{1}  winner={2}  elapsed={3}ms",
                    i + 1, cfg.num_games, winner, sw.ElapsedMilliseconds - t0));

                yield return null; // yield each game so we don't hog the main thread
            }

            sw.Stop();
            Debug.Log(string.Format("[HeadlessSim] Done.  P0:{0}  P1:{1}  Draws:{2}  Total:{3}ms  Avg:{4}ms",
                p0_wins, p1_wins, draws, sw.ElapsedMilliseconds,
                cfg.num_games > 0 ? sw.ElapsedMilliseconds / cfg.num_games : 0));

            Quit(0);
        }

        private SimConfig BuildConfig()
        {
            return new SimConfig
            {
                num_games = num_games,
                max_turns = max_turns,
                player0 = player0,
                player1 = player1,
            };
        }

        private static string DeckIdOf(SimPlayerConfig p)
        {
            if (!string.IsNullOrEmpty(p.deck_id)) return p.deck_id;
            return p.deck_asset != null ? p.deck_asset.id : "(none)";
        }

        private static void Quit(int code)
        {
            if (Application.isBatchMode)
                Application.Quit(code);
        }
    }
}
