using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TcgEngine.UI;

namespace TcgEngine
{
    /// <summary>
    /// Live operations received from the API (ops in the version, login and heartbeat responses): maintenance and admin messages.
    /// Every response is the full current list, scheduled times are counted down locally from remain_sec
    /// so every client reacts at the same time without trusting its own clock.
    /// Announcements are shown only when their time passes while the game is running (not on receive nor after a pause).
    /// Maintenance: NoticeText 10 and 5 min before, a countdown in the last minute, then back to the Boot scene with a popup.
    /// Messages: NoticeText at their show time.
    /// The game server only reads the maintenance state (matchmaking block, onShutdown) and never leaves.
    /// Created on first use and kept between scenes, the popup is built in code.
    /// </summary>

    public class LiveOpsManager : MonoBehaviour
    {
        public const string boot_scene = "Boot";

        public UnityAction onShutdown; //Shutdown time reached or server reported an active maintenance

        private const string load_key = "maintenance";
        private const float busy_wait_max = 5f; //Max wait for pending requests (deck save...) before leaving
        private static readonly int[] notice_minutes = { 10, 5 }; //Announced once each when the time passes while playing
        private const float notice_duration = 4f;
        private const float countdown_sec = 60f;    //Last minute: the notice stays visible and counts down
        private const float notice_jump_max = 2f;   //A bigger remain drop means the app was paused, passed notices are skipped

        private MaintenanceInfo info;
        private float shutdown_time = -1f; //Time.realtimeSinceStartup of the shutdown, -1 when none is scheduled
        private bool closed = false;
        private bool leaving = false;
        private string notice_schedule = ""; //shutdown_at the notices are tracked for, a new schedule restarts them
        private float notice_remain = -1f;   //Remain seconds at the previous frame, -1 until the first frame of a schedule
        private bool counting = false;       //Countdown notice is held

        private Dictionary<string, ScheduledMessage> messages = new Dictionary<string, ScheduledMessage>(); //By message id
        private List<string> message_removes = new List<string>();

        private Canvas canvas;
        private GameObject popup;
        private Text popup_text;
        private GameObject event_system; //Only created when the current scene has none (Boot scene)

        private static LiveOpsManager instance;

        public static LiveOpsManager Get()
        {
            if (instance == null && Application.isPlaying)
            {
                GameObject obj = new GameObject("LiveOpsManager");
                DontDestroyOnLoad(obj);
                instance = obj.AddComponent<LiveOpsManager>();
            }
            return instance;
        }

        void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (instance == this)
                instance = null;
        }

        void Update()
        {
            if (IsClientSide())
                UpdateMessages();

            if (shutdown_time < 0f || closed)
                return;

            float remain = shutdown_time - Time.realtimeSinceStartup;
            if (remain <= 0f)
            {
                Shutdown();
                return;
            }

            if (IsClientSide())
                UpdateNotices(remain);
        }

        //Called with every API response that contains the live ops state
        public void Receive(LiveOpsInfo ops)
        {
            if (string.IsNullOrEmpty(ops.maintenance.state))
                return; //Older API without live ops support

            ReceiveMaintenance(ops.maintenance);
            ReceiveMessages(ops.messages);
        }

        private void ReceiveMaintenance(MaintenanceInfo minfo)
        {
            info = minfo;

            if (minfo.IsScheduled)
            {
                shutdown_time = Time.realtimeSinceStartup + Mathf.Max(minfo.remain_sec, 0f);
                closed = false;
                if (minfo.shutdown_at != notice_schedule)
                {
                    notice_schedule = minfo.shutdown_at;
                    notice_remain = -1f; //Nothing is announced on receive, only when a notice time passes while playing
                }
            }
            else if (minfo.IsActive)
            {
                shutdown_time = Time.realtimeSinceStartup;
                if (!closed)
                    Shutdown();
                else if (IsClientSide() && !leaving)
                    ShowPopup(); //Retry from the Boot scene while still in maintenance
            }
            else
            {
                shutdown_time = -1f;
                closed = false;
                ResetNotices();
                HidePopup();
            }
        }

        private void Shutdown()
        {
            closed = true;
            ResetNotices();
            Debug.Log("Server maintenance: shutdown");
            onShutdown?.Invoke();

            if (IsClientSide())
                Leave();
        }

