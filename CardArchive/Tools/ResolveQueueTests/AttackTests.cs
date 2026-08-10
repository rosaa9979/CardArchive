// 전투 시나리오 테스트.
//   before attack → 데미지 교환 → after attack → death
//   그리고 필드 유닛을 한 번씩 순회하며 차례대로 공격하는 흐름
// 실제 GameLogic의 AttackCheck → AttackSearch → AttackTargets → AttackTarget →
// ResolveAttack → ResolveAttackHit → ResolveDeath 체인을 Sim.Attack.cs가 미러링한다.
using System;
using System.Collections.Generic;

namespace TcgEngine
{
    public static class AttackTests
    {
        static int pass = 0, fail = 0;
        static List<string> failures = new List<string>();

        static void Check(string name, string[] expected, List<string> actual)
        {
            string e = string.Join("\n", expected);
            string a = string.Join("\n", actual);
            if (e == a) { pass++; Console.WriteLine($"  [PASS] {name}"); }
            else
            {
                fail++;
                Console.WriteLine($"  [FAIL] {name}");
                Console.WriteLine("         기대:");
                foreach (string l in expected) Console.WriteLine($"           {l}");
                Console.WriteLine("         실제:");
                foreach (string l in actual) Console.WriteLine($"           {l}");
                failures.Add(name);
            }
        }

        static AbilityData Ab(string id, Action<Sim, Card, int> eff = null)
            => new AbilityData { id = id, effect = eff };

        static Card Unit(Sim s, int pid, string uid, int hp, int atk, bool front = false)
        {
            Card c = s.AddCard(pid, uid, hp);
            c.attack = atk;
            c.is_front = front;
            return c;
        }

        public static int Run()
        {
            Console.WriteLine("\n===== 전투 시나리오 =====\n");
            Console.WriteLine("--- 단일 교전 ---");
            W01(); W02(); W03(); W04(); W05();
            Console.WriteLine("\n--- 볼리 (다중 대상) ---");
            W06(); W07(); W08();
            Console.WriteLine("\n--- 필드 순회 ---");
            W09(); W10(); W11(); W12(); W13();

            Console.WriteLine($"\n===== 전투 PASS {pass} / FAIL {fail} =====");
            if (fail > 0) Console.WriteLine("실패: " + string.Join(", ", failures));
            return fail;
        }

        // ==================== 단일 교전 ====================

        // 기본 순서: 공격 전 트리거 → 데미지 → 공격 후 트리거 → 볼리 종료
        static void W01()
        {
            var s = new Sim();
            var a = Unit(s, 0, "아군", 5, 2);
            var d = Unit(s, 1, "적군", 5, 1);
            a.on_before_attack = Ab("[전]아군 OnBeforeAttack");
            d.on_before_defend = Ab("[전]적군 OnBeforeDefend");
            a.on_after_attack = Ab("[후]아군 OnAfterAttack");
            d.on_after_defend = Ab("[후]적군 OnAfterDefend");
            s.StartAttackPhase();
            s.rq.ResolveAll();
            Check("W01 단일 교전 기본 순서", new[]{
                "▶ 아군 공격 개시",
                "[전]아군 OnBeforeAttack",
                "[전]적군 OnBeforeDefend",
                "  ⚔ 아군(2) → 적군(체5)",
                "[후]아군 OnAfterAttack",
                "[후]적군 OnAfterDefend",
                "◀ 아군 공격 종료",
                "— 전투 종료 —",
            }, s.log);
        }

        // FRONT 무기 = 반격. 데미지 교환이 공격 후 트리거 **전**에 끝난다
        static void W02()
        {
            var s = new Sim();
            var a = Unit(s, 0, "아군", 5, 2, front: true);
            var d = Unit(s, 1, "적군", 5, 3);
            a.on_after_attack = Ab("[후]아군 OnAfterAttack");
            s.StartAttackPhase();
            s.rq.ResolveAll();
            Check("W02 데미지 교환 (반격)", new[]{
                "▶ 아군 공격 개시",
                "  ⚔ 아군(2) → 적군(체5)",
                "  ⚔ 반격 적군(3) → 아군(체5)",
                "[후]아군 OnAfterAttack",
                "◀ 아군 공격 종료",
                "— 전투 종료 —",
            }, s.log);
        }

