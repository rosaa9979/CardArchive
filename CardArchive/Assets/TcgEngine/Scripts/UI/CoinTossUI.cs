using System.Collections;
using TcgEngine.Client;
using UnityEngine;
using UnityEngine.UI;

namespace TcgEngine.UI
{
    /// <summary>Presentation of the authoritative first_player; never rolls its own result.</summary>
    public class CoinTossUI : MonoBehaviour
    {
        private CanvasGroup group;
        private RectTransform coin;
        private CoinTossGraphic face;
        private CoinTossGraphic edge;
        private UnityEngine.UI.Text result;
        private UnityEngine.UI.Text caption;
        private bool first;
        private bool liveMatch;
        public static bool IsShowing { get; private set; }
        private static CoinTossUI active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { active = null; IsShowing = false; }

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
            Texture front = Resources.Load<Texture2D>("CoinToss/Front");
            Texture back = Resources.Load<Texture2D>("CoinToss/Back");
            if (front == null || back == null)
            {
                Debug.LogError("Coin toss textures missing from Resources/CoinToss.");
                return null;
            }
            var root = new GameObject("CoinToss", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(CoinTossUI));
            root.transform.SetParent(parent, false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            CoinTossUI ui = root.GetComponent<CoinTossUI>();
            active = ui;
            IsShowing = true;
            ui.first = goesFirst;
            ui.liveMatch = isLiveMatch;
            ui.group = root.GetComponent<CanvasGroup>();
            ui.group.alpha = 0f;
            var shade = Child("Backdrop", root.transform, new Vector2(1920, 1080));
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = Vector2.one;
            shade.sizeDelta = Vector2.zero;
            var background = shade.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.015f, 0.035f, 0.08f, 0.84f);
            background.raycastTarget = true;
            ui.caption = Label("Caption", root.transform, font, 25, 390);
            ui.caption.text = "선·후공 결정";
            ui.caption.color = new Color32(180, 196, 218, 255);
            ui.coin = Child("Coin", root.transform, new Vector2(400, 400));
            ui.edge = Child("TwoToneRim", ui.coin, new Vector2(400, 400)).gameObject.AddComponent<CoinTossGraphic>();
            ui.edge.rim = true;
            ui.edge.raycastTarget = false;
            ui.face = Child("Face", ui.coin, new Vector2(400, 400)).gameObject.AddComponent<CoinTossGraphic>();
            ui.face.front = front;
            ui.face.back = back;
            ui.face.raycastTarget = false;
            ui.result = Label("Result", root.transform, font, 46, -250);
            ui.result.text = "";
            ui.SetFrame(0f);
            if (Application.isPlaying) ui.StartCoroutine(ui.Animate());
            return ui;
        }

        private static RectTransform Child(string name, Transform parent, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return rect;
        }

        private static UnityEngine.UI.Text Label(string name, Transform parent, Font font, int size, float y)
        {
            RectTransform rect = Child(name, parent, new Vector2(900, 90));
            rect.anchoredPosition = new Vector2(0, y);
            var label = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            label.font = font;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return label;
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
            float toss = Mathf.Clamp01((seconds - 0.2f) / 1.65f);
            float ease = 1f - Mathf.Pow(1f - toss, 3f);
            float finalAngle = first ? 1440f : 1620f;
            Quaternion pose = Quaternion.Euler(12f * Mathf.Sin(toss * Mathf.PI), finalAngle * ease,
                14f * Mathf.Sin(toss * Mathf.PI * 2f) * (1f - toss));
            float bounce = seconds > 1.85f ? Mathf.Sin((seconds - 1.85f) * 20f) * Mathf.Exp(-(seconds - 1.85f) * 10f) : 0f;
            coin.anchoredPosition = new Vector2(0, 35f + 135f * Mathf.Sin(toss * Mathf.PI) + bounce * 12f);
            float size = 310f + 35f * Mathf.Sin(toss * Mathf.PI) + bounce * 16f;
            face.SetPose(pose, size);
            edge.SetPose(pose, size);
            group.alpha = Mathf.Min(Mathf.Clamp01(seconds / 0.18f), Mathf.Clamp01((GameConfig.Timing.coin_toss - seconds) / 0.3f));
            bool revealed = seconds >= 1.85f;
            result.text = revealed ? (first ? "선공입니다" : "후공입니다") : "";
            result.color = first ? new Color32(75, 172, 255, 255) : new Color32(255, 145, 195, 255);
            caption.text = revealed ? "곧 대전이 시작됩니다" : "선·후공 결정";
        }

        private void OnDestroy()
        {
            if (active == this) { active = null; IsShowing = false; }
        }
    }
}