        private async void Leave()
        {
            if (leaving)
                return;

            leaving = true;
            LoadPanel.Show(load_key, "서버 점검으로 접속을 종료합니다");

            //Let pending requests finish so nothing is half saved, the API still accepts them until it answers 503
            ApiClient client = ApiClient.Get();
            float wait = 0f;
            while (client != null && client.IsBusy() && wait < busy_wait_max)
            {
                await TimeTool.Delay(100);
                wait += 0.1f;
            }

            if (!Application.isPlaying || this == null)
                return;

            //Keep the saved tokens so the next login screen can refresh the session after the maintenance
            client?.EndSession();
            messages.Clear();
            NoticeText.ClearText();

            TcgNetwork network = TcgNetwork.Get();
            if (network != null && network.IsActive())
                network.Disconnect();

            if (SceneNav.GetCurrentScene() != boot_scene)
                SceneNav.GoTo(boot_scene);

            LoadPanel.Hide(load_key);
            leaving = false;
            ShowPopup();
        }

        //Game server stops matchmaking a few minutes before the shutdown, clients check it before searching
        public bool IsMatchmakingBlocked()
        {
            if (closed || info.matchmaking_blocked)
                return true;
            if (shutdown_time < 0f)
                return false;
            float block_time = shutdown_time - info.block_match_minutes * 60f;
            return Time.realtimeSinceStartup >= block_time;
        }

        public bool IsScheduled()
        {
            return shutdown_time >= 0f && !closed;
        }

        public bool IsClosed()
        {
            return closed;
        }

        public float GetRemainSec()
        {
            if (shutdown_time < 0f)
                return 0f;
            return Mathf.Max(shutdown_time - Time.realtimeSinceStartup, 0f);
        }

        public MaintenanceInfo Info { get { return info; } }

        private bool IsClientSide()
        {
            ApiClient client = ApiClient.Get();
            return !Application.isBatchMode && (client == null || !client.is_server);
        }

        // ---- Notices ----

        //A time is passed live when it is crossed between two close frames:
        //not on the first frame after receiving it (last_remain < 0) nor after a pause (big drop)
        private static bool IsCrossedLive(float last_remain, float remain, float time)
        {
            return last_remain >= 0f && last_remain > time && remain <= time && last_remain - remain <= notice_jump_max;
        }

        private void UpdateNotices(float remain)
        {
            foreach (int minutes in notice_minutes)
            {
                if (IsCrossedLive(notice_remain, remain, minutes * 60f))
                    NoticeText.ShowText(minutes + "분 뒤 서버 점검으로 접속이 종료됩니다", notice_duration);
            }
            notice_remain = remain;

            if (remain <= countdown_sec)
            {
                NoticeText.HoldText(Mathf.CeilToInt(remain) + "초 뒤 서버 점검으로 접속이 종료됩니다");
                counting = true;
            }
            else if (counting)
            {
                NoticeText.ReleaseText(); //Schedule moved later
                counting = false;
            }
        }

        private void ResetNotices()
        {
            notice_schedule = "";
            notice_remain = -1f;
            if (counting)
                NoticeText.ReleaseText();
            counting = false;
        }

        // ---- Messages ----

        private class ScheduledMessage
        {
            public string text;
            public float show_time;           //Time.realtimeSinceStartup when every client shows it
            public float last_remain = -1f;   //Remain seconds at the previous frame
            public bool done = false;         //Show time passed (shown or skipped), kept until the server list drops it
        }

        //The list holds the messages not shown yet: new ids are scheduled, known ids only resync, missing ids are removed
        private void ReceiveMessages(LiveOpsMessage[] list)
        {
            message_removes.Clear();
            message_removes.AddRange(messages.Keys);

            if (list != null)
            {
                foreach (LiveOpsMessage message in list)
                {
                    if (string.IsNullOrEmpty(message.id))
                        continue;

                    message_removes.Remove(message.id);
                    float show_time = Time.realtimeSinceStartup + Mathf.Max(message.remain_sec, 0f);
                    if (messages.TryGetValue(message.id, out ScheduledMessage scheduled))
                    {
                        if (!scheduled.done)
                            scheduled.show_time = show_time;
                    }
                    else
                    {
                        messages[message.id] = new ScheduledMessage { text = message.text, show_time = show_time };
                    }
                }
            }

            foreach (string id in message_removes)
                messages.Remove(id);
        }

        private void UpdateMessages()
        {
            foreach (ScheduledMessage message in messages.Values)
            {
                if (message.done)
                    continue;

                float remain = message.show_time - Time.realtimeSinceStartup;
                if (remain <= 0f)
                {
                    message.done = true;
                    if (IsCrossedLive(message.last_remain, remain, 0f))
                        NoticeText.ShowText(message.text, notice_duration);
                }
                message.last_remain = remain;
            }
        }

