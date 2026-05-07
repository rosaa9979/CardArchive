using System;
using UnityEngine;

namespace TcgEngine
{
    public enum SimAIType
    {
        Random = 0,
        MiniMax = 1,
    }

    [Serializable]
    public class SimPlayerConfig
    {
        public string username = "Player";
        public DeckData deck_asset;
        public string deck_id = "";
        public SimAIType ai_type = SimAIType.MiniMax;
        [Range(1, 10)] public int ai_level = 5;
    }

    public class SimConfig
    {
        public int num_games = 10;
        public int max_turns = 50;
        public SimPlayerConfig player0;
        public SimPlayerConfig player1;

        public static void ApplyCLI(SimConfig cfg, string[] args)
        {
            if (args == null) return;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                string next = (i + 1 < args.Length) ? args[i + 1] : null;

                if (a == "--sim-games" && int.TryParse(next, out int g))
                    cfg.num_games = Mathf.Max(1, g);
                else if (a == "--sim-max-turns" && int.TryParse(next, out int mt))
                    cfg.max_turns = Mathf.Max(1, mt);
                else if (a == "--sim-ai0")
                    ParseAI(next, cfg.player0);
                else if (a == "--sim-ai1")
                    ParseAI(next, cfg.player1);
                else if (a == "--sim-deck0" && next != null)
                    cfg.player0.deck_id = next;
                else if (a == "--sim-deck1" && next != null)
                    cfg.player1.deck_id = next;
                else if (a == "--sim-name0" && next != null)
                    cfg.player0.username = next;
                else if (a == "--sim-name1" && next != null)
                    cfg.player1.username = next;
            }
        }

        private static void ParseAI(string spec, SimPlayerConfig pcfg)
        {
            if (string.IsNullOrEmpty(spec) || pcfg == null) return;

            string[] parts = spec.Split(':');
            string type = parts[0].ToLowerInvariant();
            int level = pcfg.ai_level;
            if (parts.Length > 1) int.TryParse(parts[1], out level);

            if (type == "random")
                pcfg.ai_type = SimAIType.Random;
            else if (type == "mm" || type == "minimax")
                pcfg.ai_type = SimAIType.MiniMax;

            pcfg.ai_level = Mathf.Clamp(level, 1, 10);
        }
    }
}
