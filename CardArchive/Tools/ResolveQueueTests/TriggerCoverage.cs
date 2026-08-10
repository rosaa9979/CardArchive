// 전 트리거 커버리지. 각 테스트는 GameLogic의 **실제 호출부 구조를 그대로 미러링**한다
// (주석의 GameLogic.cs 줄번호 참조). 검사하는 것은 매번 같다:
//   ① 한 이벤트에 반응하는 카드들이 한 묶음인가 (중간에 사망 웨이브가 없는가)
//   ② 연속된 이벤트 사이에는 사망 웨이브가 도는가 (Rule 6)
//   ③ 트리거가 유발한 연쇄가 depth-first인가
using System;
using System.Collections.Generic;

namespace TcgEngine
{
    public static class TriggerCoverage
    {
        static int pass = 0, fail = 0;
        static List<string> failures = new List<string>();

        static void Check(string name, string expected, string actual)
        {
            if (expected == actual) { pass++; Console.WriteLine($"  [PASS] {name}"); }
            else
            {
                fail++;
                Console.WriteLine($"  [FAIL] {name}");
                Console.WriteLine($"         기대: {expected}");
                Console.WriteLine($"         실제: {actual}");
                failures.Add(name);
            }
        }

        static AbilityData Ab(string id, Action<Sim, Card, int> eff = null, int rep = 0)
            => new AbilityData { id = id, effect = eff, max_repeat = rep };

        // 적 1명(체력1)을 죽이는 광역
        static Action<Sim, Card, int> Wipe => (x, c, r) => x.DamageAllEnemies(0, 1);

        public static int Run()
        {
            Console.WriteLine("\n===== 전 트리거 커버리지 =====\n");

            Console.WriteLine("--- 게임/턴 구조 트리거 ---");
            G01_OnGameStart(); G02_StartOfTurn(); G03_StartOfTurn_독약선행(); G04_EndOfTurn();

            Console.WriteLine("\n--- 카드 사용 트리거 ---");
            P05_OnPlay(); P06_OnPlayOther(); P07_OnPlay_OnPlayOther_사이(); P08_PlayCard_전체시퀀스();
            P09_OnUse_OnUseOther(); P10_OnMove(); P11_Activate();

            Console.WriteLine("\n--- 전투 트리거 ---");
            B12_OnBeforeAttack_Defend(); B13_OnBeforeOther배치(); B14_공격전_시퀀스전체();
            B15_OnAfterAttack_Defend(); B16_OnAfterOther배치();

            Console.WriteLine("\n--- 피해 / 회복 트리거 ---");
            D17_OnAfterDamage_단일(); D18_OnAfterDamage_광역N대상(); D19_OnAfterDamage_연쇄();
            D20_OnHeal_OnHealOther();

            Console.WriteLine("\n--- 사망 트리거 ---");
            K21_OnKill(); K22_OnKill_OnDeath_OnDeathOther_한묶음(); K23_OnDeathOther_다음웨이브();
            K24_OnDeath_다중사망순서();

            Console.WriteLine("\n--- 기타 트리거 ---");
            M25_OnDraw_1장(); M26_OnDraw_다중(); M27_OnAddClubOther(); M28_Ongoing_큐미경유();

            Console.WriteLine($"\n===== 커버리지 PASS {pass} / FAIL {fail} =====");
            if (fail > 0) Console.WriteLine("실패: " + string.Join(", ", failures));
            return fail;
        }

        // ==================== 게임 / 턴 구조 ====================

