#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TcgEngine.Client;
using TcgEngine.Server;
using UnityEngine;
using UnityEngine.UI;

namespace TcgEngine
{
    [DefaultExecutionOrder(-10000)]
    public class EffectTestPanel : MonoBehaviour
    {
        public const string ScenePath = "Assets/TcgEngine/Resources/Scenes/Tool/EffectTestGame.unity";
        static EffectTestPanel instance;
        public static bool Active => instance != null;
        GameSettings previousGame;
        GameSettings testSettings;
        PlayerSettings previousPlayer, previousAI;
        string previousObserver;
        GameServer server;
        List<CardData> cards;
        CardData selected;
        string search = "", status = "Complete mulligan to prepare the test board.";
        int owner, zone;
        Slot slot = Slot.None;
        Vector2 scroll, cardScroll;
        bool expanded = true;
        RectTransform blocker;
        Rect PanelRect => new Rect(10, 44, Mathf.Min(370, Screen.width - 20), Mathf.Max(100, Screen.height - 54));

        void Awake()
        {
            instance = this;
            previousGame = GameClient.game_settings;
            previousPlayer = GameClient.player_settings;
            previousAI = GameClient.ai_settings;
            previousObserver = GameClient.observe_user;
            GameClient.game_settings = GameSettings.Default;
            testSettings = GameClient.game_settings;
            GameClient.game_settings.game_uid = "effect-test-" + Guid.NewGuid().ToString("N");
            GameClient.game_settings.scene = "EffectTestGame";
            GameClient.player_settings = PlayerSettings.Default;
            GameClient.ai_settings = PlayerSettings.DefaultAI;
            GameClient.observe_user = null;
            Time.timeScale = 1;
        }
        void Start()
        {
            if (TcgNetwork.Get() != null && TcgNetwork.Get().IsActive()) TcgNetwork.Get().Disconnect();
            GameClient.player_settings.deck = new UserDeckData(GameplayData.Get().test_deck);
            GameClient.ai_settings.deck = new UserDeckData(GameplayData.Get().test_deck_ai);
            GameClient.ai_settings.ai_level = GameplayData.Get().ai_level;
            cards = CardData.GetAll().Where(c => c != null).OrderBy(c => c.title).ThenBy(c => c.id).ToList();
            // A UI raycast surface prevents panel clicks from playing the card underneath.
            var canvasObject = new GameObject("Effect test input surface", typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var surface = new GameObject("Panel input blocker", typeof(RectTransform), typeof(Image));
            surface.layer = LayerMask.NameToLayer("UI");
            surface.transform.SetParent(canvasObject.transform, false);
            surface.GetComponent<Image>().color = Color.clear;
            blocker = surface.GetComponent<RectTransform>();
            blocker.anchorMin = blocker.anchorMax = blocker.pivot = new Vector2(0, 1);
        }
        void Update()
        {
            if (server == null)
            {
                var manager = FindAnyObjectByType<ServerManagerLocal>();
                if (manager != null) server = manager.DebugServer;
            }
            if (blocker != null)
            {
                blocker.anchoredPosition = new Vector2(10, -10);
                blocker.sizeDelta = expanded ? new Vector2(PanelRect.width, PanelRect.height + 34) : new Vector2(150, 28);
            }
        }
        void OnGUI()
        {
            if (GUI.Button(new Rect(10, 10, 150, 28), expanded ? "Hide effect test" : "Effect test")) expanded = !expanded;
            if (!expanded) return;
            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Effect test — direct card insertion");
            GUILayout.Label("No play / summon / draw triggers or costs.");
            if (server != null)
            {
                server.DebugPauseAI = GUILayout.Toggle(server.DebugPauseAI, "Pause AI (mulligan still completes)");
                bool freeze = GUILayout.Toggle(server.DebugFreezeTimer, "Freeze turn timer");
                if (server.DebugFreezeTimer && !freeze) server.GetGameData().turn_timer = GameplayData.Get().turn_duration;
                server.DebugFreezeTimer = freeze;
                GUI.enabled = server.DebugCanEdit;
                if (GUILayout.Button("End current main phase")) server.DebugNextStep();
                GUI.enabled = true;
            }
            owner = GUILayout.SelectionGrid(owner, new[] { "Owner: Player 0", "Owner: Player 1" }, 2);
            zone = GUILayout.SelectionGrid(zone, new[] { "Hand", "Field" }, 2);
            GUILayout.Label("Search by card name or ID");
            search = GUILayout.TextField(search);
            cardScroll = GUILayout.BeginScrollView(cardScroll, GUILayout.Height(170));
            int shown = 0;
            if (cards != null)
                foreach (var card in cards)
                {
                    if (zone == 1 && !card.IsBoardCard()) continue;
                    if (!string.IsNullOrEmpty(search) && card.id.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                        && (card.title ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (++shown > 100) { GUILayout.Label("Refine search to show more cards."); break; }
                    if (GUILayout.Button((selected == card ? "✓ " : "") + card.title + " [" + card.id + "]")) selected = card;
                }
            GUILayout.EndScrollView();
            GUILayout.Label(selected != null ? "Selected: " + selected.title + " [" + selected.id + "]" : "Select a card.");
            if (zone == 1)
            {
                GUILayout.Label("Empty field slot (owner is independent of location)");
                var empty = Slot.GetAll().Where(s => BSlot.Get(s) != null && server != null && !server.GetGameData().IsCardOnSlot(s)).ToList();
                for (int i = 0; i < empty.Count; i++)
                {
                    if (i % 4 == 0) GUILayout.BeginHorizontal();
                    var s = empty[i];
                    if (GUILayout.Button((slot == s ? "✓" : "") + s.x + "," + s.y + " P" + s.p)) slot = s;
                    if (i % 4 == 3 || i == empty.Count - 1) GUILayout.EndHorizontal();
                }
            }
            GUI.enabled = server != null && server.DebugCanEdit && selected != null
                && (zone == 0 || selected.IsBoardCard() && slot.IsValid());
            if (GUILayout.Button("Add card without triggering effects"))
            {
                try
                {
                    server.DebugAddCard(selected, owner, zone == 1, slot);
                    status = "Added " + selected.title + " to P" + owner + (zone == 1 ? " field." : " hand.");
                }
                catch (Exception e) { status = e.Message; }
            }
            GUI.enabled = true;
            if (server != null && !server.DebugCanEdit) GUILayout.Label("Add is available in an idle main phase without a selector.");
            GUILayout.Label(status);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        void OnDestroy()
        {
            if (instance != this) return;
            instance = null;
            if (!ReferenceEquals(GameClient.game_settings, testSettings)) return;
            GameClient.game_settings = previousGame;
            GameClient.player_settings = previousPlayer;
            GameClient.ai_settings = previousAI;
            GameClient.observe_user = previousObserver;
        }
    }
}
#endif