        // ---- UI ----

        private void ShowPopup()
        {
            BuildUI();
            string message = string.IsNullOrWhiteSpace(info.message) ? "서버 점검 중입니다. 잠시 후 다시 접속해 주세요." : info.message;
            string end_time = FormatDate(info.end_at);
            if (!string.IsNullOrEmpty(end_time))
                message += "\n\n예상 종료: " + end_time;
            popup_text.text = message;
            popup.SetActive(true);
            EnsureEventSystem();
        }

        private void HidePopup()
        {
            if (popup != null)
                popup.SetActive(false);
            ReleaseEventSystem();
        }

        private void OnClickRetry()
        {
            HidePopup();
            BootMenu boot = FindAnyObjectByType<BootMenu>();
            if (boot != null)
                boot.CheckVersion(); //Shows the popup again if the server is still in maintenance
            else
                SceneNav.GoTo(boot_scene);
        }

        private void OnClickQuit()
        {
            Application.Quit();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            //The new scene brings its own EventSystem, two active ones log warnings
            if (event_system != null && FindObjectsByType<EventSystem>().Length > 1)
                ReleaseEventSystem();
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null || event_system != null)
                return;
            event_system = new GameObject("LiveOpsEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            event_system.transform.SetParent(transform, false);
        }

        private void ReleaseEventSystem()
        {
            if (event_system != null)
                Destroy(event_system);
            event_system = null;
        }

        private string FormatDate(string iso)
        {
            if (string.IsNullOrEmpty(iso))
                return "";
            if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime date))
                return "";
            return date.ToLocalTime().ToString("M월 d일 HH:mm");
        }

        private void BuildUI()
        {
            if (canvas != null)
                return;

            Font font = GetFont();

            GameObject cobj = new GameObject("LiveOpsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cobj.transform.SetParent(transform, false);
            canvas = cobj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1100; //Above every scene UI, the LoadPanel, WarningText and NoticeText
            CanvasScaler scaler = cobj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            //Popup with a full screen blocker
            Image blocker = CreateImage("Popup", cobj.transform, new Color(0f, 0f, 0f, 0.7f));
            Stretch(blocker.rectTransform, Vector2.zero);
            popup = blocker.gameObject;

            Image box = CreateImage("Box", popup.transform, new Color(0.12f, 0.13f, 0.16f, 0.98f));
            SetRect(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 560f));

            Text title = CreateText("Title", box.transform, font, 44, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -60f), new Vector2(-80f, 80f));
            title.text = "서버 점검 안내";

            popup_text = CreateText("Message", box.transform, font, 30, TextAnchor.MiddleCenter);
            SetRect(popup_text.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 10f), new Vector2(-100f, -300f));

            CreateButton("Retry", box.transform, font, "다시 시도", new Vector2(-170f, 70f), new Color(0.23f, 0.42f, 1f), OnClickRetry);
            CreateButton("Quit", box.transform, font, "게임 종료", new Vector2(170f, 70f), new Color(0.17f, 0.19f, 0.23f), OnClickQuit);

            popup.SetActive(false);
        }

        //Same font as the loading panel (supports Korean), the built-in font is only a fallback
        private Font GetFont()
        {
            LoadPanel panel = LoadPanel.Get();
            if (panel != null && panel.load_txt != null && panel.load_txt.font != null)
                return panel.load_txt.font;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private Text CreateText(string name, Transform parent, Font font, int size, TextAnchor anchor)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            Text text = obj.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private void CreateButton(string name, Transform parent, Font font, string label, Vector2 pos, Color color, UnityAction action)
        {
            Image image = CreateImage(name, parent, color);
            SetRect(image.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), pos, new Vector2(280f, 76f));
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            Text text = CreateText("Text", image.transform, font, 30, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, Vector2.zero);
            text.text = label;
            text.raycastTarget = false;
        }

        private void SetRect(RectTransform rect, Vector2 anchor_min, Vector2 anchor_max, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = anchor_min;
            rect.anchorMax = anchor_max;
            rect.pivot = new Vector2(0.5f, anchor_min.y == anchor_max.y ? anchor_min.y : 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        private void Stretch(RectTransform rect, Vector2 padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = padding;
            rect.offsetMax = -padding;
        }
    }
}
