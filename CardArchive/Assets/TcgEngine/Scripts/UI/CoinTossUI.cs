using TcgEngine.Client;
using UnityEngine;

namespace TcgEngine.UI
{
    /// <summary>Selects the authoritative result; all presentation lives in Animator clips.</summary>
    public class CoinTossUI : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        private static CoinTossUI active;
        public static bool IsShowing => active != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { active = null; }

        public static void Show(GameUI owner)
        {
            if (TcgEngine.Replay.ReplaySession.Active || active != null) return;
            GameClient client = GameClient.Get();
            Game data = client.GetGameData();
            if (data == null || data.state != GameState.Play || data.phase != GamePhase.GameStart) return;
            CoinTossUI prefab = Resources.Load<CoinTossUI>("CoinToss/CoinTossUI");
            if (prefab == null || prefab.animator == null)
            {
                Debug.LogError("Coin toss prefab or Animator is missing.");
                return;
            }
            active = Instantiate(prefab, owner.transform, false);
            active.Play(data.first_player == client.GetPlayerID());
            PresentationClock.Track(() => active == null);
        }

        public void Play(bool goesFirst)
        {
            animator.Play(goesFirst ? "First" : "Second", 0, 0f);
        }

        // Animation Event on the last frame of both clips.
        public void Finish() { Destroy(gameObject); }

        private void OnDestroy()
        {
            if (active == this) active = null;
        }
    }
}