        // 대상이 죽는 경우: 공격 후 트리거가 **사망 처리보다 먼저**, 죽메는 그 뒤
        static void W03()
        {
            var s = new Sim();
            var a = Unit(s, 0, "아군", 5, 3);
            var d = s.AddCard(1, "적군", 2, Ab("[死]적군 죽메"));
            d.attack = 1;
            a.on_after_attack = Ab("[후]아군 OnAfterAttack");
            d.on_after_defend = Ab("[후]적군 OnAfterDefend");
            s.StartAttackPhase();
            s.rq.ResolveAll();
            Check("W03 대상 사망 — 공격 후 트리거가 사망보다 먼저", new[]{
                "▶ 아군 공격 개시",
                "  ⚔ 아군(3) → 적군(체2)",
                "[후]아군 OnAfterAttack",
                "[후]적군 OnAfterDefend",   // 빈사 상태로도 반응한다
                "◀ 아군 공격 종료",
                "[wave:적군]",
                "[死]적군 죽메",
                "— 전투 종료 —",
            }, s.log);
        }

        // 반격으로 공격자도 죽는 경우 — 둘이 **같은 웨이브**
        static void W04()
        {
            var s = new Sim();
            var a = s.AddCard(0, "아군", 2, Ab("[死]아군 죽메"));
            a.attack = 3; a.is_front = true;
            var d = s.AddCard(1, "적군", 2, Ab("[死]적군 죽메"));
            d.attack = 3;
            s.StartAttackPhase();
            s.rq.ResolveAll();
            Check("W04 상호 사망 — 같은 웨이브", new[]{
                "▶ 아군 공격 개시",
                "  ⚔ 아군(3) → 적군(체2)",
                "  ⚔ 반격 적군(3) → 아군(체2)",
                "◀ 아군 공격 종료",
                "[wave:아군+적군]",
                "[死]아군 죽메",
                "[死]적군 죽메",
                "— 전투 종료 —",
            }, s.log);
        }

        // 공격 전 트리거가 대상을 죽여도, 사망 처리는 볼리가 끝난 뒤
        static void W05()
        {
            var s = new Sim();
            var a = Unit(s, 0, "아군", 5, 1);
            var d = s.AddCard(1, "적군", 3, Ab("[死]적군 죽메"));
            d.attack = 1;
            a.on_before_attack = Ab("[전]아군 OnBeforeAttack — 적군에 3피해",
                (x, c, r) => x.DamageCard(c, d, 3));
            s.StartAttackPhase();
            s.rq.ResolveAll();
            Check("W05 공격 전 트리거로 대상이 빈사 — 그래도 타격은 진행", new[]{
                "▶ 아군 공격 개시",
                "[전]아군 OnBeforeAttack — 적군에 3피해",
                "  ⚔ 아군(1) → 적군(체0)",
                "◀ 아군 공격 종료",
                "[wave:적군]",
                "[死]적군 죽메",
                "— 전투 종료 —",
            }, s.log);
        }

        // ==================== 볼리 (다중 대상) ====================

        // 대상 2명을 순차 타격. 첫 대상이 죽어도 볼리가 끝날 때까지 제거되지 않는다
        static void W06()
        {
            var s = new Sim();
            var a = Unit(s, 0, "아군", 9, 3);
            var d1 = s.AddCard(1, "적1", 2, Ab("[死]적1 죽메"));
            var d2 = s.AddCard(1, "적2", 5, Ab("[死]적2 죽메"));
            d1.attack = 1; d2.attack = 1;
            s.StartAttackPhase();
            s.rq.ResolveAll();
            Check("W06 볼리 — 첫 대상 사망이 두 번째 타격을 막지 않음", new[]{
                "▶ 아군 공격 개시",
                "  ⚔ 아군(3) → 적1(체2)",
                "  ⚔ 아군(3) → 적2(체5)",
                "◀ 아군 공격 종료",
                "[wave:적1]",
                "[死]적1 죽메",
                "— 전투 종료 —",
            }, s.log);
        }

