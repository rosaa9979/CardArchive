using TcgEngine.Client;
using UnityEngine;

namespace TcgEngine.UI
{
    /// <summary>Selects the authoritative result; all presentation lives in Animator clips.</summary>
    public class CoinTossUI : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        private static CoinTossUI active;
        private bool finished;
        public static bool IsShowing => active != null && !active.finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { active = null; }

        public static void Show(GameUI owner)
        {
            if (owner == null || active != null) return;
            GameClient client = GameClient.Get();
            Game data = client != null ? client.GetGameData() : null;
            if (data == null || data.state != GameState.Play || data.phase != GamePhase.GameStart) return;
            CoinTossUI prefab = Resources.Load<CoinTossUI>("CoinToss/CoinTossUI");
            if (prefab == null || prefab.animator == null)
            {
                Debug.LogError("Coin toss prefab or Animator is missing.");
                return;
            }
            active = Instantiate(prefab, owner.transform, false);
            active.Play(data.first_player == client.GetPlayerID());
            // Track this instance, so a canceled intro cannot wait on a later one.
            var instance = active;
            PresentationClock.Track(() => instance == null || instance.finished);
        }

        public void Play(bool goesFirst)
        {
            if (TcgEngine.Replay.ReplaySession.Active) animator.updateMode = AnimatorUpdateMode.Normal;
            animator.Play(goesFirst ? "First" : "Second", 0, 0f);
        }

        // Animation Event on the last frame of both clips.
        public void Finish()
        {
            finished = true;
            Destroy(gameObject);
        }

        public static void Cancel()
        {
            if (active == null) return;
            var canceled = active;
            active = null;
            canceled.Finish();
        }

        private void OnDestroy()
        {
            if (active == this) active = null;
        }
    }
}