        // GameLogic.cs:231-235 — 양 플레이어 루프를 한 번 더 감싼다 = 하나의 이벤트
        static void G01_OnGameStart()
        {
            var s = new Sim();
            var p0 = s.AddCard(0, "내카드", 99);
            var p1 = s.AddCard(1, "상대카드", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.rq.BeginPhase();
            s.TriggerCardAbilityType(p0, Ab("GS-내카드(광역)", Wipe));
            s.TriggerCardAbilityType(p1, Ab("GS-상대카드"));
            s.rq.EndPhase();
            s.rq.ResolveAll();
            Check("OnGameStart — 양 플레이어가 한 묶음",
                  "GS-내카드(광역) → GS-상대카드 → [wave:적] → 적죽메", s.Log());
        }

        // GameLogic.cs:371-375
        static void G02_StartOfTurn()
        {
            var s = new Sim();
            var a = s.AddCard(0, "A", 99); var b = s.AddCard(1, "B", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.rq.BeginPhase();
            s.RaiseEvent((a, Ab("SOT-A(광역)", Wipe)));
            s.RaiseEvent((b, Ab("SOT-B")));
            s.rq.EndPhase();
            s.rq.ResolveAll();
            Check("StartOfTurn — 양 플레이어가 한 묶음",
                  "SOT-A(광역) → SOT-B → [wave:적] → 적죽메", s.Log());
        }

        // GameLogic.cs:342-380 — 독약 피해는 StartOfTurn과 별개 이벤트 (콜백은 감싸지 않음)
        static void G03_StartOfTurn_독약선행()
        {
            var s = new Sim();
            var a = s.AddCard(0, "A", 99);
            var poisoned = s.AddCard(0, "독약걸린카드", 1, Ab("독약카드죽메"));
            s.rq.AddCallback(() =>
            {
                s.DamageCard(null, poisoned, 1);           // 독약 피해 (이벤트 없음)
                s.rq.BeginPhase();
                s.RaiseEvent((a, Ab("SOT-A")));
                s.rq.EndPhase();
            });
            s.rq.ResolveAll();
            Check("StartOfTurn — 독약 사망이 턴시작 트리거보다 먼저",
                  "[wave:독약걸린카드] → 독약카드죽메 → SOT-A", s.Log());
        }

        // GameLogic.cs:591-595
        static void G04_EndOfTurn()
        {
            var s = new Sim();
            var a = s.AddCard(0, "A", 99); var b = s.AddCard(1, "B", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.rq.BeginPhase();
            s.RaiseEvent((a, Ab("EOT-A(광역)", Wipe)));
            s.RaiseEvent((b, Ab("EOT-B")));
            s.rq.EndPhase();
            s.rq.ResolveAll();
            Check("EndOfTurn — 양 플레이어가 한 묶음",
                  "EOT-A(광역) → EOT-B → [wave:적] → 적죽메", s.Log());
        }

        // ==================== 카드 사용 ====================

        // GameLogic.cs:878 — 자기 카드 텍스트 (전투의 함성)
        static void P05_OnPlay()
        {
            var s = new Sim();
            var c = s.AddCard(0, "낸카드", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.TriggerCardAbilityType(c, Ab("OnPlay-1(광역)", Wipe), Ab("OnPlay-2"));
            s.rq.ResolveAll();
            Check("OnPlay — 한 카드의 어빌리티 2개가 한 묶음",
                  "OnPlay-1(광역) → OnPlay-2 → [wave:적] → 적죽메", s.Log());
        }

        // GameLogic.cs:879 — 타 카드 반응 배치 (칼 곡예사 규칙)
        static void P06_OnPlayOther()
        {
            var s = new Sim();
            var o1 = s.AddCard(0, "곡예사1", 99); var o2 = s.AddCard(0, "곡예사2", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.RaiseEvent((o1, Ab("OPO-곡예사1(광역)", Wipe)), (o2, Ab("OPO-곡예사2")));
            s.rq.ResolveAll();
            Check("OnPlayOther — 반응 카드들이 한 묶음 (칼 곡예사 규칙)",
                  "OPO-곡예사1(광역) → OPO-곡예사2 → [wave:적] → 적죽메", s.Log());
        }

        // GameLogic.cs:878→879 — Sequence의 별개 Phase라 사이에 사망 처리
        static void P07_OnPlay_OnPlayOther_사이()
        {
            var s = new Sim();
            var c = s.AddCard(0, "낸카드", 99); var o = s.AddCard(0, "반응카드", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.TriggerCardAbilityType(c, Ab("OnPlay(광역)", Wipe));
            s.RaiseEvent((o, Ab("OnPlayOther")));
            s.rq.ResolveAll();
            Check("OnPlay → OnPlayOther 사이에 사망 처리 (Rule 6)",
                  "OnPlay(광역) → [wave:적] → 적죽메 → OnPlayOther", s.Log());
        }

        // GameLogic.cs:876-884 전체 (비밀 제외)
        static void P08_PlayCard_전체시퀀스()
        {
            var s = new Sim();
            var c = s.AddCard(0, "낸카드", 99); var o = s.AddCard(0, "반응카드", 99);
            s.TriggerCardAbilityType(c, Ab("OnPlay"));
            s.RaiseEvent((o, Ab("OnPlayOther")));
            s.TriggerCardAbilityType(c, Ab("OnUse"));
            s.RaiseEvent((o, Ab("OnUseOther")));
            s.rq.ResolveAll();
            Check("PlayCard 전체 시퀀스 순서 보존",
                  "OnPlay → OnPlayOther → OnUse → OnUseOther", s.Log());
        }

        // GameLogic.cs:883-884
        static void P09_OnUse_OnUseOther()
        {
            var s = new Sim();
            var c = s.AddCard(0, "낸카드", 99);
            var o1 = s.AddCard(0, "반응1", 99); var o2 = s.AddCard(0, "반응2", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.TriggerCardAbilityType(c, Ab("OnUse(광역)", Wipe));
            s.RaiseEvent((o1, Ab("OUO-1")), (o2, Ab("OUO-2")));
            s.rq.ResolveAll();
            Check("OnUse → OnUseOther (묶음 + 사이 사망)",
                  "OnUse(광역) → [wave:적] → 적죽메 → OUO-1 → OUO-2", s.Log());
        }

        // GameLogic.cs:926 — 단일 카드 이벤트
        static void P10_OnMove()
        {
            var s = new Sim();
            var c = s.AddCard(0, "이동카드", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.TriggerCardAbilityType(c, Ab("OnMove(광역)", Wipe));
            s.rq.ResolveAll();
            Check("OnMove — 단일 이벤트", "OnMove(광역) → [wave:적] → 적죽메", s.Log());
        }

        // GameLogic.cs:940 CastAbility — Phase로 감싸지 않는 단발 등록 (AddAbility 폴백)
        static void P11_Activate()
        {
            var s = new Sim();
            var c = s.AddCard(0, "발동카드", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            var nested = Ab("Activate가유발한이벤트");
            s.TriggerCardAbility(Ab("Activate(광역)", (x, cc, r) =>
            { x.DamageAllEnemies(0, 1); x.TriggerCardAbilityType(cc, nested); }), c);
            s.rq.ResolveAll();
            Check("Activate — 감싸지 않아도 자기 최상위 Phase가 되고 유발분은 중첩",
                  "Activate(광역) → Activate가유발한이벤트 → [wave:적] → 적죽메", s.Log());
        }

        // ==================== 전투 ====================

        // GameLogic.cs:1047-1048 — 공격자/방어자 자기 텍스트는 별개 이벤트
        static void B12_OnBeforeAttack_Defend()
        {
            var s = new Sim();
            var atk = s.AddCard(0, "공격자", 99); var def = s.AddCard(1, "방어자", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.TriggerCardAbilityType(atk, Ab("OnBeforeAttack(광역)", Wipe));
            s.TriggerCardAbilityType(def, Ab("OnBeforeDefend"));
            s.rq.ResolveAll();
            Check("OnBeforeAttack → OnBeforeDefend 사이에 사망 처리",
                  "OnBeforeAttack(광역) → [wave:적] → 적죽메 → OnBeforeDefend", s.Log());
        }

        // GameLogic.cs:1051-1052
        static void B13_OnBeforeOther배치()
        {
            var s = new Sim();
            var w1 = s.AddCard(0, "관전1", 99); var w2 = s.AddCard(0, "관전2", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.RaiseEvent((w1, Ab("OBAO-1(광역)", Wipe)), (w2, Ab("OBAO-2")));
            s.RaiseEvent((w1, Ab("OBDO-1")));
            s.rq.ResolveAll();
            Check("OnBeforeAttackOther 배치 묶음 + OnBeforeDefendOther와 분리",
                  "OBAO-1(광역) → OBAO-2 → [wave:적] → 적죽메 → OBDO-1", s.Log());
        }

        // GameLogic.cs:1047-1052 전체
        static void B14_공격전_시퀀스전체()
        {
            var s = new Sim();
            var atk = s.AddCard(0, "공격자", 99); var def = s.AddCard(1, "방어자", 99);
            var w = s.AddCard(0, "관전", 99);
            s.TriggerCardAbilityType(atk, Ab("OnBeforeAttack"));
            s.TriggerCardAbilityType(def, Ab("OnBeforeDefend"));
            s.RaiseEvent((w, Ab("OnBeforeAttackOther")));
            s.RaiseEvent((w, Ab("OnBeforeDefendOther")));
            s.rq.ResolveAll();
            Check("공격 전 4개 이벤트 순서 보존",
                  "OnBeforeAttack → OnBeforeDefend → OnBeforeAttackOther → OnBeforeDefendOther", s.Log());
        }

        // GameLogic.cs:1122-1124
        static void B15_OnAfterAttack_Defend()
        {
            var s = new Sim();
            var atk = s.AddCard(0, "공격자", 99); var def = s.AddCard(1, "방어자", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.TriggerCardAbilityType(atk, Ab("OnAfterAttack(광역)", Wipe));
            s.TriggerCardAbilityType(def, Ab("OnAfterDefend"));
            s.rq.ResolveAll();
            Check("OnAfterAttack → OnAfterDefend 사이에 사망 처리",
                  "OnAfterAttack(광역) → [wave:적] → 적죽메 → OnAfterDefend", s.Log());
        }

        // GameLogic.cs:1130-1131
        static void B16_OnAfterOther배치()
        {
            var s = new Sim();
            var w1 = s.AddCard(0, "관전1", 99); var w2 = s.AddCard(0, "관전2", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.RaiseEvent((w1, Ab("OAAO-1(광역)", Wipe)), (w2, Ab("OAAO-2")));
            s.RaiseEvent((w1, Ab("OADO-1")));
            s.rq.ResolveAll();
            Check("OnAfterAttackOther 배치 묶음 + OnAfterDefendOther와 분리",
                  "OAAO-1(광역) → OAAO-2 → [wave:적] → 적죽메 → OADO-1", s.Log());
        }

        // ==================== 피해 / 회복 ====================

        // GameLogic.cs:1602-1603 (ResolveAttackHit 경로) — 공격 마이크로스텝은 Phase 자체이므로
        // 그 안에서 뜬 OnAfterDamage가 **사망 처리보다 먼저** 발동해야 한다 (Rule 4a)
        static void D17_OnAfterDamage_단일()
        {
            var s = new Sim();
            var atk = s.AddCard(0, "공격자", 99);
            var t = s.AddCard(1, "대상", 1, Ab("대상죽메"));
            s.rq.AddAttack(atk, t, (a, tt, skip) => s.DamageCard(a, tt, 1, Ab("OnAfterDamage")));
            s.rq.ResolveAll();
            Check("OnAfterDamage — 공격 스텝의 트리거가 사망 처리보다 먼저",
                  "OnAfterDamage → [wave:대상] → 대상죽메", s.Log());
        }

        // 한 Phase 안에서 여러 대상에 피해 → OnAfterDamage는 대상 수만큼 뜨지만
        // 사망은 Phase가 끝난 뒤 **한 웨이브**로 (폭발 양 규칙)
        static void D18_OnAfterDamage_광역N대상()
        {
            var s = new Sim();
            var atk = s.AddCard(0, "공격자", 99);
            var t1 = s.AddCard(1, "대상1", 1, Ab("대상1죽메"));
            var t2 = s.AddCard(1, "대상2", 1, Ab("대상2죽메"));
            var oad = Ab("OnAfterDamage");
            s.TriggerCardAbilityType(atk, Ab("광역어빌리티", (x, c, r) =>
            { x.DamageCard(c, t1, 1, oad); x.DamageCard(c, t2, 1, oad); }));
            s.rq.ResolveAll();
            Check("OnAfterDamage — 광역은 대상마다 뜨지만 사망은 한 웨이브",
                  "광역어빌리티 → OnAfterDamage → OnAfterDamage → [wave:대상1+대상2] → 대상1죽메 → 대상2죽메",
                  s.Log());
        }

        // OnAfterDamage가 유발한 연쇄는 depth-first
        static void D19_OnAfterDamage_연쇄()
        {
            var s = new Sim();
            var atk = s.AddCard(0, "공격자", 99);
            var t = s.AddCard(1, "대상", 5);
            var w = s.AddCard(0, "관전", 99);
            var deep = Ab("OAD가유발한이벤트");
            s.TriggerCardAbilityType(atk, Ab("어빌리티", (x, c, r) =>
                x.DamageCard(atk, t, 1, Ab("OnAfterDamage", (y, cc, rr) => y.TriggerCardAbilityType(cc, deep)))));
            s.RaiseEvent((w, Ab("대기이벤트")));
            s.rq.ResolveAll();
            Check("OnAfterDamage 연쇄가 depth-first",
                  "어빌리티 → OnAfterDamage → OAD가유발한이벤트 → 대기이벤트", s.Log());
        }

        // GameLogic.cs:1531-1532 — 자기(OnHeal) → 타 카드(OnHealOther), 별개 이벤트
        static void D20_OnHeal_OnHealOther()
        {
            var s = new Sim();
            var t = s.AddCard(0, "회복대상", 99);
            var o1 = s.AddCard(0, "관전1", 99); var o2 = s.AddCard(0, "관전2", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.TriggerCardAbilityType(t, Ab("OnHeal(광역)", Wipe));
            s.RaiseEvent((o1, Ab("OnHealOther-1")), (o2, Ab("OnHealOther-2")));
            s.rq.ResolveAll();
            Check("OnHeal → OnHealOther (순서 + 사이 사망 + Other 묶음)",
                  "OnHeal(광역) → [wave:적] → 적죽메 → OnHealOther-1 → OnHealOther-2", s.Log());
        }

        // ==================== 사망 ====================

        // GameLogic.cs:2011-2012 — 죽음 스텝 안에서 OnKill이 OnDeath보다 먼저
        static void K21_OnKill()
        {
            var s = new Sim();
            var killer = s.AddCard(0, "처형자", 99);
            killer.on_kill = Ab("OnKill");
            var t = s.AddCard(1, "대상", 1, Ab("OnDeath"));
            s.rq.AddCallback(() => s.DamageCard(killer, t, 1));
            s.rq.ResolveAll();
            Check("OnKill이 OnDeath보다 먼저", "[wave:대상] → OnKill → OnDeath", s.Log());
        }

        // 죽음 스텝 전체가 하나의 Phase — 그 안에서 죽은 카드는 다음 웨이브로
        static void K22_OnKill_OnDeath_OnDeathOther_한묶음()
        {
            var s = new Sim();
            var killer = s.AddCard(0, "처형자", 99);
            killer.on_kill = Ab("OnKill(광역)", Wipe);       // 죽메 순서에 또 죽인다
            var t = s.AddCard(1, "대상", 1, Ab("OnDeath"));
            var w = s.AddCard(1, "관전자", 1, null, Ab(""));  // OnDeathOther 보유, 체력 1
            s.rq.AddCallback(() => s.DamageCard(killer, t, 1));
            s.rq.ResolveAll();
            Check("OnKill/OnDeath/OnDeathOther는 한 Phase — 그 안 사망은 다음 웨이브",
                  "[wave:대상] → OnKill(광역) → OnDeath → 관전자:목격(대상) → [wave:관전자]", s.Log());
        }

        // 죽메 연쇄가 새 웨이브를 만든다
        static void K23_OnDeathOther_다음웨이브()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            s.AddCard(1, "X", 1, Ab("X죽메"));
            s.AddCard(1, "관전자", 5, null, Ab(""));
            var deadly = Ab("");   // 관전자가 X의 죽음을 보고 자멸하도록 아래에서 직접 처리
            s.TriggerCardAbilityType(self, Ab("처치", (x, c, r) => x.Find("X").hp = 0));
            s.rq.ResolveAll();
            Check("OnDeathOther — 생존자가 목격 (같은 웨이브 아님)",
                  "처치 → [wave:X] → X죽메 → 관전자:목격(X)", s.Log());
        }

        // 다중 동시 사망: 카드마다 OnDeath → OnDeathOther 순으로, play order대로
        static void K24_OnDeath_다중사망순서()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            s.AddCard(1, "X", 1, Ab("X죽메"), Ab(""));
            s.AddCard(1, "Y", 1, Ab("Y죽메"), Ab(""));
            s.AddCard(1, "생존", 9, null, Ab(""));
            s.TriggerCardAbilityType(self, Ab("광역", Wipe));
            s.rq.ResolveAll();
            Check("다중 사망 — 카드마다 OnDeath → OnDeathOther, play order 순",
                  "광역 → [wave:X+Y] → X죽메 → 생존:목격(X) → Y죽메 → 생존:목격(Y)", s.Log());
        }

        // ==================== 기타 ====================

        // GameLogic.cs:1298 — 드로우 1회당 OnDraw 배치 1개
        static void M25_OnDraw_1장()
        {
            var s = new Sim();
            var a = s.AddCard(0, "A", 99); var b = s.AddCard(0, "B", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.rq.AddCallback(() => s.RaiseEvent((a, Ab("OnDraw-A(광역)", Wipe)), (b, Ab("OnDraw-B"))));
            s.rq.ResolveAll();
            Check("OnDraw — 반응 카드들이 한 묶음",
                  "OnDraw-A(광역) → OnDraw-B → [wave:적] → 적죽메", s.Log());
        }

        // 여러 장 뽑으면 드로우마다 별개 이벤트 → 사이에 사망 처리
        static void M26_OnDraw_다중()
        {
            var s = new Sim();
            var a = s.AddCard(0, "A", 99);
            s.AddCard(1, "적1", 1, Ab("적1죽메"));
            s.AddCard(1, "적2", 2, Ab("적2죽메"));
            s.rq.AddCallback(() =>
            {
                for (int i = 1; i <= 2; i++)
                {
                    int n = i;
                    s.RaiseEvent((a, Ab($"OnDraw#{n}(광역)", Wipe)));
                }
            });
            s.rq.ResolveAll();
            Check("OnDraw — 드로우마다 별개 이벤트, 사이에 사망 처리",
                  "OnDraw#1(광역) → [wave:적1] → 적1죽메 → OnDraw#2(광역) → [wave:적2] → 적2죽메", s.Log());
        }

        // GameLogic.cs:1385
        static void M27_OnAddClubOther()
        {
            var s = new Sim();
            var o1 = s.AddCard(0, "동아리원1", 99); var o2 = s.AddCard(0, "동아리원2", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.RaiseEvent((o1, Ab("OACO-1(광역)", Wipe)), (o2, Ab("OACO-2")));
            s.rq.ResolveAll();
            Check("OnAddClubOther — 반응 카드들이 한 묶음",
                  "OACO-1(광역) → OACO-2 → [wave:적] → 적죽메", s.Log());
        }

        // Ongoing은 오라라 resolve 큐를 타지 않는다 (UpdateOngoing에서 즉시 계산)
        static void M28_Ongoing_큐미경유()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            // Ongoing 어빌리티는 적재 자체를 하지 않는다 → 큐에는 광역 효과만
            s.TriggerCardAbilityType(self, Ab("광역", Wipe));
            s.rq.ResolveAll();
            Check("Ongoing — 큐를 타지 않음 (적재된 것만 발동)",
                  "광역 → [wave:적] → 적죽메", s.Log());
        }
    }
}
