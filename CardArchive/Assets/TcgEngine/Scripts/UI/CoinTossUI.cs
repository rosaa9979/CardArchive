using System.Collections;
using TcgEngine.Client;
using UnityEngine;

namespace TcgEngine.UI
{
    /// <summary>Presentation of the authoritative first_player; never rolls its own result.</summary>
    public class CoinTossUI : MonoBehaviour
    {
        [Header("Prefab references")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform coin;
        [SerializeField] private CoinTossGraphic face;
        [SerializeField] private CoinTossGraphic edge;
        [SerializeField] private UnityEngine.UI.Text result;
        [SerializeField] private UnityEngine.UI.Text caption;

        [Header("Animation")]
        [SerializeField, Min(1)] private int fullRotations = 2;
        [SerializeField, Min(0f)] private float tossHeight = 135f;
        [SerializeField, Min(1f)] private float coinDiameter = 310f;
        [SerializeField] private AnimationCurve spinProgress = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        private const float TossStart = 0.2f;
        private const float TossDuration = 2.25f;
        private const float RevealTime = TossStart + TossDuration;
        private bool first;
        private bool liveMatch;
        private Vector2 restPosition;
        public static bool IsShowing => active != null;
        private static CoinTossUI active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { active = null; }

        public static void Show(GameUI owner)
        {
            // Replays can seek and run faster than real time; do not introduce a realtime overlay.
            if (TcgEngine.Replay.ReplaySession.Active || active != null) return;
            Game data = GameClient.Get().GetGameData();
            if (data == null || data.state != GameState.Play) return;
            Create(owner.transform, owner.quit_btn != null ? owner.quit_btn.font : owner.turn_count.font,
                data.first_player == GameClient.Get().GetPlayerID(), true);
        }

        // Also used by the editor preview so both outcomes can be inspected without starting a match.
        public static CoinTossUI Create(Transform parent, Font font, bool goesFirst, bool isLiveMatch)
        {
            if (active != null) return active;
            CoinTossUI prefab = Resources.Load<CoinTossUI>("CoinToss/CoinTossUI");
            if (prefab == null)
            {
                Debug.LogError("Coin toss prefab missing from Resources/CoinToss/CoinTossUI.");
                return null;
            }
            CoinTossUI ui = Instantiate(prefab, parent, false);
            if (ui.group == null || ui.coin == null || ui.face == null || ui.edge == null
                || ui.result == null || ui.caption == null || ui.face.front == null || ui.face.back == null)
            {
                Debug.LogError("Coin toss prefab has missing references.", ui);
                if (Application.isPlaying) Destroy(ui.gameObject);
                else DestroyImmediate(ui.gameObject);
                return null;
            }
            active = ui;
            ui.first = goesFirst;
            ui.liveMatch = isLiveMatch;
            ui.restPosition = ui.coin.anchoredPosition;
            // Keep fonts authored on the prefab; use the match font only as a fallback.
            if (ui.result.font == null) ui.result.font = font;
            if (ui.caption.font == null) ui.caption.font = font;
            ui.SetFrame(0f);
            if (Application.isPlaying) ui.StartCoroutine(ui.Animate());
            return ui;
        }
        private IEnumerator Animate()
        {
            float elapsed = 0f;
            while (elapsed < GameConfig.Timing.coin_toss)
            {
                if (liveMatch)
                {
                    GameClient client = GameClient.Get();
                    Game data = client != null ? client.GetGameData() : null;
                    // End promptly on disconnect/end or if late packets already advanced the match.
                    if (data == null || !client.IsReady() || data.state != GameState.Play
                        || (data.phase != GamePhase.GameStart && data.phase != GamePhase.None)) break;
                }
                SetFrame(elapsed);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Destroy(gameObject);
        }

        public void SetFrame(float seconds)
        {
            float toss = Mathf.Clamp01((seconds - TossStart) / TossDuration);
            float ease = toss >= 1f ? 1f : Mathf.Clamp01(spinProgress.Evaluate(toss));
            float finalAngle = Mathf.Max(1, fullRotations) * 360f + (first ? 0f : 180f);
            Quaternion pose = Quaternion.Euler(12f * Mathf.Sin(toss * Mathf.PI), finalAngle * ease,
                14f * Mathf.Sin(toss * Mathf.PI * 2f) * (1f - toss));
            float bounce = seconds > RevealTime ? Mathf.Sin((seconds - RevealTime) * 20f) * Mathf.Exp(-(seconds - RevealTime) * 10f) : 0f;
            coin.anchoredPosition = restPosition + Vector2.up * (tossHeight * Mathf.Sin(toss * Mathf.PI) + bounce * 12f);
            float size = coinDiameter + 35f * Mathf.Sin(toss * Mathf.PI) + bounce * 16f;
            face.SetPose(pose, size);
            edge.SetPose(pose, size);
            group.alpha = Mathf.Min(Mathf.Clamp01(seconds / 0.18f), Mathf.Clamp01((GameConfig.Timing.coin_toss - seconds) / 0.3f));
            bool revealed = seconds >= RevealTime;
            result.text = revealed ? (first ? "선공입니다" : "후공입니다") : "";
            result.color = first ? new Color32(75, 172, 255, 255) : new Color32(255, 145, 195, 255);
            caption.text = revealed ? "곧 대전이 시작됩니다" : "선·후공 결정";
        }

        private void OnDestroy()
        {
            if (active == this) active = null;
        }
    }
}