        // 볼리로 둘 다 죽으면 **한 웨이브**로 동시 제거
        static void W07()
        {
            var s = new Sim();
            var a = Unit(s, 0, "아군", 9, 3);
            var d1 = s.AddCard(1, "적1", 2, Ab("[死]적1 죽메"));
            var d2 = s.AddCard(1, "적2", 2, Ab("[死]적2 죽메"));
            d1.attack = 1; d2.attack = 1;
            s.StartAttackPhase();
            s.rq.ResolveAll();
            Check("W07 볼리 — 둘 다 죽으면 한 웨이브", new[]{
                "▶ 아군 공격 개시",
                "  ⚔ 아군(3) → 적1(체2)",
                "  ⚔ 아군(3) → 적2(체2)",
                "◀ 아군 공격 종료",
                "[wave:적1+적2]",
                "[死]적1 죽메",
                "[死]적2 죽메",
                "— 전투 종료 —",
            }, s.log);
        }

        // overkill guard: 앞선 트리거가 미리 죽여둔 대상은 볼리에서 건너뛴다
        static void W08()
        {
            var s = new Sim();
            var a = Unit(s, 0, "아군", 9, 3);
            var d1 = s.AddCard(1, "적1", 5, Ab("[死]적1 죽메"));
            var d2 = s.AddCard(1, "적2", 5, Ab("[死]적2 죽메"));
            d1.attack = 1; d2.attack = 1;
            // 적1을 때릴 때 적2를 미리 빈사로 만든다
            a.on_after_attack = Ab("[후]아군 OnAfterAttack — 적2에 5피해",
                (x, c, r) => x.DamageCard(c, d2, 5));
            s.StartAttackPhase();
            s.rq.ResolveAll();
            Check("W08 볼리 — 이미 빈사인 대상은 건너뜀 (overkill guard)", new[]{
                "▶ 아군 공격 개시",
                "  ⚔ 아군(3) → 적1(체5)",
                "[후]아군 OnAfterAttack — 적2에 5피해",
                "◀ 아군 공격 종료",       // 적2는 빈사라 스킵
                "[wave:적2]",
                "[死]적2 죽메",
                "— 전투 종료 —",
            }, s.log);
        }

        // ==================== 필드 순회 ====================

        // 아군 3유닛이 순서대로 한 번씩 공격
        static void W09()
        {
            var s = new Sim();
            Unit(s, 0, "아군1", 9, 1);
            Unit(s, 0, "아군2", 9, 1);
            Unit(s, 0, "아군3", 9, 1);
            var d = Unit(s, 1, "적군", 99, 0);
            s.StartAttackPhase();
            s.rq.ResolveAll();
            Check("W09 필드 순회 — 3유닛이 차례대로 1회씩", new[]{
                "▶ 아군1 공격 개시",
                "  ⚔ 아군1(1) → 적군(체99)",
                "◀ 아군1 공격 종료",
                "▶ 아군2 공격 개시",
                "  ⚔ 아군2(1) → 적군(체98)",
                "◀ 아군2 공격 종료",
                "▶ 아군3 공격 개시",
                "  ⚔ 아군3(1) → 적군(체97)",
                "◀ 아군3 공격 종료",
                "— 전투 종료 —",
            }, s.log);
        }

