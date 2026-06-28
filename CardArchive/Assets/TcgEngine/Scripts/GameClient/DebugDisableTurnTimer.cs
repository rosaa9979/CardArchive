#if UNITY_EDITOR
using UnityEngine;
using TcgEngine;

namespace TcgEngine.Client
{
    /// <summary>
    /// EDITOR-ONLY. Drop this on the debug scene to disable the turn timer for games run there.
    ///
    /// The authoritative timer lives server-side: GameServer.Update counts down game_data.turn_timer
    /// and calls NextStep() at 0, and turn_timer is reset to GameplayData.turn_duration each turn.
    /// So we neutralize it by raising turn_duration to a huge value while this scene is loaded
    /// (GameUI also hides the on-screen counter once turn_timer >= 999), then restore it on exit.
    ///
    /// NOTE: GameplayData is a ScriptableObject; runtime edits to it persist after exiting Play Mode
    /// in the editor. We capture the original value once (the first frame GameplayData is available)
    /// and restore it on destroy, so a normal play/stop cycle leaves the asset untouched.
    ///
    /// The whole file is wrapped in #if UNITY_EDITOR so it is stripped from regular builds.
    /// </summary>
    public class DebugDisableTurnTimer : MonoBehaviour
    {
        private const float kDisabledDuration = 100000f; //~27h, effectively no timer
        private const float kFallbackDuration = 30f;     //Used if the asset is already in the disabled state

        private float original_duration;
        private bool applied;

        void Update()
        {
            //Enforce every frame instead of once in Awake: GameplayData/DataLoader may not be ready at
            //Awake (undefined script order), and turn_duration is only read at each turn's start. Setting it
            //continuously from frame 0 reliably wins the race before the game's first StartTurn.
            GameplayData gd = GameplayData.Get();
            if (gd == null)
                return; //DataLoader not ready yet; try again next frame

            if (!applied)
            {
                original_duration = gd.turn_duration;
                if (original_duration >= 999f) //Asset was left in the disabled state from a previous run
                    original_duration = kFallbackDuration;
                applied = true;
            }

            if (!Mathf.Approximately(gd.turn_duration, kDisabledDuration))
                gd.turn_duration = kDisabledDuration; //Re-assert / keep disabled
        }

        void OnDestroy()
        {
            if (!applied)
                return;

            GameplayData gd = GameplayData.Get();
            if (gd != null && Mathf.Approximately(gd.turn_duration, kDisabledDuration))
                gd.turn_duration = original_duration;
        }
    }
}
#endif
