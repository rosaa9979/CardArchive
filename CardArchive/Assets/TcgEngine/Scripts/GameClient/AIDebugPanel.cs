#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using TcgEngine;
using TcgEngine.AI;
using TcgEngine.UI;

namespace TcgEngine.Client
{
    /// <summary>
    /// EDITOR-ONLY cheat/debug panel (plain MonoBehaviour; shows/hides a referenced CanvasGroup).
    /// Runs the AI minimax evaluation on the current game state, FROM THE LOCAL PLAYER'S
    /// perspective (i.e. "what would the AI do with my hand right now"), and fills a list of
    /// item rows with the candidate actions ranked by their minimax score, highest first.
    ///
    /// Like the real solo AI (AIPlayerMM), the ranking is recomputed every action: while the panel
    /// is open and it is the local player's turn, it re-evaluates on each game refresh / new turn.
    /// The evaluation is decoupled from Show() so opening the panel does not flash a loading screen.
    ///
    /// The whole file is wrapped in #if UNITY_EDITOR so it is stripped from regular builds.
    ///
    /// Scene/UI setup is done by hand:
    ///  - this component can live on an always-active object (e.g. the canvas root)
    ///  - canvas_group : the panel content CanvasGroup that Show/Hide turns on/off
    ///  - open/close (toggle) button -> OnClickToggle()  (place it OUTSIDE canvas_group, otherwise
    ///                                  it becomes unclickable while the panel is hidden)
    ///  - close button -> ClosePanel()  (can live inside the panel)
    ///  - (optional) refresh button -> Evaluate()
    ///  - items       : the ranked rows (one AIDebugActionItem per slot, e.g. 10 of them)
    ///  - status_text : (optional) shows "계산 중..." / messages like "내 턴이 아닙니다"
    /// </summary>
    public class AIDebugPanel : MonoBehaviour
    {
        [Header("UI refs (set up by hand)")]
        public CanvasGroup canvas_group;         //Panel content; Show/Hide toggles its alpha/interactable
        public List<AIDebugActionItem> items = new List<AIDebugActionItem>(); //Ranked rows (defines max count)
        public TMP_Text status_text;             //Optional: messages when there is nothing to rank

        [Header("Settings")]
        public int ai_level = 10;                //10 = strongest / least randomized heuristic (most deterministic)
        public bool start_hidden = true;         //Hide the panel on Awake
        public bool auto_recalc = true;          //While open + your turn, recompute on every refresh / new turn
        public bool block_input_while_calculating = false; //Show the network LoadPanel (blocks touch) during calc
        public bool reverse_order = false;       //Fill rows bottom-up (use if your layout stacks items bottom-to-top)

        private AILogic ai_logic;
        private bool evaluating;
        private bool visible;
        private bool needs_recalc;
        private bool subscribed;

        void Awake()
        {
            if (start_hidden)
                SetGroupVisible(false);
            ClearItems();
            SetStatus("");
        }

        void Update()
        {
            EnsureSubscribed();

            //Coalesce multiple refreshes in the same frame into a single recompute
            if (needs_recalc && !evaluating)
            {
                needs_recalc = false;
                Evaluate();
            }
        }

        void OnDisable()
        {
            //Make sure the worker thread never outlives this component
            StopAndClear();
            evaluating = false;
            ShowNetworkLoading(false);
            Unsubscribe();
        }

        //--- Game events (recompute every action, like AIPlayerMM) -----------

        private void EnsureSubscribed()
        {
            if (subscribed)
                return;
            GameClient client = GameClient.Get();
            if (client == null)
                return;
            client.onNewTurn += OnNewTurn;
            client.onRefreshAll += OnRefresh;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            GameClient client = GameClient.Get();
            if (client != null)
            {
                client.onNewTurn -= OnNewTurn;
                client.onRefreshAll -= OnRefresh;
            }
            subscribed = false;
        }

        private void OnNewTurn(int player_id)
        {
            RequestRecalc();
        }

        private void OnRefresh()
        {
            RequestRecalc();
        }

        //Auto recompute only while the panel is open (no point evaluating an invisible panel).
        private void RequestRecalc()
        {
            if (auto_recalc && visible)
                needs_recalc = true;
        }

        //--- Show / Hide ----------------------------------------------------

        public void Show()
        {
            visible = true;
            SetGroupVisible(true);
            needs_recalc = true; //Refresh once on open (goes through the coalesced path, no loading flash)
        }

        public void Hide()
        {
            visible = false;
            SetGroupVisible(false);
            needs_recalc = false;
            StopAndClear();
            evaluating = false;
            ShowNetworkLoading(false);
        }

        public void Toggle()
        {
            if (visible)
                Hide();
            else
                Show();
        }

        private void SetGroupVisible(bool show)
        {
            if (canvas_group == null)
                return;
            canvas_group.alpha = show ? 1f : 0f;
            canvas_group.interactable = show;
            canvas_group.blocksRaycasts = show;
        }

        //--- Button hooks ---------------------------------------------------

        public void OnClickToggle()
        {
            Toggle();
        }

        public void ClosePanel()
        {
            Hide();
        }

