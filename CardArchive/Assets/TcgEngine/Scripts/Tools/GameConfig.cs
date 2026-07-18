using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Project-wide constants and tuning values, grouped by domain.
    /// Single source of truth: code reads these directly instead of per-asset serialized fields.
    /// </summary>

    public static class GameConfig
    {
        /// <summary>
        /// Pointer gesture rules, identical on PC and mobile:
        /// hold still to preview, move to drag/scrub, release to confirm.
        /// All preview-style UI must read these values instead of defining its own.
        /// </summary>
        public static class Gesture
        {
            public const float preview_delay = 0.4f;    //Hold (or hover) duration before a preview appears
            public const float dead_zone = 20f;         //Screen pixels a press may move and still count as a tap
            public const float hand_drag_line = 0.15f;  //Screen height percent above which a hand card press becomes a drag

            //A tap is a short press that stayed within the dead zone
            public static bool IsTap(float press_duration, Vector2 press_start, Vector2 press_now)
            {
                return press_duration < preview_delay && (press_now - press_start).magnitude <= dead_zone;
            }
        }

        /// <summary>
        /// Resolve/pacing delays in seconds, used by ResolveQueue and GameLogic.
        /// AI prediction runs with skip_delay=true and ignores all of these.
        /// </summary>
        public static class Timing
        {
            //--- Resolve queue per-step defaults (gap before the NEXT queued element of each type) ---
            public const float ability = 1f;            //ability_queue: spacing between chained ability effects (연출)
            public const float secret = 1f;             //secret_queue
            public const float attack = 0.2f;           //attack_queue: rhythm of combat micro-steps
            public const float callback = 0.1f;         //callback_queue: transition callbacks

            //--- Phase / turn transitions ---
            public const float game_start = 1f;             //GameStart -> starting hands/mulligan
            public const float first_turn = 1f;             //Mulligan/GameStart -> first StartTurn
            public const float mulligan_to_turn = 4f;       //Mulligan done -> first turn (client handoff buffer)
            public const float turn_start = 1.5f;           //StartTurn -> BeforeMainPhase
            public const float pre_main_phase = 0.2f;       //BeforeMainPhase -> Draw/Main
            public const float attack_phase_start = 1.5f;   //StartAttackPhase -> first AttackCheck
            public const float turn_end = 0.2f;             //EndTurn -> StartNextTurn

            //--- Attack loop ---
            public const float between_attackers = 1f;      //AttackSearch -> next AttackCheck (gap between attackers)
            public const float attack_phase_end = 0.1f;     //no more attackers -> EndTurn

            //--- Attack resolve chain (card vs card) ---
            public const float attack_step = 0.05f;         //AttackTargets/AttackTarget/ResolveAttack/ResolveDeath steps
            public const float attack_hit = 0.2f;           //ResolveAttackHit -> ResolveDeath

            //--- Attack resolve chain (card vs player) ---
            public const float attack_player_step = 0.2f;   //AttackPlayer chain steps

            //--- Card actions ---
            public const float play_card = 0.3f;            //after summon/play card
            public const float move_card = 0.2f;            //after move card
            public const float ability_resolve = 0.2f;      //after an ability resolves
        }
    }
}
