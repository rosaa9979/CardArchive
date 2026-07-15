using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.AI;

namespace TcgEngine
{
    /// <summary>
    /// Generic gameplay settings, such as starting stats, decks limit, scenes, and ai level
    /// </summary>

    [CreateAssetMenu(fileName = "GameplayData", menuName = "TcgEngine/GameplayData", order = 0)]
    public class GameplayData : ScriptableObject
    {
        [Header("Gameplay")]
        public int hp_start = 20;
        public int mana_start = 1;
        public int mana_per_turn = 1;
        public int mana_max = 10;
        public int cards_start = 5;
        public int cards_per_turn = 1;
        public int cards_max = 10;
        public float turn_duration = 30f;
        public CardData second_bonus;
        public bool mulligan;

        [Header("Deckbuilding")]
        public int deck_size = 20;
        public int club_size = 3;
        public int deck_non_student_duplicate_max = 2;
        public int deck_student_duplicate_max = 1;

        [Header("Buy/Sell")]
        public float sell_ratio = 0.8f;

        [Header("AI")]
        public AIType tutorial_ai_type;
        public AIType ai_type;              //AI algorythm
        public int ai_level = 10;           //AI level, 10=best, 1=weakest

        [Header("Decks")]
        public DeckData[] free_decks;       //These decks are always available in menu, useful for tests
        public DeckData[] starter_decks;    //When API is enabled, each player can select ONE of those
        public DeckData[] ai_decks;         //When player solo, AI will pick one of these at random

        [Header("Scenes")]
        public string[] arena_list;         //List of game scenes

        [Header("Test")]
        public DeckData test_deck;          //For when starting the game directly from Unity game scene
        public DeckData test_deck_ai;       //For when starting the game directly from Unity game scene
        public bool ai_vs_ai;

        [Header("Timing")]
        public TimingData timing = new TimingData(); //All resolve/pacing delays, centralized here

        public int GetPlayerLevel(int xp)
        {
            return Mathf.FloorToInt(xp / 1000f) + 1;
        }

        public string GetRandomArena()
        {
            if (arena_list.Length > 0)
                return arena_list[Random.Range(0, arena_list.Length)];
            return "Game";
        }

        public string GetRandomAIDeck()
        {
            if (ai_decks.Length > 0)
                return ai_decks[Random.Range(0, ai_decks.Length)].id;
            return "";
        }

        public static GameplayData Get()
        {
            return DataLoader.Get().data;
        }
    }

    /// <summary>
    /// Centralized resolve/pacing delays (seconds). Edited in the GameplayData asset.
    /// AI prediction runs with skip_delay=true and ignores all of these.
    /// </summary>
    [System.Serializable]
    public class TimingData
    {
        [Header("Resolve queue per-step defaults (gap before the NEXT queued element of each type)")]
        public float ability = 1f;          //ability_queue: spacing between chained ability effects (연출)
        public float secret = 1f;           //secret_queue
        public float attack = 0.2f;         //attack_queue: rhythm of combat micro-steps
        public float callback = 0.1f;       //callback_queue: transition callbacks

        [Header("Phase / turn transitions")]
        public float game_start = 1f;           //GameStart -> starting hands/mulligan
        public float first_turn = 1f;           //Mulligan/GameStart -> first StartTurn
        public float mulligan_to_turn = 4f;     //Mulligan done -> first turn (client handoff buffer)
        public float turn_start = 1.5f;         //StartTurn -> BeforeMainPhase
        public float pre_main_phase = 0.2f;     //BeforeMainPhase -> Draw/Main
        public float attack_phase_start = 1.5f; //StartAttackPhase -> first AttackCheck
        public float turn_end = 0.2f;           //EndTurn -> StartNextTurn

        [Header("Attack loop")]
        public float between_attackers = 1f;    //AttackSearch -> next AttackCheck (gap between attackers)
        public float attack_phase_end = 0.1f;   //no more attackers -> EndTurn

        [Header("Attack resolve chain (card vs card)")]
        public float attack_step = 0.05f;       //AttackTargets/AttackTarget/ResolveAttack/ResolveDeath steps
        public float attack_hit = 0.2f;         //ResolveAttackHit -> ResolveDeath

        [Header("Attack resolve chain (card vs player)")]
        public float attack_player_step = 0.2f; //AttackPlayer chain steps

        [Header("Card actions")]
        public float play_card = 0.3f;          //after summon/play card
        public float move_card = 0.2f;          //after move card
        public float ability_resolve = 0.2f;    //after an ability resolves
    }
}