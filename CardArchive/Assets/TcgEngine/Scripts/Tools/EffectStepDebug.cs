using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// EDITOR-ONLY 치트. 효과 처리(ResolveQueue)를 한 항목씩 손으로 진행시키고, 지금 대기 중인
    /// 효과와 직전 스텝에서 실제로 실행된 EffectData를 들여다보기 위한 디버그 훅 모음이다.
    /// AiDebugGame 씬의 EffectStepPanel이 이 클래스를 조작한다.
    ///
    /// ── 정상 동작에 영향을 주지 않기 위한 규칙 ────────────────────────────────────
    ///  1) 상태 필드와 모든 메서드 본문이 #if UNITY_EDITOR 안에 있다. 빌드에서는 필드가 하나도
    ///     남지 않고, 훅 메서드는 [Conditional("UNITY_EDITOR")]라 **호출 지점 자체가 컴파일러에
    ///     의해 제거**된다 (호출부에 #if를 흩뿌리지 않아도 되는 이유).
    ///  2) 치트가 꺼져 있으면(enabled=false, 기본값) 훅은 static bool 하나만 읽고 즉시 돌아간다.
    ///     ResolveQueue 쪽 게이트도 전부 enabled를 먼저 본다.
    ///  3) 등록되는 큐는 **실게임 큐 하나뿐**이다 (ResolveQueue 생성자에서 skip_delay=false일 때만).
    ///     AI 예측용 GameLogic(true)의 큐는 등록되지 않으므로 minimax 계산은 절대 멈추지 않는다.
    ///  4) EffectData 로그는 "스텝을 실행 중인 그 스레드"에서 온 것만 받는다 (capture_thread).
    ///     AI 워커 스레드가 동시에 DoEffects를 돌려도 로그 리스트를 건드리지 못한다.
    /// ─────────────────────────────────────────────────────────────────────────────
    /// </summary>
    public static class EffectStepDebug
    {
#if UNITY_EDITOR
        //한 스텝에서 기록할 EffectData 최대 개수 (폭주하는 효과에서 리스트가 무한히 커지는 것 방지)
        private const int max_effect_log = 200;

        //실게임의 ResolveQueue. skip_delay=false인 큐만 등록된다.
        private static ResolveQueue queue;

        //치트 on/off. EffectStepPanel만 바꾼다. 기본 false = 완전 무개입.
        private static bool enabled;

        //"다음 하나를 처리해도 좋다"는 1회용 토큰. BeginStep에서 소비된다.
        private static bool step_requested;

        //스텝을 실행 중인 스레드 id. -1이면 캡처 중이 아님.
        private static int capture_thread = -1;

        private static int step_count;
        private static readonly List<string> effect_log = new List<string>();

        //대기 목록. 항목 객체를 재사용하므로 0.1초마다 갱신해도 GC가 돌지 않는다.
        private static readonly PendingList pending = new PendingList();

        //Enter Play Mode Options로 도메인 리로드를 끈 경우 static 상태가 세션을 넘어 살아남는다.
        //치트가 켜진 채로 다른 씬에 들어가는 사고를 막기 위해 플레이 진입마다 초기화한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            queue = null;
            enabled = false;
            step_requested = false;
            capture_thread = -1;
            step_count = 0;
            effect_log.Clear();
            pending.Reset(0);
        }

        /// <summary>대기 목록의 한 줄. 항목 객체는 재사용되므로 참조를 보관하지 말 것.</summary>
        public class PendingEntry
        {
            public string tag;    //유발 / 연쇄 / 유발공격 / 사망 / 비밀 / 공격 / 진행
            public string title;  //어빌리티 이름, 스텝 이름 등
            public string source; //시전자 / 공격자 (없으면 null)
            public int depth;     //Phase 트리 깊이. 0 = 최상위 (들여쓰기용)
        }

        /// <summary>ResolveQueue가 채우고 EffectStepPanel이 읽는 고정 용량 버퍼.</summary>
        public class PendingList
        {
            private readonly List<PendingEntry> entries = new List<PendingEntry>();
            private int count;
            private int max;

            public int Count { get { return count; } }
            public bool IsFull { get { return count >= max; } }
            public PendingEntry this[int i] { get { return entries[i]; } }

            public void Reset(int max)
            {
                this.max = max;
                count = 0;
            }

            public void Add(string tag, string title, string source, int depth)
            {
                if (count >= max)
                    return;
                PendingEntry e;
                if (count < entries.Count)
                {
                    e = entries[count];
                }
                else
                {
                    e = new PendingEntry();
                    entries.Add(e);
                }
                e.tag = tag;
                e.title = title;
                e.source = source;
                e.depth = depth;
                count++;
            }
        }
