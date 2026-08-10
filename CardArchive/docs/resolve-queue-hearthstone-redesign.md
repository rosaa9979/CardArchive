# Resolve 구조 개편안 — 하스스톤식 (페이즈 스택 + 죽음 페이즈)

> 📄 **현재 동작만 빠르게 알고 싶다면** [`effect-resolution-flow.md`](effect-resolution-flow.md)를 먼저 볼 것.
> 적재/Resolve 플로우차트와 대표 시나리오가 거기 정리돼 있다.
> 이 문서는 **왜 그렇게 설계했는지**의 근거와 개편 이력이다.

작성일: 2026-07-12 (Phase 2 구현: 2026-07-13)
상태: Phase 1·2 구현 완료 (순서 시나리오 검증 통과, 인게임 수동 확인 남음)

## 배경과 목표

기존 구조는 단일 FIFO `ability_queue`라서, 어떤 어빌리티(A)가 resolve되는 도중 유발된
효과(D)와 chain 어빌리티가 모두 큐 꼬리에 붙는다. 그 결과 같은 배치에 대기 중이던
다른 어빌리티(B, C)가 A의 연계보다 먼저 발동한다 (breadth-first).

```
현재:  A → B → C → D(A가 유발) → A의 chain
```

하스스톤은 depth-first다. 트리거 하나가 resolve되며 새 이벤트를 일으키면, 중첩
페이즈가 열려 그 결과까지 완전히 처리한 뒤에야 대기 중이던 다음 트리거가 발동한다.
(참고: 커뮤니티 Advanced Rulebook — https://hearthstone.wiki.gg/wiki/Advanced_rulebook)

본 개편의 확정 요구사항:

1. **Depth-first**: resolve 중 유발된 효과는 대기 중인 다른 효과보다 먼저 발동.
   단, 해당 어빌리티의 **chain이 유발 효과보다 먼저**.
2. **Repeat**: 반복 회차는 trigger condition을 재평가하지 않고 repeat condition만으로
   판정. 반복분은 회차별로 그룹핑되어 대기 배치보다 앞에 온다.
   → **Phase 3에서 축소됨**: "대기 중인 **다른 이벤트**보다 앞"이지만 "**같은 이벤트 묶음
   안의 형제**보다는 뒤"다. 하스스톤 Rule 3(묶음이 다 비어야 죽음 처리)과 모독 페이싱
   (회차 조건은 사망 처리 후 보드를 봐야 함)이 동시에 성립하려면 물리적으로 그럴 수밖에
   없다. 상세는 아래 Phase 3 참조.
3. **적용 범위**: ability뿐 아니라 secret/attack/callback 처리 중 유발된 어빌리티도
   전부 depth-first.
4. **Order of play**: 동시 트리거 배치의 발동 순서는 보드 슬롯 순회가 아니라
   **필드에 나온 순서**(먼저 낸 카드가 먼저 발동).
5. **죽음 페이즈** (Phase 2): 죽음을 즉시 처리하지 않고 페이즈가 끝날 때 일괄 처리.
6. 무한 연쇄 가드는 넣지 않는다 (기획으로 관리). — 단 페이즈 스택 구조상 추후
   "스택 깊이 상한" 한 줄로 추가 가능.

목표 순서 (A, B, C 동시 트리거 / A가 chain + D1, D2 유발 / D1이 E 유발 / A 2회 repeat):

```
Phase 1·2 기준:  A1 → A1의 chain → D1 → E → D2 → A2 → (A2의 연계...) → B → C
Phase 3 기준:    A1 → A1의 chain → D1 → E → D2 → B → C → A2
                                                    └─ 반복분은 같은 묶음 형제(B,C) 뒤
```

## 진행 단계

| 단계 | 내용 | 상태 |
|---|---|---|
| Phase 1 | 페이즈 스택, is_chain, repeat 규칙, selector 스코프, play order | 구현 완료 |
| Phase 2 | 죽음 페이즈 (Death Creation Step) | 구현 완료 (2026-07-13) |
| Phase 3 | 이벤트 단위 Phase, 형제 FIFO, 요그 규칙, player_ability 편입 | 구현 완료 (2026-08-02) |

Phase 1 검증 (2026-07-12): 실제 `ResolveQueue.cs`를 스텁과 함께 단독 컴파일해 순서
시나리오 4종 통과 — ① 동시 배치 depth-first + chain 우선 + repeat 회차 그룹
(A1→Ach→D1→E→D2→A2→…→B→C), ② attack 처리 중 유발 어빌리티 depth-first,
③ selector 중단/재개 시 chain→유발→중단 전 대기분 순서 유지, ④ secret/callback
depth-first. 인게임 수동 확인(기존 카드 풀 대표 연계)은 남아 있음.

Phase 2 검증 (2026-07-13, 하스스톤식 통합 구조로 개정 후 2026-07-14 재검증):
같은 방식(실제 `ResolveQueue.cs` + 스텁 단독 컴파일, `ProcessDeathStep` 로직을
미러링한 하네스)으로 시나리오 8종 통과 —
① 동시 죽음(전원 제거 후 트리거, 같은 웨이브끼리 OnDeathOther 미발동, OnKill →
OnDeath 순, play_order 순), ② 죽메 연쇄(웨이브 반복), ③ 아우라 상실 연쇄(트리거
없는 웨이브에서도 루프 유지), ④ attack 마이크로스텝 사이 죽음 스텝 선행,
⑤ Phase 1 depth-first/chain 회귀, ⑥ 모독 타이밍(회차 → 죽음 페이즈에서 죽메 토큰
소환 → 토큰이 다음 회차에 참여 → 아무도 안 죽으면 종료), ⑦ 배치 요소 사이 죽음
페이즈(B1이 죽인 카드의 죽음+죽메가 C1보다 먼저), ⑧ 지연 반복이 대기 배치보다 앞
(A1 → A2 → B).

> ⚠️ **⑦·⑧은 Phase 3에서 기대값이 뒤집혔다.** 당시 하네스는 배치를 base 큐에 낱개로
> 넣어 검증했는데, 그 전제 자체가 하스스톤과 어긋난 것이었다 (⑦은 같은 이벤트 묶음이면
> 같은 웨이브여야 하고, ⑧은 같은 묶음 형제보다 뒤여야 함). Phase 3 검증 30종이 이를
> 대체한다.

---

## Phase 1 — 페이즈 스택

> ⚠️ **이 절의 자료구조는 Phase 3(2026-08-02)에서 교체되었다.** `phase_stack`(평평한 LIFO)
> → `phase_queue`(최상위 FIFO) + `AbilityPhase.children`(자식 FIFO). 현재 코드를 읽을 때는
> 아래 「Phase 3」 절을 기준으로 볼 것. 이 절은 **왜 그렇게 갔었는지**의 기록으로 남긴다.
> 요구사항 1~4(depth-first, chain 우선, play order)는 Phase 3에서도 그대로 유효하다.

### 자료구조 (`Tools/ResolveQueue.cs`) — Phase 1·2 시점

```csharp
class AbilityPhase {
    Queue<AbilityQueueElement> chains;    // 먼저 소진
    Queue<AbilityQueueElement> triggers;  // 그 다음
    bool active;                          // 아직 채워지는 중(소유 요소가 resolve 중)
}
List<AbilityPhase> phase_stack;   // 마지막 원소가 top. top부터 소진
Stack<AbilityPhase> insert_stack; // 현재 삽입 대상 (중첩 대비)
```

- `BeginPhase()`: 새 페이즈를 phase_stack top과 insert_stack에 push.
- `EndPhase()`: insert_stack pop, `active = false`. 비어 있으면 스택에서 제거 후 풀 반납.
- `AddAbility(..., bool is_chain)`: insert_stack이 비어 있으면(평상시) base
  `ability_queue` 꼬리로 — 동시 트리거 배치의 등록 방식은 기존과 동일.
  삽입 대상 페이즈가 있으면 chain 여부에 따라 chains/triggers 큐로.
- `Resolve()`의 ability 단계: phase_stack을 top부터 스캔해 첫 비어있지 않은 페이즈의
  chains → triggers 순으로 dequeue. 전부 비면 base ability_queue. dequeue 후 top의
  비활성·빈 페이즈는 pop.
- **모든 요소 타입**(ability/secret/attack/callback)의 콜백 실행을
  `BeginPhase()`/`EndPhase()`로 감싼다 → 실행 중 유발된 어빌리티가 자동으로 그 요소의
  페이즈에 담겨 depth-first가 성립.
- `CanResolve()`/`GetNextQueueDelay()`/`Clear()`는 페이즈 스택 포함해서 판단.

### 순서 성립 원리

- 요소 X가 resolve되면 X의 페이즈가 top에 생기고, X가 유발한 것들이 거기 담긴다.
  top부터 소진하므로 X의 연계가 그 아래(기존 대기분)보다 먼저 = depth-first.
- chain은 `AfterAbilityResolved`에서 시간상 나중에 등록되지만 chains 큐가 triggers
  큐보다 먼저 소진되므로 요구사항(chain 우선)이 성립.
- repeat 다음 회차는 `AfterAbilityResolved`에서 일반 트리거로 등록 → 현재 회차
  페이즈의 triggers 꼬리 = "회차별 그룹"이 자동 성립.

### GameLogic 변경 (`GameLogic/GameLogic.cs`)

1. `TriggerCardAbility` / `RepeatTriggerCardAbility` (Card triggerer 버전)에
   `bool is_chain = false` 파라미터 추가, `AddAbility`까지 관통.
   `AfterAbilityResolved`의 chain 등록 루프에서만 `true`.
2. **트리거 조건 이중 검사** (2026-07-14, 하스스톤 동일): `TriggerCardAbility`가
   **등록 시점**에 침묵 + trigger condition을 검사해 통과한 것만 큐에 넣고,
   `ResolveCardAbility`가 **발동 시점**에 최신 상태로 재검사 (`current_repeat == 0`일
   때만). 둘 다 통과해야 발동. 등록 시 거짓 → 발동 시 참이 된 경우(예: 같은 배치의
   선행 어빌리티가 갱신한 클럽 호스트 카운터를 보는 케이스)는 **발동하지 않음** —
   이전의 "발동 시만" 정책에서 의도적으로 허용했던 케이스이므로 관련 카드 확인 필요.
   Player triggerer 버전은 등록 시 검사가 Player 조건을 볼 수 있는 유일한 지점
   (큐에는 Player가 보존되지 않음). 반복 회차는 양쪽 검사 모두 건너뛰고
   repeat condition만으로 판정 (죽음 페이즈 안정 시점, `ProcessPendingRepeats`).
   침묵(`CanDoAbilities`)·OnDeathOther 보드 이탈 체크는 발동 시점에 전 회차 유지.
3. selector 완료 4경로(`SelectCard`/`SelectPlayer`/`SelectSlot`/`SelectChoice`)는
   `Resolve()` 밖에서 효과를 적용하므로 효과 적용 ~ `AfterAbilityResolved` 구간을
   `BeginPhase()`/`EndPhase()`로 직접 감싼다.
3. selector 완료 4경로(`SelectCard`/`SelectPlayer`/`SelectSlot`/`SelectChoice`)는
   `Resolve()` 밖에서 효과를 적용하므로 효과 적용 ~ `AfterAbilityResolved` 구간을
   `BeginPhase()`/`EndPhase()`로 직접 감싼다.

### Play order

- `Game.play_order_counter`(int) + `Card.play_order`(int) 추가.
- 카드가 **필드에 진입할 때**(보드/클럽/히어로/장착) 카운터를 증가시켜 부여.
  손·덱에 있는 동안은 의미 없음. 필드를 떠났다 다시 들어오면 새 값(하스스톤 동일).
- `TriggerOtherCardsAbilityType` / `TriggerPlayerCardsAbilityType`: 후보 카드를 모아
  `play_order` 오름차순(먼저 낸 순)으로 정렬 후 등록.
- 자기 트리거(OnPlay 등)가 Other보다 먼저인 것은 유지 — 하스스톤에서도 전투의 함성은
  트리거 큐가 아니라 카드 텍스트 단계라 항상 먼저다.

---

## Phase 2 — 죽음 페이즈 (구현 완료 2026-07-13, 하스스톤식 통합 구조로 개정 2026-07-14)

### 현재 구조의 문제

`DamageCard_Event`에서 hp≤0이면 즉시 `KillCard` → `DiscardCard` → 그 자리에서
OnDeath/OnDeathOther/시크릿 트리거. 죽음이 데미지 적용 도중에 끼어들어 동시 처리
개념이 없다 (`cards_to_clear` 지연 정리 장치가 일부만 보완).

### 빈사(mortally wounded) 마킹

- 데미지로 hp≤0이 되어도 즉시 죽이지 않는다. 카드는 보드에 남아 계속 트리거에 반응.
- Deathtouch, 파괴 이펙트(`EffectDestroy` 계열), `KillCard` 호출부 전부
  `MarkDying(attacker, target)`으로 교체. `Card.dying` 플래그 + 킬 귀속
  (kill_count/OnKill용) 기록.
- `DiscardCard`는 손/덱에서 버리기 등 비전투 즉시 제거 전용으로 남긴다.

### Death Phase (Death Creation Step)

**최상위(outermost) Phase 하나(와 그것이 유발한 depth-first 서브트리 전체)가 끝날 때마다**
실행 — 다음 대기 요소를 꺼내기 **전에** 돈다 (하스스톤 Rule 3/4a와 동일).
중첩 연쇄 도중에는 돌지 않는다.

> 📌 **Phase 3 정정**: 원문은 이 경계를 "페이즈 스택이 비는 순간"으로 적었는데, 그건
> 자료구조에 기댄 표현이었고 **"무엇이 최상위 Phase인가"가 호출 경로에 따라 달라지는**
> 문제가 있었다 (같은 턴시작 배치인데 base 큐로 가느냐 페이즈로 가느냐에 따라 결과가
> 갈림). Phase 3에서는 `current_top`(지금 처리 중인 최상위 Phase)이 소진되는 순간으로
> 명시적으로 정의한다. 규칙 자체는 바뀌지 않았다.

```
1. UpdateOngoing()                          // 오라 갱신
2. 죽을 대상 수집: dying || GetHP()<=0, play_order 순 정렬
3. 없으면 지연 반복(pending repeat) 평가 → 없으면 CheckForWinner() 후 종료
4. 전원 동시에 보드 제거 + 무덤 이동 (트리거 없이), 킬 귀속 확정(OnKill 등록)
5. 죽은 카드의 OnDeath + 생존 카드의 OnDeathOther + 시크릿을 죽은 순서대로
   새 페이즈로 등록
6. resolve 계속 → 다음 경계에서 재실행 (죽메 연쇄) → 안정될 때까지 반복
```

- **동시 죽음 규칙**: 4에서 전원을 먼저 제거하므로, 같은 웨이브에 죽은 카드끼리는
  서로의 OnDeathOther를 받지 않는다 (하스스톤 Cult Master 규칙). 웨이브 단위는
  **"같은 최상위 Phase가 만든 죽음"**이다. 따라서 **한 이벤트 묶음 안에서 죽은 카드끼리는
  같은 웨이브**(서로 목격 못 함)이고, **서로 다른 이벤트가 죽인 카드끼리는 별개 웨이브**가
  되어 OnDeathOther를 주고받는다.
- 최상위 Phase 경계에서 죽음이 처리되므로, 어떤 Phase가 죽인 카드의 죽메는 **대기 중인
  다음 Phase보다 먼저** 발동한다 (`BeginImmediatePhase`). 단 **같은 묶음 안의 형제**보다는
  뒤다 — 묶음이 다 비어야 경계가 오기 때문.

> 📌 **Phase 3 정정**: 원문의 "배치 요소"는 낱개 트리거를 뜻했고, 그 결과 같은 이벤트에
> 반응한 카드들이 서로 다른 웨이브로 갈라졌다. 하스스톤은 그 반대다 —
> "All Knife Juggler effects are handled before any of their deaths are detected."
> Phase 3에서 이벤트 묶음이 도입되면서 웨이브 단위가 "이벤트" 기준으로 정정되었다.
- `cards_to_clear` 장치는 죽음 스텝에 흡수.
- 승패 판정(`CheckForWinner`)은 죽음 스텝의 안정 시점에 통합.

### 구현 상세 (실제 반영 내용)

- `ResolveQueue.SetDeathStep(death_step, has_deaths)`: GameLogic이
  `ProcessDeathStep` / `HasPendingDeaths`를 훅으로 주입. `Resolve()` **맨 앞**에서
  "최상위 Phase 소진(`current_top == null`) && has_deaths"일 때 죽음 스텝을 먼저 돌리고,
  아니면 Phase(chain→자식→trigger) → secret → attack → callback 순으로 진행.
  *(Phase 3 이전 표현: "페이즈 스택 비어 있음", "ability(페이즈 스택+base)")*
  has_deaths 게이트 덕에 죽음/지연 반복이 없는 경계는 비용 없이 통과.
  `CanResolve()`/`GetNextQueueDelay()`/비-skip `hasMore`에도 `has_deaths` 반영
  (마지막 요소가 남긴 죽음·지연 반복도 루프가 이어서 처리).
- **빈사 판정** (`GameLogic.IsDying`): `dying || GetHP() <= 0`, Invincibility 제외.
  - 데미지 사망은 dying 플래그를 세우지 않고 **스텝 시점에 GetHP()<=0 재평가** →
    스텝 전에 힐이 들어오면 생존 (하스스톤 규칙).
  - 파괴류(`EffectDestroy`→`KillCard`→`MarkDying`, Deathtouch)는 `dying` 마킹 →
    힐로 구제 불가.
- **킬 귀속**: 치명 데미지 시점에 `Card.death_source_uid`(첫 치명타 우선) +
  `death_source_counter`(반격 킬 = OnKill 미발동, kill_count는 증가) 기록.
  힐로 0 초과 회복 시 귀속 해제. 스텝에서 kill_count/OnKill 확정.
- `KillCard`는 API 유지: 보드 카드 → `MarkDying`, 장비 카드 → 기존 즉시 제거
  (장비는 죽음 페이즈 대상 아님). `DiscardCard`는 즉시 제거 경로로 유지
  (`RemoveFromPlay`로 제거 로직 공용화).
- `ResolveDeath`(attack 마이크로스텝)의 즉시 KillCard 제거,
  `UpdateOngoing`의 보드 0hp 즉시 정리 제거 (둘 다 죽음 스텝으로 이관).
- 스텝 마지막에 `UpdateOngoing()` 재실행: 죽은 카드의 아우라를 잃어 0 이하가 된
  카드를 `HasPendingDeaths`가 즉시 감지 → 트리거 없는 웨이브에서도 안정화 루프 유지.
- 죽음 트리거는 스텝이 연 **하나의 페이즈**에 OnKill(전원) → 죽은 순서대로
  OnDeath + OnDeathOther + 시크릿 순으로 등록 → 대기 중인 attack/callback보다 먼저
  resolve (depth-first 유지).

### 반복(repeat) 페이싱 — 하스스톤/모독(Defile) 방식으로 통일

모든 repeat는 다음 회차를 즉시 등록하지 않고 지연시킨다 (옵트인 플래그 없음, 단일
구조):

- `AfterAbilityResolved`는 반복 가능성이 있을 때(`condition_repeat != null ||
  next < max_repeat`)만 `pending_repeats`에 적재. repeat condition 평가는 하지 않음.
- 죽음 스텝의 **안정 시점**(이번 회차의 죽음 + 죽메 연쇄 완결)에
  `ProcessPendingRepeats`가 repeat condition을 평가하고, 다음 회차를
  `BeginImmediatePhase()`로 **큐 맨 앞의 새 최상위 Phase**에 등록 → 대기 중인 **다른
  이벤트**보다 먼저 온다: `A1 → (죽음 페이즈) → A2 → … → 다음 이벤트`.

> 📌 **Phase 3 정정**: 원문은 "반복분은 대기 배치보다 앞"이 그대로 유지된다고 적었지만,
> **같은 이벤트 묶음 안의 형제**에는 성립하지 않는다. 묶음이 다 비어야 죽음 페이즈가
> 돌고(Rule 3), 회차 조건은 죽음 처리 후 보드를 봐야 하기 때문(모독). 실제 순서는
> `A1 → B → (죽음 페이즈) → A2 → …`이고, **A2부터는 모독 페이싱이 그대로 유지된다.**
> 규칙은 "대기 중인 **다른 이벤트**보다 앞"으로 축소되었다 (검증 D27/D28).
- 여러 pending이 공존하면(한 서브트리 안의 중첩 요소가 각자 반복) **역순(나중 등록
  = 더 깊은 요소) 우선**으로 등록 — depth-first 정합.
- 조건 실패 = 그 반복 체인 종료. `HasPendingDeaths`가 pending도 감지하므로 큐가
  완전히 비어도 평가가 유실되지 않는다.
- repeat condition은 죽음 처리가 끝난 보드를 보고 평가되므로 "이번 회차로
  죽었는가" 류 조건(모독)을 그대로 쓸 수 있고, 죽메 토큰이 다음 회차에 참여한다.
  대기 배치(B, C)는 아직 실행 전이므로 남의 킬이 판정에 섞이지 않는다.
- trigger condition을 회차마다 재평가하지 않는 원칙은 변화 없음 (평가 시점만 이동).
- **요그사론 규칙** (Phase 3 신설): 시전자가 회차 도중 필드를 떠나면 남은 회차는 발동하지
  않는다 (하스스톤 Rule 6). `caster.CardData.IsBoardCard() && !IsOnBoard(caster)`로 거른다.

### Phase 2 리스크 (구현 시 전수 확인 필요)

- 빈사 카드가 죽음 스텝까지 슬롯을 점유 → 그 슬롯 대상 소환/이동 효과 타이밍 변화.
- `last_destroyed` / `last_destroyed_slot` 참조 효과의 시점 변화.
- "죽자마자 부활" 류 효과, `Shishido_Izumi` 등 하드코딩 특수 케이스.
- 클라 연출: 사망 애니메이션이 요소별 죽음 페이즈 경계마다 웨이브 단위로 발생
  (하스스톤과 동일한 연출이지만 기존 즉시-사망 타이밍과 달라짐). 빈사 상태 표시
  여부는 별도 결정.

---

## Phase 3 — 이벤트 단위 Phase / 형제 FIFO (구현 완료 2026-08-02)

### 배경: Phase 1·2에 남아 있던 두 구멍

Phase 1·2는 "죽음은 outermost Phase가 끝날 때만"(Rule 3)을 정확히 구현했지만, **무엇이
outermost Phase인가**를 자료구조에 맡겨두었다. 그래서 두 문제가 있었다.

**① 동시 트리거 배치가 묶이지 않았다.** `TriggerOtherCardsAbilityType` /
`TriggerPlayerCardsAbilityType`은 play order로 정렬만 하고 카드마다 낱개 요소로 등록했다.
루프 밖에서 호출되면(카드 플레이 경로) 각 카드가 **저마다 outermost 요소**가 되어 사이사이
사망 웨이브가 돌았다. 하스스톤은 정반대다:

> "**All Knife Juggler effects are handled before any of their deaths are detected and
> processed.**" — Knife Juggler 문서
>
> Advanced Rulebook 예시: "You play an Explosive Sheep and a Cult Master. ... first the
> Explosive Sheep's Deathrattle, **which mortally wounds the Cult Master**, then the Cult
> Master's on-Death trigger, **which draws a card**. Finally, a new Death Phase begins where
> the Cult Master is killed."

즉 한 이벤트에 반응하는 트리거들은 **개별로 순차 resolve되지만 죽음 처리는 묶여서** 뒤로
간다. 반대로 Sequence의 서로 다른 Phase 사이(준비→공격, 소환→전투의 함성→After Summon)에는
죽음 처리가 돈다 (Rule 6).

**② 반대로 턴 진행 경로는 과하게 묶여 있었다.** 모든 요소(ability/secret/attack/callback)를
`BeginPhase`로 감쌌기 때문에, `BeforeMainPahse` 콜백 하나 안에서 뜬 독약 피해와 턴시작 배치가
같은 Phase의 중첩 형제가 되어 **순서가 뒤집혔다**(평평한 phase_stack을 top부터 스캔했으므로).

### 자료구조: 두 축의 분리

Phase는 두 관계를 담당하는데 Phase 1·2는 이를 하나의 LIFO 스택으로 처리했다.

| 관계 | 올바른 규칙 | Phase 1·2 | Phase 3 |
|---|---|---|---|
| 부모 ↔ 자식 (연쇄) | 자식 먼저 (depth-first) | ✔ (LIFO로 우연히 맞음) | ✔ `children` |
| 형제 ↔ 형제 (같은 레벨) | **먼저 만든 것 먼저 (FIFO)** | ✘ 역전 | ✔ `phase_queue` / `children` |

```csharp
// Tools/ResolveQueue.cs
List<AbilityPhase> phase_queue;   // 최상위 Phase들 — FIFO (base ability_queue를 대체)
AbilityPhase current_top;         // 지금 처리 중인 최상위 Phase. 소진되는 순간이 죽음 경계

class AbilityPhase {
    Queue<AbilityQueueElement> chains;    // 가장 먼저
    List<AbilityPhase>         children;  // 그다음, 생성 순 FIFO
    Queue<AbilityQueueElement> triggers;  // 그다음
    Queue<AttackQueueElement>  attacks;   // 유발된 공격
    AbilityPhase parent;
    bool active;
}
```

**소진 우선순위** (`FindNextPhase` + `Resolve`):

```
① 현재 Phase의 chain                  ← 어빌리티의 후속 텍스트가 자기가 유발한 것보다 먼저
② 자식 Phase (생성 순 FIFO, 재귀)      ← depth-first
③ 현재 Phase의 trigger → 유발된 공격
④ (최상위 소진) 죽음 페이즈 → 지연 반복
⑤ Queue의 다음 최상위 Phase
⑥ secret → attack 마이크로스텝 → callback   (우선순위는 Phase 1·2와 동일)
```

두 가지가 미묘하다:
- **①이 ②보다 앞**이어야 한다. 아니면 `AfterAbilityResolved`가 등록한 chain을, 그 어빌리티가
  먼저 유발해둔 이벤트가 추월한다 (검증 B11/B12).
- 처리 중인 효과의 스코프는 부모 `children`의 **맨 앞**에 꽂는다(`NewChildPhase(owner, true)`).
  그 효과가 **이미 유발해둔** 형제 자식들보다 자기 연쇄가 먼저여야 하기 때문 (검증 B12).

### 무엇을 Phase로 감싸는가

원칙: **이벤트만 Phase를 연다. Sequence의 골격은 감싸지 않는다.**

| 대상 | 처리 | 이유 |
|---|---|---|
| `TriggerCardAbilityType` (2종) | `BeginPhase`/`EndPhase`로 감쌈 | 한 카드의 같은 트리거 어빌리티들 = 한 이벤트 |
| `TriggerOtherCardsAbilityType` / `TriggerPlayerCardsAbilityType` | 배치 전체를 감쌈 | 한 이벤트에 반응하는 카드들 = 한 묶음 |
| `OnGameStart` / `StartOfTurn` / `EndOfTurn` 플레이어 루프 | 호출 측에서 한 번 더 감쌈 | 한 이벤트가 호출 2번에 걸침 |
| 어빌리티 요소 | `Resolve`가 스코프를 엶 | 그 효과가 유발할 것들의 컨테이너 |
| **attack 마이크로스텝** | 스코프를 열고 **곧바로 `current_top`으로 잡음** | 이건 Sequence 골격이 아니라 **Phase 자체**다 (하스스톤의 'Preparation' / 'Attack' Phase). 아래 참조 |
| **secret / callback** | **감싸지 않음** | Sequence의 골격. 이들이 띄우는 이벤트가 각자 최상위 Phase가 되어 FIFO로 줄서고 사이에 죽음 처리가 돌아야 함 (Rule 6) |

#### 공격 마이크로스텝을 `current_top`으로 잡는 이유

> "Sending a Character to attack another character creates a Sequence of **two Phases** -
> 'Preparation' and 'Attack'." — Advanced Rulebook

공격 스텝은 Phase이므로, 그 안에서 뜬 트리거(`OnAfterDamage`, `OnAfterAttack`,
`OnAfterDefend`…)가 **죽음 처리보다 먼저** 발동해야 한다 (Rule 4a: Death Creation Step은
Phase가 끝난 뒤).

그런데 스코프를 그냥 최상위 Phase로 큐에 넣기만 하면 `Resolve()` ②의 죽음 게이트
(`current_top == null`)가 먼저 열려서, **공격에 죽은 카드가 트리거보다 앞서 제거된다.**
그래서 스코프를 만든 즉시 `current_top`에 대입해 게이트를 막는다.

부수적으로 `AddTriggeredAttack`의 판별 기준도 바뀌었다. 공격 스텝도 이제 스코프를 열기
때문에 `insert_stack`만 보면 **볼리의 다음 발이 현재 공격의 Phase 안으로 잘못 들어간다.**
그래서 `GameLogic.attack_triggered_by_effect` 플래그로 명시 판별하고,
`EffectAttack`은 전용 진입점 `AttackTargetFromEffect`를 쓴다.

`BeginImmediatePhase()`는 "현재 Sequence의 다음 Phase" — 대기 중인 다른 최상위 Phase보다 **앞**에
끼워넣는다. 죽음 트리거(`ProcessDeathStep`)와 반복 회차(`ProcessPendingRepeats`)가 쓴다.

### selector 재개 복원

selector 4경로(`SelectCard`/`SelectPlayer`/`SelectSlot`/`SelectChoice`)는 `Resolve()` 밖에서
효과를 적용하므로 `BeginPhase()`를 직접 부른다. Phase 1·2에서는 그 Phase가 stack top에 쌓여
**우연히** depth-first가 유지됐지만, 최상위가 FIFO가 된 Phase 3에서는 대기 중인 형제들 뒤로
밀린다. 그래서 `ResolveQueue`가 중단된 효과의 스코프를 `suspended_scope`로 살려두었다가
`BeginPhase()`가 **그 스코프를 그대로 다시 연다.** GameLogic 쪽 코드는 그대로 두었다.

### 반복(repeat) 규칙 축소 — 모독 / 요그사론

**"반복분은 대기 배치보다 앞"은 "같은 이벤트 묶음 안의 형제보다는 뒤"로 축소되었다.**
두 요구가 물리적으로 양립할 수 없기 때문이다:

- 모독 페이싱 = 회차 조건이 **사망 처리가 끝난 보드**를 봐야 함
- Rule 3 = 사망 처리는 **묶음이 다 비어야** 돎
- ⇒ 같은 묶음의 형제가 먼저 끝나야 한다

관측되는 순서: `A1 → B → [wave] → A2 → [wave] → A3 → [wave]` (검증 D28/D23).
B가 첫 웨이브 앞에 한 번 끼어들 뿐, **A2부터는 모독 페이싱이 그대로 유지된다.**

Phase 1·2 설계 검토 중 제안됐던 "PendingRepeat에 phase 깊이를 태그해 소유 요소의 서브트리
소진 시점에 평가" 안은 **폐기**했다. `ProcessPendingRepeats`가 `ProcessDeathStep` 안에서만
호출되고 그 함수가 최상위 경계 게이트 뒤에 있어 애초에 도달할 수 없고, 도달하게 만들면 모독
페이싱이 깨진다.

**요그사론 규칙 신설** (`ProcessPendingRepeats`):

> Rule 6: "Subsequent Phases of a Sequence will not run if a subject is required but is no
> longer in play."

시전자가 회차 도중 필드를 떠나면(자기 광역에 자멸 등) 남은 회차를 발동하지 않는다.
개편 전에는 `CanDoAbilities()`가 침묵만 봤고 보드 이탈 체크는 `OnDeathOther`에만 있어서,
죽은 요그가 계속 시전했다 (검증 D24). `caster.CardData.IsBoardCard() && !IsOnBoard(caster)`로
거른다 — 히어로/클럽/player_ability처럼 보드 카드가 아닌 시전자는 대상 외.

### player_ability를 죽음 페이즈로 편입

`UpdateOngoing`이 0hp인 `player_ability` 카드를 즉시 `DiscardCard`하던 경로를 제거하고,
`ProcessDeathStep`의 수집 대상에 추가했다. 이제 보드 카드와 동일하게 빈사 상태로 남았다가
동시 제거되고 `OnDeath`/`OnDeathOther`를 정상 발동한다 (검증 C22).
부착 카드(`cards_attach`)와 장비(`cards_equip`)는 미사용 타입이라 기존 즉시 제거 경로를 유지한다.

### 유발된 공격

`EffectAttack`이 어빌리티 처리 중에 `AttackTarget`을 부르면 공격 요소가 base attack 큐로
탈출해 depth-first가 깨졌다. `AddTriggeredAttack`으로 바꿔, 적재 중인 Phase가 있으면 그
Phase의 `attacks`에 담고 없으면(플레이어 공격 / 볼리 마이크로스텝) 기존대로 base 큐로 간다.

### Phase 3 검증 (2026-08-02)

수정된 실제 `ResolveQueue.cs`를 스텁 + `ProcessDeathStep`/`ProcessPendingRepeats` 미러와 함께
단독 컴파일해 **58종 전부 통과** (`Tools/ResolveQueueTests`, `dotnet run`).

**① 구조 검증 30종** (`Tests.cs`)

| 그룹 | 내용 |
|---|---|
| A (7) | 이벤트 묶음 / 형제 순서 — 묶음 중간 사망 없음, 연속 이벤트 2·3개 순서 보존, 이벤트 사이 사망 처리, 콜백 안 순서 보존, 한 카드 어빌리티 2개, 빈 이벤트 건너뜀 |
| B (6) | 중첩 / depth-first — 중첩이 대기 형제보다 먼저, 한 효과가 띄운 이벤트 2개 생성 순, 3단 중첩, chain 우선, chain의 중첩, 중첩 도중 사망 없음 |
| C (9) | 죽음 — 동시 죽음 격리, play order 죽메, 죽메 연쇄, 죽메가 대기 Phase보다 먼저, 빈사 카드 반응, 힐 구제 / 파괴 불가, 무적, player_ability 편입 |
| D (7) | 반복 — 모독 페이싱, 요그 자멸 중단 / 생존 계속, 조건 실패 종료, 다른 이벤트보다 먼저, 같은 묶음 형제보다 뒤, 중첩 repeat |
| E (1) | selector 중단·재개 후 depth-first 유지 |

**② 전 트리거 커버리지 28종** (`TriggerCoverage.cs`) — 각 테스트가 `GameLogic`의 **실제
호출부 구조를 그대로 미러링**한다 (주석에 줄번호 명시). 큐를 타는 트리거 22종을 모두 덮는다.

| 그룹 | 트리거 |
|---|---|
| 게임/턴 (4) | `OnGameStart`, `StartOfTurn`(+독약 선행), `EndOfTurn` |
| 카드 사용 (7) | `OnPlay`, `OnPlayOther`, `OnUse`, `OnUseOther`, `OnMove`, `Activate`, PlayCard 전체 시퀀스 |
| 전투 (5) | `OnBeforeAttack`/`OnBeforeDefend`(+Other), `OnAfterAttack`/`OnAfterDefend`(+Other) |
| 피해/회복 (4) | `OnAfterDamage`(단일·광역·연쇄), `OnHeal`/`OnHealOther` |
| 사망 (4) | `OnKill`, `OnDeath`, `OnDeathOther`, 다중 사망 순서 |
| 기타 (4) | `OnDraw`(단일·다중), `OnAddClubOther`, `Ongoing`(큐 미경유) |

이 커버리지에서 **공격 마이크로스텝의 트리거가 사망 처리보다 늦게 발동하는 버그**를 잡았다
(위 「공격 마이크로스텝을 `current_top`으로 잡는 이유」 참조).

인게임 수동 확인은 남아 있음. 영향이 실제로 큰 카드: `OnPlayOther`(`OPO_add_attack1_MUWC`,
`OPO_SH_add_hp1`), `StartOfTurn` 계열(`turn_kill_lowest`, `SOT_Bombard_Fire` 등 10종 이상).

---

## 하스스톤과 의도적으로 다르게 유지하는 것

| 항목 | 하스스톤 | 본 프로젝트 |
|---|---|---|
| repeat 회차의 조건 | (해당 개념 없음) | repeat condition만 평가, trigger condition 미평가. 회차 사이 죽음 페이즈 완결은 하스스톤(모독)과 동일 |
| 무한 연쇄 가드 | 있음 | 없음 (기획 관리) |
| 큐 불변성 | 등록 후 immutable | 배치 선등록 방식이라 사실상 동일 |
| 떠난 카드의 잔존 큐 트리거 | 큐에서 제거 | OnDeathOther만 보드 이탈 가드, 반복 회차는 요그 규칙으로 중단, 그 외는 resolve됨 |
| 비밀 | 같은 Queue에 play order로 섞임, 조건 맞는 것 전부 발동 | 별도 secret 큐(모든 Phase 뒤), 이벤트당 1개만 — **미사용 타입이라 보류** |
| 장비 / 부착 카드 | 파괴도 Death Creation Step 대상 | `UpdateOngoing`에서 즉시 제거 — **미사용 타입이라 보류** |
| 빈사 카드 타겟팅 (Rule 5) | 부정적 효과는 무시, 이로운 효과는 대상에 포함 | 미구현 — **개별 효과 condition으로 처리 예정** |
| 광역 효과의 대상 순서 | play order (Rule 2b) | 보드/슬롯 순회 순서 — **후순위 과제** |
| 죽음 트리거 순서 | 죽메와 타 카드 on-Death가 한 큐에서 play order | 죽메 먼저 → 그다음 OnDeathOther — **후순위 과제** |

## 검증 계획

- `GameLogic`을 skip_delay(AI 예측) 모드로 직접 구동하는 순서 시나리오 검증:
  - 동시 트리거 배치 + 연쇄 depth-first 순서
  - chain이 유발 트리거보다 먼저
  - repeat 회차별 그룹 + trigger condition 미재평가
  - selector 중단/재개 시 순서 유지
  - (Phase 2) 동시 죽음, 죽메 연쇄, 죽음 스텝 반복
  - (Phase 3) 이벤트 묶음 / 형제 FIFO / 요그 규칙 / player_ability — 30종 통과
- 인게임 수동 확인: 기존 카드 풀의 대표 연계 카드. **남아 있음.**

### 회귀 검증 재현 방법

실제 `ResolveQueue.cs`를 Unity 없이 단독 컴파일해 순서만 검증한다. 구조를 다시 손댈 때
이 방식을 그대로 쓰면 된다.

1. 빈 폴더에 `ResolveQueue.cs` + `Pool.cs`를 복사
2. 스텁 작성 — `UnityEngine.Mathf/Debug`, `Game`(state/selector/players), `Player`,
   `Card`(hp/dying/play_order), `AbilityData`, `Slot`, `GameConfig.Timing`,
   `GameState`, `SelectorType`
3. `GameLogic`의 `ProcessDeathStep` / `ProcessPendingRepeats` / `Trigger*AbilityType`를
   미러링한 하네스 작성 (`SetDeathStep`으로 주입)
4. 시나리오마다 로그 문자열을 기대값과 비교

각 시나리오는 `효과명 → 효과명 → [wave:죽은카드들] → 죽메` 형태의 한 줄 로그로 비교한다.
`[wave:...]`는 Death Creation Step이 돈 지점이라 **어디서 죽음이 처리됐는지가 눈에 보인다.**

---

## 코드 진입점 지도 (구조 분석용)

| 관심사 | 위치 |
|---|---|
| Phase 자료구조 / 소진 우선순위 | `Tools/ResolveQueue.cs` — `AbilityPhase`, `FindNextPhase`, `Resolve` |
| 최상위 Phase 큐 / 죽음 경계 | `ResolveQueue.phase_queue`, `current_top`, `Resolve()` ①②③ |
| 이벤트 묶음을 여는 곳 | `GameLogic.TriggerCardAbilityType`(2종), `TriggerOtherCardsAbilityType`, `TriggerPlayerCardsAbilityType` — 전부 `BeginPhase`/`EndPhase` |
| 이벤트가 호출 2번에 걸치는 곳 | `StartGame`(OnGameStart), `BeforeMainPahse`(StartOfTurn), `EndTurn`(EndOfTurn) — 플레이어 루프를 한 번 더 감쌈 |
| 죽음 페이즈 | `GameLogic.ProcessDeathStep` / `HasPendingDeaths` / `IsDying` / `MarkDying` |
| 반복 회차 | `GameLogic.AfterAbilityResolved`(적재) → `ProcessPendingRepeats`(판정 + 요그 규칙) |
| selector 중단·재개 | `ResolveQueue.suspended_scope` ↔ `GameLogic.SelectCard/SelectPlayer/SelectSlot/SelectChoice`의 `BeginPhase` |
| 유발된 공격 | `Effects/EffectAttack.cs` → `GameLogic.AttackTarget` → `ResolveQueue.AddTriggeredAttack` |
| 감싸지 않는 것 (의도적) | secret / attack 마이크로스텝 / callback — `ResolveQueue.Resolve()` ⑤ |

### 용어

| 용어 | 뜻 |
|---|---|
| **이벤트(Event)** | 게임에서 일어난 하나의 사건 (카드 사용, 피해, 사망, 턴 시작…) |
| **Phase** | 한 이벤트에 반응하는 트리거 묶음. 또는 한 효과가 여는 처리 스코프 |
| **최상위 Phase** | 효과 처리 중이 아닐 때 열린 Phase. **이게 끝날 때만 죽음 처리** |
| **중첩 Phase** | 효과 처리 중에 열린 Phase. 부모의 남은 항목보다 먼저 |
| **Queue** | 최상위 Phase들의 대기열 (FIFO) |
| **빈사(mortally wounded)** | hp≤0 또는 `dying`이지만 아직 보드에 있는 상태 |
| **웨이브** | 한 번의 Death Creation Step에서 동시에 제거되는 카드들 |