        //Run the AI evaluation on the current state and refresh the list.
        public void Evaluate()
        {
            if (evaluating)
            {
                needs_recalc = true; //Re-run once the current calc finishes (state may have changed)
                return;
            }

            GameClient client = GameClient.Get();
            if (client == null || !client.IsReady())
            {
                ClearItems();
                SetStatus("게임이 아직 준비되지 않았습니다.");
                return;
            }

            StartCoroutine(EvaluateRoutine());
        }

        //--- Evaluation -----------------------------------------------------

        private IEnumerator EvaluateRoutine()
        {
            evaluating = true;

            GameClient client = GameClient.Get();
            Game data = client.GetGameData();
            int player_id = client.GetPlayerID();

            //AILogic only enumerates actions for data.current_player, so the ranking is only
            //meaningful while it is actually the local player's turn.
            if (!client.IsYourTurn())
            {
                ClearItems();
                SetStatus("내 턴에서만 평가할 수 있습니다.");
                evaluating = false;
                yield break;
            }

            //Keep the previous ranking on screen while recomputing; just flag the status.
            SetStatus("계산 중...");
            if (block_input_while_calculating)
                ShowNetworkLoading(true);

            ai_logic = AILogic.Create(player_id, ai_level);

            //Widen the first-level search so we can surface as many candidates as we have item rows.
            int want = Mathf.Max(items.Count, 1);
            ai_logic.nodes_per_action = Mathf.Max(ai_logic.nodes_per_action, want);
            ai_logic.nodes_per_action_wide = Mathf.Max(ai_logic.nodes_per_action_wide, want);

            ai_logic.RunAI(data); //Runs on a worker thread

            while (ai_logic.IsRunning())
            {
                if (block_input_while_calculating)
                    ShowNetworkLoading(true); //Re-assert each frame (GameUI hides LoadPanel every frame)
                yield return null;
            }

            BuildOutput(data, ai_logic.GetFirst());

            StopAndClear();

            if (block_input_while_calculating)
                ShowNetworkLoading(false);
            evaluating = false;
        }

        //Fill the item rows from the first-level child nodes (one per candidate first action),
        //sorted by minimax score, descending.
        private void BuildOutput(Game data, NodeState first)
        {
            ClearItems();

            if (first == null || first.childs == null || first.childs.Count == 0)
            {
                SetStatus("평가 가능한 행동이 없습니다.");
                return;
            }

            SetStatus("");

            List<NodeState> nodes = new List<NodeState>(first.childs);
            nodes.Sort((a, b) => b.hvalue.CompareTo(a.hvalue)); //Descending by score

            int count = Mathf.Min(items.Count, nodes.Count);
            for (int i = 0; i < count; i++)
            {
                int slot = reverse_order ? (count - 1 - i) : i; //rank 1 still highest; just which row it lands in
                AIDebugActionItem item = items[slot];
                if (item == null)
                    continue;

                NodeState node = nodes[i];
                item.SetData(i + 1, node.hvalue, DescribeAction(data, node.last_action));
            }
        }

        //Human-readable description of an AIAction (resolves card / ability ids to titles).
        private string DescribeAction(Game data, AIAction action)
        {
            if (action == null)
                return "(없음)";

            Card card = !string.IsNullOrEmpty(action.card_uid) ? data.GetCard(action.card_uid) : null;
            string cardName = (card != null && card.CardData != null) ? card.CardData.GetTitle() : action.card_uid;

            if (action.type == GameAction.PlayCard)
            {
                string txt = "카드 플레이: " + cardName;
                if (action.slot != Slot.None)
                    txt += " (슬롯 " + action.slot.x + "," + action.slot.y + ")";
                return txt;
            }

            if (action.type == GameAction.CastAbility)
            {
                AbilityData ability = AbilityData.Get(action.ability_id);
                string abilityName = ability != null ? ability.GetTitle() : action.ability_id;
                return "능력 발동: " + cardName + " → " + abilityName;
            }

            if (action.type == GameAction.Move)
            {
                string txt = "이동: " + cardName;
                if (action.slot != Slot.None)
                    txt += " (슬롯 " + action.slot.x + "," + action.slot.y + ")";
                return txt;
            }

            if (action.type == GameAction.EndTurn)
                return "턴 종료";

            //Fallback for selector / other actions
            string verb = GameAction.GetString(action.type);
            return card != null ? verb + " " + cardName : verb;
        }

        //--- Helpers --------------------------------------------------------

        private void StopAndClear()
        {
            if (ai_logic != null)
            {
                ai_logic.Stop();
                ai_logic.ClearMemory();
                ai_logic = null;
            }
        }

        private void ClearItems()
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                    items[i].Clear();
            }
        }

        //Reuse the existing network loading panel (already blocks touch input).
        private void ShowNetworkLoading(bool show)
        {
            LoadPanel panel = LoadPanel.Get();
            if (panel == null)
                return;

            if (show)
                panel.Show(true);
            else
                panel.Hide(true);
        }

        private void SetStatus(string text)
        {
            if (status_text != null)
                status_text.text = text;
        }
    }
}
#endif