#endif

        // ======================= ResolveQueue / AbilityData 훅 =======================

        /// <summary>실게임 ResolveQueue가 자기 자신을 등록한다 (생성자).</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void Register(ResolveQueue q)
        {
#if UNITY_EDITOR
            queue = q;
            step_requested = false;
            capture_thread = -1;
            step_count = 0;
            effect_log.Clear();
#endif
        }

        /// <summary>한 항목을 처리하기 직전. 토큰을 소비하고 EffectData 캡처를 연다.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void BeginStep(ResolveQueue q)
        {
#if UNITY_EDITOR
            if (!enabled || q == null || q != queue)
                return;
            step_requested = false;
            capture_thread = Thread.CurrentThread.ManagedThreadId;
            effect_log.Clear();
#endif
        }

        /// <summary>한 항목 처리가 끝난 직후. 캡처를 닫는다.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void EndStep(ResolveQueue q)
        {
#if UNITY_EDITOR
            if (capture_thread < 0 || q == null || q != queue)
                return;
            capture_thread = -1;
            step_count++;
#endif
        }

        /// <summary>
        /// AbilityData.DoEffects가 EffectData 하나를 실행하기 직전에 부른다.
        /// 치트가 꺼져 있으면 static bool 한 번 읽고 끝난다. 빌드에서는 호출 자체가 사라진다.
        ///
        /// 대상 타입마다 오버로드를 두는 이유: object 하나로 받으면 값 형식인 Slot이 호출 지점에서
        /// **매번 박싱**된다. 빌드에서는 호출 자체가 사라지니 상관없지만, 에디터에서는 AI 예측이
        /// 이 경로를 수만 번 돌기 때문에 치트가 꺼져 있어도 GC 부담이 생긴다.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void LogEffect(AbilityData ability, EffectData effect, Card caster)
        {
#if UNITY_EDITOR
            if (!CanCapture(effect)) return;
            Append(effect, "(대상 없음)");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void LogEffect(AbilityData ability, EffectData effect, Card caster, Card target)
        {
#if UNITY_EDITOR
            if (!CanCapture(effect)) return;
            Append(effect, DescribeCard(target));
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void LogEffect(AbilityData ability, EffectData effect, Card caster, Player target)
        {
#if UNITY_EDITOR
            if (!CanCapture(effect)) return;
            Append(effect, target != null ? "플레이어 " + target.player_id : "(없음)");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void LogEffect(AbilityData ability, EffectData effect, Card caster, Slot target)
        {
#if UNITY_EDITOR
            if (!CanCapture(effect)) return;
            Append(effect, DescribeSlot(target));
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void LogEffect(AbilityData ability, EffectData effect, Card caster, CardData target)
        {
#if UNITY_EDITOR
            if (!CanCapture(effect)) return;
            Append(effect, target != null ? target.GetTitle() : "(없음)");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void LogEffect(AbilityData ability, EffectData effect, Card caster, List<Card> target)
        {
#if UNITY_EDITOR
            if (!CanCapture(effect)) return;
            Append(effect, target != null ? "카드 " + target.Count + "장" : "(없음)");
#endif
        }

#if UNITY_EDITOR
        //기록해도 되는 상황인가. 치트가 꺼져 있으면 static bool 하나만 읽고 끝난다.
        private static bool CanCapture(EffectData effect)
        {
            if (!enabled || effect == null)
                return false;
            //스텝을 실행 중인 스레드가 아니면 무시 — AI 예측(워커 스레드)이 여기로 들어오지 못한다.
            if (capture_thread != Thread.CurrentThread.ManagedThreadId)
                return false;
            return effect_log.Count < max_effect_log;
        }

        private static void Append(EffectData effect, string target_desc)
        {
            string line = effect.GetType().Name;
            if (!string.IsNullOrEmpty(effect.name) && effect.name != line)
                line += " (" + effect.name + ")";
            effect_log.Add(line + "  →  " + target_desc);
        }
#endif

#if UNITY_EDITOR

        /// <summary>이 큐가 지금 치트에 붙잡혀 있는가 (= 스텝 요청 없이는 다음 항목을 처리하면 안 됨).</summary>
        public static bool IsPausing(ResolveQueue q)
        {
            return enabled && q != null && q == queue && !step_requested;
        }

        // ======================= EffectStepPanel용 API =======================

        public static bool IsEnabled { get { return enabled; } }
        public static bool HasQueue { get { return queue != null; } }
        public static int StepCount { get { return step_count; } }

        public static void SetEnabled(bool on)
        {
            if (enabled == on)
                return;
            enabled = on;
            step_requested = false;
            capture_thread = -1;
            if (!on)
            {
                effect_log.Clear();
                //붙잡혀 있던 큐는 delay가 0인 채로 잠들어 있다. 명시적으로 다시 굴려줘야 한다.
                if (queue != null)
                    queue.EditorResolveKick();
            }
        }

        /// <summary>다음 항목 하나를 처리한다.</summary>
        public static void Step()
        {
            if (!enabled || queue == null)
                return;
            step_requested = true;
            queue.EditorResolveKick();
            //처리되지 못했다면(선택 대기/게임 종료 등) 토큰을 남기지 않는다 — 나중에 혼자 한 칸
            //진행해버리는 것을 막기 위해.
            step_requested = false;
        }

        /// <summary>직전 스텝에서 실제로 실행된 EffectData 목록.</summary>
        public static List<string> GetEffectLog()
        {
            return effect_log;
        }

        /// <summary>지금 대기 중인 효과 목록 (처리될 순서 추정치). 반환 버퍼는 매번 재사용된다.</summary>
        public static PendingList GetPending(int max)
        {
            pending.Reset(max);
            if (queue != null)
                queue.EditorCollectPending(pending);
            return pending;
        }

        public static string GetStatusText()
        {
            if (queue == null)
                return "게임 없음";
            if (!enabled)
                return "치트 꺼짐 (정상 진행)";
            if (queue.EditorIsWaitingSelector())
                return "플레이어 선택 대기 중 — 선택을 마쳐야 진행됩니다";
            if (!queue.EditorHasPendingWork())
                return "대기 중인 효과 없음";
            return "일시정지 — 스텝 " + step_count;
        }

        public static string DescribeSlot(Slot slot)
        {
            if (!slot.IsValid())
                return "(슬롯 없음)";
            return "슬롯 (" + slot.x + "," + slot.y + ",p" + slot.p + ")";
        }

        //메인 스레드에서만 불린다 (Card.CardData가 Resources를 만지므로).
        public static string DescribeCard(Card card)
        {
            if (card == null)
                return "(없음)";
            CardData cdata = card.CardData;
            string title = cdata != null ? cdata.GetTitle() : null;
            return string.IsNullOrEmpty(title) ? card.card_id : title;
        }

        public static string DescribeAbility(AbilityData ability)
        {
            if (ability == null)
                return "(없음)";
            string title = ability.GetTitle();
            return string.IsNullOrEmpty(title) ? ability.id : title;
        }
#endif
    }
}