        // 앞 유닛이 죽인 적은 다음 유닛이 공격하기 **전에** 제거되어 있다
        static void W10()
        {
            var s = new Sim();
            Unit(s, 0, "아군1", 9, 3);
            Unit(s, 0, "아군2", 9, 3);
            var d1 = s.AddCard(1, "적1", 2, Ab("[死]적1 죽메"));
            var d2 = s.AddCard(1, "적2", 9, Ab("[死]적2 죽메"));
            d1.attack = 0; d2.attack = 0;
            s.StartAttackPhase();
            s.rq.ResolveAll();
            Check("W10 순회 — 앞 유닛이 죽인 적은 다음 유닛 차례 전에 정리됨", new[]{
                "▶ 아군1 공격 개시",
                "  ⚔ 아군1(3) → 적1(체2)",
                "  ⚔ 아군1(3) → 적2(체9)",
                "◀ 아군1 공격 종료",
                "[wave:적1]",
                "[死]적1 죽메",
                "▶ 아군2 공격 개시",
                "  ⚔ 아군2(3) → 적2(체6)",   // 적1은 이미 사라져 대상에 없다
                "◀ 아군2 공격 종료",
                "— 전투 종료 —",
            }, s.log);
        }

        // 반격으로 공격자가 죽으면, 그 유닛의 볼리는 끝나고 다음 유닛이 이어받는다
        static void W11()
        {
            var s = new Sim();
            var a1 = s.AddCard(0, "아군1", 2, Ab("[死]아군1 죽메"));
            a1.attack = 1; a1.is_front = true;
            Unit(s, 0, "아군2", 9, 1);
            var d1 = Unit(s, 1, "적1", 9, 5);
            var d2 = Unit(s, 1, "적2", 9, 0);
            s.StartAttackPhase();
            s.rq.ResolveAll();
            Check("W11 순회 — 반격으로 죽은 공격자의 볼리는 무산되고 다음 유닛으로", new[]{
                "▶ 아군1 공격 개시",
                "  ⚔ 아군1(1) → 적1(체9)",
                "  ⚔ 반격 적1(5) → 아군1(체2)",
                "  ⚔ 아군1(1) → 적2(체9)",       // 빈사여도 보드에 있어 볼리는 계속
                "  ⚔ 반격 적2(0) → 아군1(체-3)",
                "◀ 아군1 공격 종료",
                "[wave:아군1]",
                "[死]아군1 죽메",
                "▶ 아군2 공격 개시",
                "  ⚔ 아군2(1) → 적1(체8)",
                "  ⚔ 아군2(1) → 적2(체8)",
                "◀ 아군2 공격 종료",
                "— 전투 종료 —",
            }, s.log);
        }

        // 트리거도 죽메도 전혀 없는 순수 공격. 큐에 아무것도 안 쌓여도 사망 처리가 도는가?
        static void W13()
        {
            var s = new Sim();
            Unit(s, 0, "아군", 5, 3);
            Unit(s, 1, "적군", 2, 0);   // 죽메 없음, 트리거 없음
            s.StartAttackPhase();
            s.rq.ResolveAll();
            Check("W13 순수 공격 — 큐가 비어도 사망 처리가 돈다", new[]{
                "▶ 아군 공격 개시",
                "  ⚔ 아군(3) → 적군(체2)",
                "◀ 아군 공격 종료",
                "[wave:적군]",          // ← 적재된 효과가 하나도 없는데도 웨이브가 돈다
                "— 전투 종료 —",
            }, s.log);
        }

        // 적이 전멸하면 남은 아군은 대상 없이 넘어간다
        static void W12()
        {
            var s = new Sim();
            Unit(s, 0, "아군1", 9, 5);
            Unit(s, 0, "아군2", 9, 5);
            var d = s.AddCard(1, "적군", 3, Ab("[死]적군 죽메"));
            d.attack = 0;
            s.StartAttackPhase();
            s.rq.ResolveAll();
            Check("W12 순회 — 적 전멸 후 남은 유닛은 대상 없음", new[]{
                "▶ 아군1 공격 개시",
                "  ⚔ 아군1(5) → 적군(체3)",
                "◀ 아군1 공격 종료",
                "[wave:적군]",
                "[死]적군 죽메",
                "▶ 아군2 공격 개시",
                "◀ 아군2 대상 없음",
                "— 전투 종료 —",
            }, s.log);
        }
    }
}
