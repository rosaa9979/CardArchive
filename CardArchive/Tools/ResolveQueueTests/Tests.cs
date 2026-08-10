// 신규 30종 시나리오. 이전 23종과 겹치지 않도록 새로 구성했다.
using System;
using System.Collections.Generic;

namespace TcgEngine
{
    public static class Program
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

        static AbilityData Ab(string id, Action<Sim, Card, int> eff = null, int rep = 0, Func<Sim, bool> cond = null)
            => new AbilityData { id = id, effect = eff, max_repeat = rep, repeat_condition = cond };

        public static void Main()
        {
            Console.WriteLine("===== 신규 30종 시나리오 (수정된 ResolveQueue) =====\n");
            Console.WriteLine("--- A. 이벤트 묶음 / 형제 순서 ---");
            A01(); A02(); A03(); A04(); A05(); A06(); A07();
            Console.WriteLine("\n--- B. 중첩 / depth-first ---");
            B08(); B09(); B10(); B11(); B12(); B13();
            Console.WriteLine("\n--- C. 죽음 처리 ---");
            C14(); C15(); C16(); C17(); C18(); C19(); C20(); C21(); C22();
            Console.WriteLine("\n--- D. 반복 (모독 / 요그사론) ---");
            D23(); D24(); D25(); D26(); D27(); D28(); D29();
            Console.WriteLine("\n--- E. selector ---");
            E30();

            Console.WriteLine($"\n===== 구조 검증 PASS {pass} / FAIL {fail} =====");
            if (fail > 0) Console.WriteLine("실패: " + string.Join(", ", failures));

            int cov_fail = TriggerCoverage.Run();
            int atk_fail = AttackTests.Run();
            Showcase.Run();

            Console.WriteLine($"\n########## 총계: FAIL {fail + cov_fail + atk_fail} ##########");
            Environment.ExitCode = (fail + cov_fail + atk_fail) > 0 ? 1 : 0;
        }

        // ==================== A. 이벤트 묶음 / 형제 순서 ====================

        // 한 이벤트에 두 카드가 반응, 첫 카드가 적을 죽임 → 묶음 중간에 사망 처리가 없어야 한다
        static void A01()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            var u2 = s.AddCard(0, "U2", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.RaiseEvent((self, Ab("갑(광역1)", (x, c, r) => x.DamageAllEnemies(0, 1))), (u2, Ab("을")));
            s.rq.ResolveAll();
            Check("A01 한 이벤트 2카드 — 묶음 중간에 사망 없음",
                  "갑(광역1) → 을 → [wave:적] → 적죽메", s.Log());
        }

