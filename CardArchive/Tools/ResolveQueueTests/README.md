# ResolveQueue 효과 처리 순서 회귀 검증

Unity를 띄우지 않고 **실제** `Assets/TcgEngine/Scripts/Tools/ResolveQueue.cs`를 그대로
컴파일해서 효과 발동 순서와 죽음 처리 경계만 검증한다.
`.csproj`가 원본 파일을 **링크**하므로 복사본이 낡을 일이 없다.

```bash
dotnet run --project Tools/ResolveQueueTests
```

## 구성

| 파일 | 역할 |
|---|---|
| `Stubs.cs` | `ResolveQueue.cs`가 컴파일되기 위한 최소 스텁 (`Game`/`Card`/`Player`/`Mathf` 등) |
| `Sim.cs` | `GameLogic`의 `ProcessDeathStep` / `ProcessPendingRepeats` / `Trigger*AbilityType` 미러 |
| `Tests.cs` | 구조 검증 30종 (묶음·형제 순서·중첩·죽음·반복·selector) |
| `TriggerCoverage.cs` | 전 트리거 커버리지 28종 — 각 테스트가 `GameLogic`의 **실제 호출부 구조**를 미러링 (주석에 줄번호) |
| `Sim.Attack.cs` | 전투 흐름 미러 — `AttackCheck → AttackSearch → AttackTargets → AttackTarget → ResolveAttack → ResolveAttackHit → ResolveDeath` |
| `AttackTests.cs` | 전투 시나리오 12종 (단일 교전 / 볼리 / 필드 순회) |
| `Showcase.cs` | 개편 전후 대조 대표 시나리오 (문서용) |

`Sim.cs`는 `GameLogic`의 **미러**다. `GameLogic` 쪽 로직(죽음 수집 대상, 요그 규칙,
이벤트 묶음 방식 등)을 바꾸면 여기도 같이 맞춰야 검증이 의미를 가진다.

## 읽는 법

각 시나리오는 한 줄 로그로 비교한다.

```
갑(광역1) → 을 → [wave:적] → 적죽메
```

- 대괄호가 없는 항목 = 어빌리티가 발동한 것
- `[wave:...]` = **Death Creation Step**이 돌아 그 카드들을 동시에 제거한 지점

즉 `[wave:...]`의 위치가 곧 "어디서 죽음이 처리됐는가"다. 순서 버그는 대부분
이 마커의 위치가 기대와 달라지는 형태로 드러난다.

## 시나리오 그룹

**구조 검증 (`Tests.cs`, 30종)**

| 그룹 | 수 | 내용 |
|---|---|---|
| A | 7 | 이벤트 묶음 / 형제 순서 |
| B | 6 | 중첩 / depth-first / chain 우선 |
| C | 9 | 죽음 처리 (동시 죽음, 죽메 연쇄, 빈사, 무적, player_ability) |
| D | 7 | 반복 — 모독(Defile) 페이싱 / 요그사론 규칙 |
| E | 1 | selector 중단·재개 |

**전 트리거 커버리지 (`TriggerCoverage.cs`, 28종)** — 큐를 타는 트리거 22종 전부

| 그룹 | 수 | 트리거 |
|---|---|---|
| 게임/턴 | 4 | `OnGameStart`, `StartOfTurn`, `EndOfTurn` |
| 카드 사용 | 7 | `OnPlay`, `OnPlayOther`, `OnUse`, `OnUseOther`, `OnMove`, `Activate` |
| 전투 | 5 | `OnBefore/AfterAttack`, `OnBefore/AfterDefend`, 각 `Other` |
| 피해/회복 | 4 | `OnAfterDamage`, `OnHeal`, `OnHealOther` |
| 사망 | 4 | `OnKill`, `OnDeath`, `OnDeathOther` |
| 기타 | 4 | `OnDraw`, `OnAddClubOther`, `Ongoing`(큐 미경유 확인) |

**전투 시나리오 (`AttackTests.cs`, 12종)**

| 그룹 | 수 | 내용 |
|---|---|---|
| 단일 교전 | 5 | 전 트리거 → 데미지 교환(반격) → 후 트리거 → 사망 순서, 상호 사망, 공격 전 트리거로 빈사 |
| 볼리 | 3 | 다중 대상 순차 타격, 볼리 전체가 한 웨이브, overkill guard |
| 필드 순회 | 4 | 유닛별 1회씩 진행, 순회 중 사망 정리, 공격자 사망, 적 전멸 |

전투의 핵심은 **볼리 전체가 하나의 논리적 Phase**라는 것이다 (`death_step_suspended`).
한 유닛이 여러 대상을 때리는 동안 죽음은 보류되고, 볼리가 끝나야 한 웨이브로 정리된다.
그래서 다음 유닛의 차례가 오면 항상 **정리된 보드**를 본다.

커버리지 테스트는 각 트리거마다 세 가지를 본다:
① 한 이벤트의 반응 카드들이 한 묶음인가 (중간에 사망 웨이브가 없는가)
② 연속된 이벤트 **사이**에는 사망 웨이브가 도는가 (Rule 6)
③ 트리거가 유발한 연쇄가 depth-first인가

`Ongoing`은 오라라 resolve 큐를 타지 않고, 비밀(`TriggerSecrets`)과 장비는 미사용 타입이라
보류 상태다 — 커버리지 대상에서 제외.

## 시나리오를 추가할 때

`Tests.cs`에 메서드를 하나 추가하고 `Main()`에서 호출한 뒤 `Check(이름, 기대, 실제)`로
비교하면 된다. 헬퍼:

- `s.TriggerCardAbilityType(카드, 어빌리티들…)` — 카드 한 장의 한 이벤트
- `s.RaiseEvent((카드, 어빌리티), …)` — 여러 카드가 한 이벤트에 반응
- `s.rq.AddCallback(...)` — 턴 진행 콜백 (Phase로 감싸지 않는 경로)
- `Ab(id, effect, rep, cond)` — 어빌리티 생성

## 배경

설계 근거·하스스톤 규칙 인용·개편 이력은 `docs/resolve-queue-hearthstone-redesign.md`
(특히 「Phase 3」 절)에 있다.
