// 개편 효과를 한눈에 보여주는 대표 시나리오.
// 카드 한 장을 내는 것만으로 Phase 3의 핵심 규칙 4개가 전부 드러난다:
//   ① 이벤트 사이에는 사망 처리가 돈다 (Rule 6)
//   ② 죽메는 대기 중인 다음 이벤트보다 먼저
//   ③ 한 이벤트에 반응하는 카드들 사이에는 사망 처리가 없다 (칼 곡예사 규칙)
//   ④ 그래서 빈사 카드가 대상 수 계산에 그대로 잡힌다
using System;

namespace TcgEngine
{
    public static class Showcase
    {
        static AbilityData Ab(string id, Action<Sim, Card, int> eff = null)
            => new AbilityData { id = id, effect = eff };

        public static void Run()
        {
            Console.WriteLine("\n===== 대표 시나리오: 신입생 1장을 냈을 때 =====");
            Console.WriteLine("\n[개편 전] 반응 카드가 낱개로 적재되던 시절 (묶음 없음)");
            Print(Trace(false));
            Console.WriteLine("\n[개편 후] 한 이벤트 = 한 묶음");
            Print(Trace(true));
            Console.WriteLine("\n[개편 후 · 중첩 깊이 표시] dN = 그 효과가 몇 겹 안에서 발동했는가");
            Print(Trace(true, depth: true));
            DepthDemo();
            QueueDemo();
            NestDemo();
        }

        /// 효과가 몇 겹으로 중첩되든 Queue의 최상위 element는 하나 그대로인가?
        static AbilityData BuildNest(int level, int max)
        {
            if (level > max)
                return Ab($"{level - 1}단에서 멈춤", (x, c, r) =>
                    x.log.Add($"     [최상위 큐 = {x.rq.TopPhaseCount()}, 깊이 = {x.rq.CurrentDepth()}]"));

            AbilityData inner = BuildNest(level + 1, max);
            return Ab($"{level}단 효과", (x, c, r) =>
            {
                x.log.Add($"     [최상위 큐 = {x.rq.TopPhaseCount()}, 깊이 = {x.rq.CurrentDepth()}]");
                x.TriggerCardAbilityType(c, inner);   // 더 깊이 중첩
            });
        }

        static void NestDemo()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);

            s.TriggerCardAbilityType(self, BuildNest(1, 5));
            s.TriggerCardAbilityType(self, Ab("뒤에 대기 중인 별개 이벤트"));

            Console.WriteLine("\n[5단 중첩 — Queue 최상위 element 수는?]");
            Console.WriteLine($"     [루프 시작 전 · 최상위 큐 = {s.rq.TopPhaseCount()}]");
            s.rq.ResolveAll();
            Print(s.log);
        }

        /// 효과 처리 **중**에 뜬 이벤트가 Queue의 새 최상위 element가 되는가? → 아니다.
        static void QueueDemo()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);

            var nested = Ab("  ↳ 처리 중 뜬 이벤트");
            var e1 = Ab("이벤트1", (x, c, r) =>
            {
                x.log.Add($"     [이벤트1 처리 중 · 최상위 큐 = {x.rq.TopPhaseCount()}]");
                x.TriggerCardAbilityType(c, nested);
                x.log.Add($"     [중첩 이벤트 등록 후 · 최상위 큐 = {x.rq.TopPhaseCount()}]");
            });
            var e2 = Ab("이벤트2");

            // 효과 처리 중이 아닐 때 → 최상위로 등록
            s.TriggerCardAbilityType(self, e1);
            s.TriggerCardAbilityType(self, e2);

            Console.WriteLine("\n[Queue의 최상위 element 수 변화]");
            Console.WriteLine($"     [루프 시작 전 · 최상위 큐 = {s.rq.TopPhaseCount()}]  ← 이벤트1, 이벤트2");
            s.rq.ResolveAll();
            Print(s.log);
        }

        /// 효과 처리 중 뭔가를 등록하는 두 경로의 차이:
        ///   chain  → Phase를 열지 않고 현재 스코프의 chains 큐에 **항목만**
        ///   이벤트 → BeginPhase로 **새 Phase**를 자식으로 열고 그 안에 적재
        static void DepthDemo()
        {
            var s = new Sim();
            s.show_depth = true;
            var self = s.AddCard(0, "SELF", 99);

            var chained = Ab("chain 등록 (Phase 안 열림)");
            var evented = Ab("이벤트 등록 (새 Phase)");
            var main = Ab("본 효과", (x, c, r) =>
            {
                x.TriggerCardAbilityType(c, evented);      // 이벤트 → BeginPhase
                x.TriggerCardAbility(chained, c, true);    // chain → 항목만
            });
            var sibling = Ab("같은 묶음의 형제");

            s.RaiseEvent((self, main), (self, sibling));
            s.rq.ResolveAll();

            Console.WriteLine("\n[chain vs 이벤트 — 등록 경로별 깊이]");
            Print(s.log);
        }

        static void Print(System.Collections.Generic.List<string> log)
        {
            int i = 0;
            foreach (string line in log)
                Console.WriteLine($"  {++i,2}. {line}");
        }

        // wrap=false 는 개편 전 재현: 반응 카드를 묶지 않고 낱개로 적재하면
        // 각자 자기 최상위 Phase가 되어 사이사이 사망 웨이브가 돈다 (= 옛 base 큐 동작).
        static System.Collections.Generic.List<string> Trace(bool wrap, bool depth = false)
        {
            var s = new Sim();
            s.show_depth = depth;

            // 내 필드 (play order 순)
            var gab = s.AddCard(0, "곡예사甲", 99);
            var eul = s.AddCard(0, "곡예사乙", 99);

            // 적 필드
            Card A = null, B = null;
            var drawReact = Ab("  ↳ 드로우 반응 트리거");
            A = s.AddCard(1, "적A", 1, Ab("적A 죽메 — 내 유닛 강화"));
            B = s.AddCard(1, "적B", 2, Ab("적B 죽메 — 카드 1장 뽑기",
                    (x, c, r) => x.RaiseEvent((gab, drawReact))));   // 드로우 → OnDraw 이벤트

            var rookie = s.AddCard(0, "신입생", 99);

            // ── PlayCard(신입생) 시퀀스 (GameLogic.cs:878-879) ──
            var onPlay = Ab("신입생 OnPlay — 적A에 1 피해", (x, c, r) => x.DamageCard(c, A, 1));
            // 곡예사甲의 피해가 甲 자신의 OnAfterDamage를 깨운다 = 효과 적용 중 다른 효과 유발
            var oad = Ab("  ↳ 곡예사甲 OnAfterDamage — 적B에 1 더 피해", (x, c, r) => x.DamageCard(c, B, 1));
            var juggler1 = Ab("곡예사甲 OnPlayOther — 적B에 1 피해", (x, c, r) => x.DamageCard(c, B, 1, oad));
            var juggler2 = Ab("곡예사乙", (x, c, r) =>
                x.Relabel($"곡예사乙 OnPlayOther — 적 {x.EnemyCount(0)}명만큼 회복"));

            if (wrap)
            {
                s.TriggerCardAbilityType(rookie, onPlay);            // 이벤트 1
                s.RaiseEvent((gab, juggler1), (eul, juggler2));      // 이벤트 2 (2장이 한 묶음)
            }
            else
            {
                s.TriggerCardAbility(onPlay, rookie);                // 각자 최상위 = 옛 동작
                s.TriggerCardAbility(juggler1, gab);
                s.TriggerCardAbility(juggler2, eul);
            }

            s.rq.ResolveAll();
            return s.log;
        }
    }
}
