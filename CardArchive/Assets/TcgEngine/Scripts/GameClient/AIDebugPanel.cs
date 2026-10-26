#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
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
        public CanvasGroup detail_group;         //Optional: hover popup that shows a row's score breakdown
        public TMP_Text detail_text;             //Optional: text inside detail_group
        public float detail_padding = 14f;       //Inner margin used when auto-sizing the popup height to the text

        [Header("Settings")]
        public int ai_level = 10;                //10 = strongest / least randomized heuristic (most deterministic)
        public bool start_hidden = true;         //Hide the panel on Awake
        public bool auto_recalc = true;          //While open + your turn, recompute on every refresh / new turn
        public bool block_input_while_calculating = false; //Show the global LoadPanel (blocks touch) during calc
        public bool reverse_order = false;       //Fill rows bottom-up (use if your layout stacks items bottom-to-top)

        private const string load_key = "ai_debug_calc";

        private AILogic ai_logic;
        private bool evaluating;
        private bool visible;
        private bool needs_recalc;
        private bool subscribed;

        void Awake()
        {
            StylePanel(canvas_group);
            StylePanel(detail_group);
            if (detail_text != null)
            {
                //This entire component is editor-only; reuse the project's existing Korean font.
                TMP_FontAsset font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TcgEngine/Fonts/Pretendard-Bold SDF.asset");
                if (font != null)
                {
                    detail_text.font = font;
                    detail_text.fontSharedMaterial = font.material;
                }
                detail_text.fontStyle = FontStyles.Normal;
                detail_text.enableAutoSizing = false;
                detail_text.fontSize = 20f;
                detail_text.color = new Color32(230, 237, 247, 255);
                detail_text.alignment = TextAlignmentOptions.TopLeft;
                detail_text.richText = true;
                detail_text.lineSpacing = 5f;
                detail_text.raycastTarget = false;
                detail_text.margin = new Vector4(detail_padding, detail_padding, detail_padding, detail_padding);
            }
            if (start_hidden)
                SetGroupVisible(false);
            ClearItems();
            SetStatus("");
            SetDetailVisible(false);
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
            SetDetailVisible(false);
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
            SetStatus("AI 평가 · " + count + "/" + nodes.Count + "개 행동");
            for (int i = 0; i < count; i++)
            {
                int slot = reverse_order ? (count - 1 - i) : i; //rank 1 still highest; just which row it lands in
                AIDebugActionItem item = items[slot];
                if (item == null)
                    continue;

                NodeState node = nodes[i];
                //Compute the score breakdown now, while ai_logic is still alive (StopAndClear runs after this).
                string details = ai_logic != null ? ai_logic.BuildScoreReport(node) : "";
                item.SetData(i + 1, node.hvalue, DescribeAction(data, node.last_action), details, this, nodes[0].hvalue);
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
            SetDetailVisible(false);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                    items[i].Clear();
            }
        }

        //--- Hover detail popup -------------------------------------------

        //Called by an item row on pointer enter: fill + size + position the breakdown beside that row.
        //The popup auto-sizes its height to the text and is clamped to stay fully on screen.
        //Setup assumption: detail_text is stretch-anchored inside detail_group (so it fills the resized box),
        //and detail_group has NO ContentSizeFitter (this code drives the size instead).
        public void ShowDetail(AIDebugActionItem item, string details)
        {
            if (detail_group == null)
                return;

            RectTransform drt = detail_group.transform as RectTransform;

            if (detail_text != null)
            {
                detail_text.textWrappingMode = TextWrappingModes.Normal;
                detail_text.overflowMode = TextOverflowModes.Page;
                detail_text.pageToDisplay = 1;
                detail_text.text = (item != null ? item.Summary + "\n" : "")
                    + "<size=80%><color=#9CAEC5>행 위에서 휠로 상세 페이지 이동 · 점수가 높을수록 유리</color></size>\n\n"
                    + FormatReport(details);
            }

            //Height = preferred text height (wrapped at the box width) + padding, capped to the canvas height.
            if (drt != null && detail_text != null)
            {
                float pad = Mathf.Max(0f, detail_padding);
                float box_w = Mathf.Max(10f, drt.rect.width);
                //TMP subtracts its own margins from the supplied measurement width.
                float h = detail_text.GetPreferredValues(detail_text.text, box_w, 0f).y + pad * 2f;

                RectTransform canvas_rt = GetCanvasRect(drt);
                if (canvas_rt != null)
                {
                    float localScale = Mathf.Abs(drt.lossyScale.y / canvas_rt.lossyScale.y);
                    h = Mathf.Min(h, (canvas_rt.rect.height - pad * 2f) / Mathf.Max(0.01f, localScale));
                }

                drt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
            }

            //Follow the hovered row vertically (keep the popup's own horizontal placement), then clamp on-screen.
            RectTransform irt = item != null ? item.transform as RectTransform : null;
            if (drt != null && irt != null)
            {
                Vector3 p = drt.position;
                p.y = irt.position.y;
                drt.position = p;
            }
            ClampToCanvas(drt);

            SetDetailVisible(true);
            if (detail_text != null)
                detail_text.ForceMeshUpdate();
        }

        public void ScrollDetail(float delta)
        {
            if (detail_text == null || detail_group == null || detail_group.alpha == 0f || delta == 0f)
                return;
            detail_text.pageToDisplay = Mathf.Clamp(detail_text.pageToDisplay + (delta < 0f ? 1 : -1),
                1, Mathf.Max(1, detail_text.textInfo.pageCount));
        }

        private void StylePanel(CanvasGroup group)
        {
            if (group == null)
                return;
            UnityEngine.UI.Image image = group.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
                image.color = new Color32(16, 23, 36, 250);
        }

        public static string EscapeText(string text)
        {
            //Keep card names and literal comparisons from becoming TMP tags.
            return (text ?? "").Replace("<", "<\u200B");
        }

        public static string ScoreText(int score)
        {
            string color = score > 0 ? "69E0CD" : score < 0 ? "FF9DAB" : "BBC8DA";
            return "<color=#" + color + ">" + score.ToString("+#,0;-#,0;0") + "</color>";
        }

        private static string FormatReport(string report)
        {
            if (string.IsNullOrEmpty(report))
                return "분해 정보 없음";

            StringBuilder result = new StringBuilder();
            bool clubsStarted = false;
            int step = 0;
            foreach (string raw in report.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0)
                    continue;
                if (line.StartsWith("■ 예측"))
                {
                    ReportHeading(result, "01  예측 행동", "양측이 최선의 수를 선택했을 때의 진행 순서");
                    continue;
                }
                if (line.StartsWith("■ 리프"))
                {
                    ReportHeading(result, "02  기본 점수", "예측 경로 마지막 상태 · 수량 × 가중치 = 기여 점수");
                    ReportColumns(result, "항목", "나", "상대", "차이 (나 − 상대)");
                    continue;
                }
                Match path = Regex.Match(line, @"^(나|상대): (.*)$");
                if (path.Success)
                {
                    result.Append("<color=#9CAEC5>").Append((++step).ToString("00"))
                        .Append("</color>  ").Append(SideLabel(path.Groups[1].Value))
                        .Append("  ").AppendLine(EscapeText(path.Groups[2].Value));
                    continue;
                }
                Match split = Regex.Match(line, @"^(.+): 나 (.+) − 상대 (.+) = ([+-]?\d+)$");
                if (split.Success)
                {
                    ReportColumns(result, EscapeText(split.Groups[1].Value),
                        EscapeText(split.Groups[2].Value), EscapeText(split.Groups[3].Value),
                        ReportNumber(split.Groups[4].Value));
                    continue;
                }
                if (line.StartsWith("유닛 ("))
                {
                    ReportHeading(result, "03  유닛별 기여", line.Substring(3).TrimEnd(':'));
                    continue;
                }
                Match unit = Regex.Match(line, @"^\[(나|상대)\] (.*): 공(-?\d+)→(-?\d+), 체(-?\d+)→(-?\d+)(?:, 상태→(-?\d+))? \(계 (-?\d+)\)$");
                if (unit.Success)
                {
                    result.Append(SideLabel(unit.Groups[1].Value)).Append("  ")
                        .AppendLine(EscapeText(unit.Groups[2].Value));
                    ReportColumns(result, "<color=#9CAEC5>기여 점수</color>",
                        "공격 " + unit.Groups[3].Value + " → " + unit.Groups[4].Value,
                        "체력 " + unit.Groups[5].Value + " → " + unit.Groups[6].Value,
                        "상태 " + (unit.Groups[7].Success ? unit.Groups[7].Value : "0"));
                    result.Append("<color=#9CAEC5>기여 합계</color><pos=76%>")
                        .AppendLine(ReportNumber(unit.Groups[8].Value));
                    result.AppendLine();
                    continue;
                }
                Match club = Regex.Match(line, @"^\[(나|상대)\] 클럽 (.*) \((.*)\) → (-?\d+)$");
                if (club.Success)
                {
                    if (!clubsStarted)
                    {
                        ReportHeading(result, "04  클럽 시너지", "발동 조건과 해당 진영의 기여 점수");
                        clubsStarted = true;
                    }
                    result.Append(SideLabel(club.Groups[1].Value)).Append("  ")
                        .AppendLine(EscapeText(club.Groups[2].Value));
                    result.Append("<color=#9CAEC5>").Append(EscapeText(club.Groups[3].Value))
                        .Append("</color><pos=76%>").AppendLine(ReportNumber(club.Groups[4].Value));
                    continue;
                }
                if (line.StartsWith("합계(hvalue): "))
                {
                    ReportHeading(result, "최종 평가", "예측 경로 마지막 상태의 평가값");
                    result.Append("<size=120%>리프 평가 점수<pos=76%>")
                        .Append(ReportNumber(line.Substring("합계(hvalue): ".Length))).AppendLine("</size>");
                    continue;
                }
                Match subtotal = Regex.Match(line, @"^└ (.+) \(나 (-?\d+) − 상대 (-?\d+)\): ([+-]?\d+)$");
                if (subtotal.Success)
                {
                    ReportColumns(result, EscapeText(subtotal.Groups[1].Value), subtotal.Groups[2].Value,
                        subtotal.Groups[3].Value, ReportNumber(subtotal.Groups[4].Value));
                    continue;
                }
                //Preserve terminal-state bonuses, random modifiers and any future report fields.
                result.AppendLine(EscapeText(line));
            }
            return result.ToString();
        }

        private static void ReportHeading(StringBuilder result, string title, string caption)
        {
            if (result.Length > 0)
                result.AppendLine();
            result.Append("<color=#8FBAFF><size=110%>").Append(EscapeText(title)).AppendLine("</size></color>");
            result.Append("<size=85%><color=#9CAEC5>").Append(EscapeText(caption)).AppendLine("</color></size>");
        }

        private static void ReportColumns(StringBuilder result, string label, string mine, string theirs, string net)
        {
            //Percentage positions align columns independently of Pretendard's variable glyph widths.
            result.Append(label).Append("<pos=28%>").Append(mine)
                .Append("<pos=52%>").Append(theirs).Append("<pos=76%>").AppendLine(net);
        }

        private static string SideLabel(string side)
        {
            return "<color=#" + (side == "나" ? "69E0CD" : "FFB68F") + ">[" + side + "]</color>";
        }

        private static string ReportNumber(string value)
        {
            int number;
            return int.TryParse(value, out number) ? ScoreText(number) : EscapeText(value);
        }

        //Root canvas RectTransform that 'rt' lives under (used as the on-screen bounds).
        private RectTransform GetCanvasRect(RectTransform rt)
        {
            if (rt == null)
                return null;
            Canvas c = rt.GetComponentInParent<Canvas>();
            return c != null ? c.rootCanvas.transform as RectTransform : null;
        }

        //Shift 'rt' so all four corners stay inside the root canvas rect (no off-screen overflow).
        private void ClampToCanvas(RectTransform rt)
        {
            RectTransform canvas_rt = GetCanvasRect(rt);
            if (canvas_rt == null)
                return;

            Canvas.ForceUpdateCanvases(); //Make sure the resize above is reflected before reading corners

            Vector3[] rc = new Vector3[4];
            Vector3[] cc = new Vector3[4];
            rt.GetWorldCorners(rc);        //[0]=bottom-left, [2]=top-right
            canvas_rt.GetWorldCorners(cc);

            float dx = 0f, dy = 0f;
            if (rc[0].x < cc[0].x) dx += cc[0].x - rc[0].x; //past left edge -> push right
            if (rc[2].x > cc[2].x) dx -= rc[2].x - cc[2].x; //past right edge -> push left
            if (rc[0].y < cc[0].y) dy += cc[0].y - rc[0].y; //past bottom -> push up
            if (rc[2].y > cc[2].y) dy -= rc[2].y - cc[2].y; //past top -> push down

            if (dx != 0f || dy != 0f)
                rt.position += new Vector3(dx, dy, 0f);
        }

        //Called by an item row on pointer exit.
        public void HideDetail(AIDebugActionItem item)
        {
            SetDetailVisible(false);
        }

        private void SetDetailVisible(bool show)
        {
            if (detail_group == null)
                return;
            detail_group.alpha = show ? 1f : 0f;
            detail_group.interactable = false;   //Never steal interaction
            detail_group.blocksRaycasts = false; //Let hover pass through to the rows underneath
        }

        //Reuse the global loading panel (already blocks touch input) under its own request key.
        private void ShowNetworkLoading(bool show)
        {
            if (show)
                LoadPanel.Show(load_key);
            else
                LoadPanel.Hide(load_key);
        }

        private void SetStatus(string text)
        {
            if (status_text != null)
                status_text.text = text;
        }
    }
}
#endif
