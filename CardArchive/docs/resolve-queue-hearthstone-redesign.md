# Resolve 구조 개편안 — 하스스톤식 (페이즈 스택 + 죽음 페이즈)

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
3. **적용 범위**: ability뿐 아니라 secret/attack/callback 처리 중 유발된 어빌리티도
   전부 depth-first.
4. **Order of play**: 동시 트리거 배치의 발동 순서는 보드 슬롯 순회가 아니라
   **필드에 나온 순서**(먼저 낸 카드가 먼저 발동).
5. **죽음 페이즈** (Phase 2): 죽음을 즉시 처리하지 않고 페이즈가 끝날 때 일괄 처리.
6. 무한 연쇄 가드는 넣지 않는다 (기획으로 관리). — 단 페이즈 스택 구조상 추후
   "스택 깊이 상한" 한 줄로 추가 가능.

목표 순서 (A, B, C 동시 트리거 / A가 chain + D1, D2 유발 / D1이 E 유발 / A 2회 repeat):

```
A1 → A1의 chain → D1 → E → D2 → A2 → (A2의 연계...) → B → C
```

## 진행 단계

| 단계 | 내용 | 상태 |
|---|---|---|
| Phase 1 | 페이즈 스택, is_chain, repeat 규칙, selector 스코프, play order | 구현 완료 |
| Phase 2 | 죽음 페이즈 (Death Creation Step) | 구현 완료 (2026-07-13) |

Phase 1 검증 (2026-07-12): 실제 `ResolveQueue.cs`를 스텁과 함께 단독 컴파일해 순서
시나리오 4종 통과 — ① 동시 배치 depth-first + chain 우선 + repeat 회차 그룹
(A1→Ach→D1→E→D2→A2→…→B→C), ② attack 처리 중 유발 어빌리티 depth-first,
③ selector 중단/재개 시 chain→유발→중단 전 대기분 순서 유지, ④ secret/callback
depth-first. 인게임 수동 확인(기존 카드 풀 대표 연계)은 남아 있음.

Phase 2 검증 (2026-07-13): 같은 방식(실제 `ResolveQueue.cs` + 스텁 단독 컴파일,
`ProcessDeathStep` 로직을 미러링한 하네스)으로 시나리오 5종 통과 —
① 동시 죽음(전원 제거 후 트리거, 같은 웨이브끼리 OnDeathOther 미발동, OnKill →
OnDeath 순, play_order 순), ② 죽메 연쇄(웨이브 반복), ③ 아우라 상실 연쇄(트리거
없는 웨이브에서도 루프 유지), ④ attack 마이크로스텝 사이 죽음 스텝 선행,
⑤ Phase 1 depth-first/chain 회귀. 인게임 수동 확인은 남아 있음.

---

## Phase 1 — 페이즈 스택

### 자료구조 (`Tools/ResolveQueue.cs`)

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
2. `ResolveCardAbility`: trigger condition 검사를 `current_repeat == 0`일 때만.
   반복 회차는 repeat condition(등록 시점의 `AreOngoingRepeatConditionsMet`)만으로
   판정. 침묵(`CanDoAbilities`)·OnDeathOther 보드 이탈 체크는 전 회차 유지.
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

## Phase 2 — 죽음 페이즈 (구현 완료, 2026-07-13)

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

### Death Creation Step

ability 페이즈 스택 + base 큐 + secret 큐가 **모두 빈 시점마다** 실행
(attack_queue 마이크로스텝 사이에도 실행됨):

```
1. UpdateOngoing()                          // 오라 갱신
2. 죽을 대상 수집: dying || GetHP()<=0, play_order 순 정렬
3. 없으면 CheckForWinner() 후 종료
4. 전원 동시에 보드 제거 + 무덤 이동 (트리거 없이), 킬 귀속 확정(OnKill 등록)
5. 죽은 카드의 OnDeath + 생존 카드의 OnDeathOther + 시크릿을 죽은 순서대로
   새 페이즈로 등록
6. resolve 계속 → 큐가 다시 비면 재실행 (죽메 연쇄) → 안정될 때까지 반복
```

- **동시 죽음 규칙**: 4에서 전원을 먼저 제거하므로, 같은 스텝에 죽은 카드끼리는
  서로의 OnDeathOther를 받지 않는다 (하스스톤 Cult Master 규칙).
- `cards_to_clear` 장치는 죽음 스텝에 흡수.
- 승패 판정(`CheckForWinner`)은 죽음 스텝 마지막에 통합.

### 구현 상세 (실제 반영 내용)

- `ResolveQueue.SetDeathStep(death_step, has_deaths)`: GameLogic이
  `ProcessDeathStep` / `HasPendingDeaths`를 훅으로 주입. `Resolve()`는
  ability(페이즈 스택+base) → secret → **죽음 스텝** → attack → callback 순으로 판정,
  `CanResolve()`/`GetNextQueueDelay()`/비-skip `hasMore`에도 `has_deaths` 반영
  (마지막 요소가 남긴 죽음도 루프가 이어서 처리).
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

### Phase 2 리스크 (구현 시 전수 확인 필요)

- 빈사 카드가 죽음 스텝까지 슬롯을 점유 → 그 슬롯 대상 소환/이동 효과 타이밍 변화.
- `last_destroyed` / `last_destroyed_slot` 참조 효과의 시점 변화.
- "죽자마자 부활" 류 효과, `Shishido_Izumi` 등 하드코딩 특수 케이스.
- 클라 연출: 사망 애니메이션이 페이즈 끝에 일괄 발생 (하스스톤과 동일한 연출이지만
  기존 연출 타이밍과 달라짐). 빈사 상태 표시 여부는 별도 결정.

---

## 하스스톤과 의도적으로 다르게 유지하는 것

| 항목 | 하스스톤 | 본 프로젝트 |
|---|---|---|
| 트리거 조건 평가 시점 | 큐 등록 시 + 발동 시 | **발동 시만** (선행 효과가 바꾼 최신 상태 기준) |
| repeat 회차의 조건 | (해당 개념 없음) | repeat condition만 평가, trigger condition 미평가 |
| 무한 연쇄 가드 | 있음 | 없음 (기획 관리) |
| 큐 불변성 | 등록 후 immutable | 배치 선등록 방식이라 사실상 동일 |

## 검증 계획

- `GameLogic`을 skip_delay(AI 예측) 모드로 직접 구동하는 순서 시나리오 검증:
  - 동시 트리거 배치 + 연쇄 depth-first 순서
  - chain이 유발 트리거보다 먼저
  - repeat 회차별 그룹 + trigger condition 미재평가
  - selector 중단/재개 시 순서 유지
  - (Phase 2) 동시 죽음, 죽메 연쇄, 죽음 스텝 반복
- 인게임 수동 확인: 기존 카드 풀의 대표 연계 카드.