        // 연속된 두 이벤트 (OnPlay → OnPlayOther) 순서 보존
        static void A02()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            var o1 = s.AddCard(0, "O1", 99);
            var o2 = s.AddCard(0, "O2", 99);
            s.TriggerCardAbilityType(self, Ab("OnPlay(전투의함성)"));
            s.RaiseEvent((o1, Ab("OnPlayOther-갑")), (o2, Ab("OnPlayOther-을")));
            s.rq.ResolveAll();
            Check("A02 연속 이벤트 2개 순서 보존",
                  "OnPlay(전투의함성) → OnPlayOther-갑 → OnPlayOther-을", s.Log());
        }

        // 연속된 세 이벤트 순서 보존
        static void A03()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            s.TriggerCardAbilityType(self, Ab("E1"));
            s.TriggerCardAbilityType(self, Ab("E2"));
            s.TriggerCardAbilityType(self, Ab("E3"));
            s.rq.ResolveAll();
            Check("A03 연속 이벤트 3개 순서 보존", "E1 → E2 → E3", s.Log());
        }

        // 연속 이벤트 사이에는 사망 처리가 돈다 (Rule 6)
        static void A04()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.TriggerCardAbilityType(self, Ab("E1(광역1)", (x, c, r) => x.DamageAllEnemies(0, 1)));
            s.TriggerCardAbilityType(self, Ab("E2"));
            s.rq.ResolveAll();
            Check("A04 연속 이벤트 사이에 사망 처리",
                  "E1(광역1) → [wave:적] → 적죽메 → E2", s.Log());
        }

        // 턴 진행 콜백 안에서 뜬 두 이벤트도 순서 보존 (독약 → 턴시작 → 드로우)
        static void A05()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            var u2 = s.AddCard(0, "U2", 99);
            s.rq.AddCallback(() =>
            {
                s.TriggerCardAbilityType(self, Ab("독약피해"));
                s.RaiseEvent((self, Ab("턴시작-갑")), (u2, Ab("턴시작-을")));
                s.rq.AddCallback(() => s.log.Add("턴 드로우"));
            });
            s.rq.ResolveAll();
            Check("A05 콜백 안 연속 이벤트 순서 보존",
                  "독약피해 → 턴시작-갑 → 턴시작-을 → 턴 드로우", s.Log());
        }

        // 한 카드가 같은 트리거 어빌리티 2개 → 한 묶음
        static void A06()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.TriggerCardAbilityType(self, Ab("능력1(광역1)", (x, c, r) => x.DamageAllEnemies(0, 1)), Ab("능력2"));
            s.rq.ResolveAll();
            Check("A06 한 카드 어빌리티 2개는 한 묶음",
                  "능력1(광역1) → 능력2 → [wave:적] → 적죽메", s.Log());
        }

        // 아무도 반응하지 않은 빈 이벤트는 건너뛴다 (죽음 경계를 놓치지 않는다)
        static void A07()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.TriggerCardAbilityType(self, Ab("E1(광역1)", (x, c, r) => x.DamageAllEnemies(0, 1)));
            s.RaiseEvent();                      // 반응 카드 0장 = 빈 Phase
            s.TriggerCardAbilityType(self, Ab("E3"));
            s.rq.ResolveAll();
            Check("A07 빈 이벤트를 건너뛰고 사망 처리 정상",
                  "E1(광역1) → [wave:적] → 적죽메 → E3", s.Log());
        }

        // ==================== B. 중첩 / depth-first ====================

        // 처리 중 유발된 이벤트가 대기 중인 형제보다 먼저
        static void B08()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            var u2 = s.AddCard(0, "U2", 99);
            var nested = Ab("중첩이벤트");
            s.RaiseEvent((self, Ab("갑", (x, c, r) => x.TriggerCardAbilityType(c, nested))), (u2, Ab("을")));
            s.rq.ResolveAll();
            Check("B08 중첩이 대기 형제보다 먼저", "갑 → 중첩이벤트 → 을", s.Log());
        }

        // 한 효과가 이벤트 2개를 띄우면 생성 순 (OnHeal → OnHealOther)
        static void B09()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            var u2 = s.AddCard(0, "U2", 99);
            var onHeal = Ab("OnHeal");
            var onHealOther = Ab("OnHealOther");
            s.TriggerCardAbilityType(self, Ab("회복효과", (x, c, r) =>
            {
                x.TriggerCardAbilityType(c, onHeal);
                x.RaiseEvent((u2, onHealOther));
            }));
            s.rq.ResolveAll();
            Check("B09 한 효과가 띄운 이벤트 2개는 생성 순",
                  "회복효과 → OnHeal → OnHealOther", s.Log());
        }

        // 3단 중첩
        static void B10()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            var L3 = Ab("3단");
            var L2 = Ab("2단", (x, c, r) => x.TriggerCardAbilityType(c, L3));
            var L1 = Ab("1단", (x, c, r) => x.TriggerCardAbilityType(c, L2));
            s.RaiseEvent((self, L1), (self, Ab("대기")));
            s.rq.ResolveAll();
            Check("B10 3단 중첩", "1단 → 2단 → 3단 → 대기", s.Log());
        }

        // chain이 유발 트리거보다 먼저
        static void B11()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            var trig = Ab("유발트리거");
            var chain = Ab("체인");
            s.RaiseEvent((self, Ab("갑", (x, c, r) =>
            {
                x.TriggerCardAbilityType(c, trig);        // 유발 이벤트
                x.TriggerCardAbility(chain, c, true);     // chain
            })), (self, Ab("을")));
            s.rq.ResolveAll();
            Check("B11 chain이 유발 이벤트보다 먼저", "갑 → 체인 → 유발트리거 → 을", s.Log());
        }

        // chain 안에서 또 이벤트가 뜨는 혼합 케이스
        static void B12()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            var deep = Ab("체인의중첩");
            var chain = Ab("체인", (x, c, r) => x.TriggerCardAbilityType(c, deep));
            var trig = Ab("유발트리거");
            s.RaiseEvent((self, Ab("갑", (x, c, r) =>
            {
                x.TriggerCardAbilityType(c, trig);
                x.TriggerCardAbility(chain, c, true);
            })), (self, Ab("을")));
            s.rq.ResolveAll();
            Check("B12 chain → chain의 중첩 → 유발 이벤트 → 대기 형제",
                  "갑 → 체인 → 체인의중첩 → 유발트리거 → 을", s.Log());
        }

        // 중첩 연쇄 도중에는 사망 처리가 돌지 않는다 (Rule 3)
        static void B13()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            var nested = Ab("중첩이벤트");
            s.TriggerCardAbilityType(self, Ab("갑(광역1)", (x, c, r) =>
            {
                x.DamageAllEnemies(0, 1);
                x.TriggerCardAbilityType(c, nested);
            }));
            s.rq.ResolveAll();
            Check("B13 중첩 도중 사망 처리 없음",
                  "갑(광역1) → 중첩이벤트 → [wave:적] → 적죽메", s.Log());
        }

        // ==================== C. 죽음 처리 ====================

        static void C14()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            var seen = Ab("");
            s.AddCard(1, "X", 1, Ab("X죽메"), seen);
            s.AddCard(1, "Y", 1, Ab("Y죽메"), seen);
            s.AddCard(1, "생존", 9, null, seen);
            s.TriggerCardAbilityType(self, Ab("광역1", (x, c, r) => x.DamageAllEnemies(0, 1)));
            s.rq.ResolveAll();
            Check("C14 동시 죽음 — 서로 목격 안 함",
                  "광역1 → [wave:X+Y] → X죽메 → 생존:목격(X) → Y죽메 → 생존:목격(Y)", s.Log());
        }

        static void C15()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            s.AddCard(1, "먼저낸적", 1, Ab("먼저낸적죽메"));
            s.AddCard(1, "나중낸적", 1, Ab("나중낸적죽메"));
            s.TriggerCardAbilityType(self, Ab("광역1", (x, c, r) => x.DamageAllEnemies(0, 1)));
            s.rq.ResolveAll();
            Check("C15 동시 죽음은 play order 순 죽메",
                  "광역1 → [wave:먼저낸적+나중낸적] → 먼저낸적죽메 → 나중낸적죽메", s.Log());
        }

        static void C16()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            s.AddCard(1, "X", 1, Ab("X죽메(Y처치)", (x, c, r) => { var y = x.Find("Y"); if (y != null) y.hp = 0; }));
            s.AddCard(1, "Y", 5, Ab("Y죽메"));
            s.TriggerCardAbilityType(self, Ab("처치", (x, c, r) => x.Find("X").hp = 0));
            s.rq.ResolveAll();
            Check("C16 죽메 연쇄 2단",
                  "처치 → [wave:X] → X죽메(Y처치) → [wave:Y] → Y죽메", s.Log());
        }

        // 죽음 트리거 Phase는 대기 중인 다른 최상위 Phase보다 먼저 (BeginImmediatePhase)
        static void C17()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.TriggerCardAbilityType(self, Ab("E1(광역1)", (x, c, r) => x.DamageAllEnemies(0, 1)));
            s.TriggerCardAbilityType(self, Ab("E2(대기중)"));
            s.rq.ResolveAll();
            Check("C17 죽메가 대기 중인 최상위 Phase보다 먼저",
                  "E1(광역1) → [wave:적] → 적죽메 → E2(대기중)", s.Log());
        }

        // 빈사 카드도 같은 묶음의 자기 트리거에 반응한다
        static void C18()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            var e = s.AddCard(1, "적", 1, Ab("적죽메"));
            s.RaiseEvent((self, Ab("갑(광역1)", (x, c, r) => x.DamageAllEnemies(0, 1))), (e, Ab("적의트리거")));
            s.rq.ResolveAll();
            Check("C18 빈사 카드도 같은 묶음에서 발동",
                  "갑(광역1) → 적의트리거 → [wave:적] → 적죽메", s.Log());
        }

        static void C19()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            var u2 = s.AddCard(0, "U2", 99);
            s.AddCard(1, "적", 1, Ab("적죽메"));
            s.RaiseEvent((self, Ab("갑(광역1)", (x, c, r) => x.DamageAllEnemies(0, 1))),
                         (u2, Ab("을(힐3)", (x, c, r) => x.Heal(x.Find("적"), 3))));
            s.rq.ResolveAll();
            Check("C19 사망 처리 전 힐로 구제", "갑(광역1) → 을(힐3)", s.Log());
        }

        static void C20()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            var u2 = s.AddCard(0, "U2", 99);
            s.AddCard(1, "적", 5, Ab("적죽메"));
            s.RaiseEvent((self, Ab("갑(파괴)", (x, c, r) => x.MarkDying(x.Find("적")))),
                         (u2, Ab("을(힐3)", (x, c, r) => x.Heal(x.Find("적"), 3))));
            s.rq.ResolveAll();
            Check("C20 파괴는 힐로 구제 불가",
                  "갑(파괴) → 을(힐3) → [wave:적] → 적죽메", s.Log());
        }

        static void C21()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            var inv = s.AddCard(1, "무적적", 1, Ab("무적적죽메"));
            inv.invincible = true;
            s.AddCard(1, "일반적", 1, Ab("일반적죽메"));
            s.TriggerCardAbilityType(self, Ab("광역1", (x, c, r) => x.DamageAllEnemies(0, 1)));
            s.rq.ResolveAll();
            Check("C21 무적 카드는 웨이브에 포함되지 않음",
                  "광역1 → [wave:일반적] → 일반적죽메", s.Log());
        }

        // player_ability도 일반 효과와 동일하게 사망 웨이브를 탄다
        static void C22()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            s.AddPlayerAbilityCard(1, "플레이어능력", 1, Ab("플레이어능력죽메"));
            s.TriggerCardAbilityType(self, Ab("처치", (x, c, r) => x.Find("플레이어능력").hp = 0));
            s.rq.ResolveAll();
            Check("C22 player_ability도 사망 웨이브를 탄다",
                  "처치 → [wave:플레이어능력] → 플레이어능력죽메", s.Log());
        }

        // ==================== D. 반복 (모독 / 요그사론) ====================

        // 모독: 회차 사이에 사망 처리가 완결된다
        static void D23()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            s.AddCard(1, "적1", 1); s.AddCard(1, "적2", 2); s.AddCard(1, "적3", 3);
            s.TriggerCardAbilityType(self, Ab("모독", (x, c, r) => x.DamageAllEnemies(0, 1), rep: 3));
            s.rq.ResolveAll();
            Check("D23 모독 페이싱 (회차 사이 사망 처리)",
                  "모독1 → [wave:적1] → 모독2 → [wave:적2] → 모독3 → [wave:적3]", s.Log());
        }

        // 요그사론: 2회차에 자멸하면 3회차는 발동하지 않는다
        static void D24()
        {
            var s = new Sim();
            var yogg = s.AddCard(0, "요그", 5);
            s.TriggerCardAbilityType(yogg, Ab("시전", (x, c, r) => { if (r == 1) c.hp = 0; }, rep: 3));
            s.rq.ResolveAll();
            Check("D24 요그 — 시전자 자멸 시 남은 회차 중단",
                  "시전1 → 시전2 → [wave:요그]", s.Log());
        }

        // 요그사론: 시전자가 살아 있으면 끝까지 간다
        static void D25()
        {
            var s = new Sim();
            var yogg = s.AddCard(0, "요그", 5);
            s.TriggerCardAbilityType(yogg, Ab("시전", null, rep: 3));
            s.rq.ResolveAll();
            Check("D25 요그 — 생존 시 전 회차 시전", "시전1 → 시전2 → 시전3", s.Log());
        }

        static void D26()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            int[] n = { 0 };
            s.TriggerCardAbilityType(self, Ab("반복", (x, c, r) => n[0]++, rep: 9, cond: x => n[0] < 2));
            s.rq.ResolveAll();
            Check("D26 반복 조건 실패 시 체인 종료", "반복1 → 반복2", s.Log());
        }

        // 반복 회차는 대기 중인 **다른 최상위 Phase**보다 먼저
        static void D27()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            s.TriggerCardAbilityType(self, Ab("A", null, rep: 2));
            s.TriggerCardAbilityType(self, Ab("다음이벤트"));
            s.rq.ResolveAll();
            Check("D27 반복 회차가 대기 중인 다른 이벤트보다 먼저",
                  "A1 → A2 → 다음이벤트", s.Log());
        }

        // 반복 회차는 **같은 묶음 안 형제**보다는 뒤 (Rule 3에 의해 불가피)
        static void D28()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            var u2 = s.AddCard(0, "U2", 99);
            s.RaiseEvent((self, Ab("A", null, rep: 2)), (u2, Ab("B")));
            s.rq.ResolveAll();
            Check("D28 반복 회차는 같은 묶음 형제보다 뒤", "A1 → B → A2", s.Log());
        }

        // 중첩 repeat — 깊은 쪽 회차가 먼저
        static void D29()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            var D = Ab("D", null, rep: 2);
            s.TriggerCardAbilityType(self, Ab("A", (x, c, r) => { if (r == 0) x.TriggerCardAbilityType(c, D); }, rep: 2));
            s.rq.ResolveAll();
            Check("D29 중첩 repeat — 깊은 쪽 회차가 먼저",
                  "A1 → D1 → D2 → A2", s.Log());
        }

        // ==================== E. selector ====================

        // selector로 중단됐다가 재개해도 depth-first가 유지된다
        static void E30()
        {
            var s = new Sim();
            var self = s.AddCard(0, "SELF", 99);
            var u2 = s.AddCard(0, "U2", 99);
            var afterPick = Ab("선택후유발");
            s.RaiseEvent((self, Ab("갑(대상선택)", (x, c, r) => x.game.selector = SelectorType.SelectTarget)),
                         (u2, Ab("을(대기)")));
            s.rq.ResolveAll();                       // selector로 중단

            // 플레이어가 대상 선택 → GameLogic.SelectCard 미러
            s.game.selector = SelectorType.None;
            s.rq.BeginPhase();                       // 중단된 스코프를 복원
            s.log.Add("선택완료·효과적용");
            s.TriggerCardAbilityType(self, afterPick);
            s.rq.EndPhase();
            s.rq.ResolveAll();

            Check("E30 selector 재개 후에도 depth-first 유지",
                  "갑(대상선택) → 선택완료·효과적용 → 선택후유발 → 을(대기)", s.Log());
        }
    }
}
