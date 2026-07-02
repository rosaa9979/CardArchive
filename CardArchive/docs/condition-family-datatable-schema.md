# Condition 계열 데이터테이블 설계 (v2 — 전체 재설계)

대상: `ConditionData`(36종) / `ConditionWideAreaRange` / `FilterData`(7종) / `SortData`(1종) / `RepeatConditionData`(2종)
파일: `Condition.xlsx` 한 워크북에 5개 데이터 시트 번들 (Reference, Enum, _컬럼 설명, Condition, WideAreaRange, Filter, Sort, Repeat)

설계 원칙:
1. **누락 금지** — 아래 "타입별 매핑 매트릭스"가 코드의 모든 직렬화 필드를 컬럼으로 커버함을 보증한다.
2. **공용 컬럼 사전** — 5개 시트가 같은 컬럼 이름·의미를 공유한다. (`value`, `range`, `flag`, `scope`, `stat`, `pile`, `card_kinds`, `ref_*` …)
3. **코드를 따라가지 않는다** — 코드가 테이블에 맞춰 바뀌어야 하는 지점은 문서 하단 TODO로 관리.
4. 테이블 포맷 규칙은 `Tool/Skill (2).xlsx` 스펙을 따른다: float/bool 없음(bool→0/1 `!Int`, float→만분율 `!Int`), 리스트는 `;`-join 한 셀 + 스칼라 타입 토큰, `!Table`/`!EndField`/`!EndTable` 마커, enum은 bare 이름 토큰, FK는 `_ID_<Table>`.

---

## 0. 공용 컬럼 사전 (모든 시트 공통 어휘)

| 컬럼 | 타입 토큰 | 의미 |
|---|---|---|
| `id` | `!Id` | **에셋 이름 그대로**(sanitize만). Ability 쪽 FK(RefName)와 일치시키기 위해 `{Type}_{name}` 접두 방식 폐지 → TODO-1 |
| `type` | `ConditionType` 등 | 디스패치 enum. 서브클래스 이름에서 base 접두어 제거한 멤버명 |
| `oper` | `ConditionOperator` | 통합 비교 연산자. bool 연산은 IsTrue→`Equal`, IsFalse→`NotEqual` |
| `value` | `!Int` | 범용 수치: 임계값·인덱스·수량(amount)·최소스택·확률(만분율, 10000=100%) |
| `range` | `!Int` | 거리/범위 (distance, range) |
| `flag` | `!Int` | bool(0/1) 또는 비트마스크(1·2·4). 타입별 의미는 매트릭스 참조 |
| `scope` | `ConditionPlayerType` | Self / Opponent / Both — 플레이어 범위 |
| `stat` | `ConditionStatType` | Attack/HP/Mana + **BossSkill/BossAtg/BossGroggy 확장** → TODO-2 |
| `pile` | `PileType` | 카드 더미 (Effect.xlsx의 enum 참조) |
| `pile_pos` | `PilePosType` | Top / Bottom / Index (ConditionPilePosition.PosMode 승격) → TODO-3 |
| `last_type` | `ConditionLastType` | LastAttacked/LastTargeted/LastSummoned/LastDestroyed/LastPlayed/LastSelected |
| `target_kind` | `ConditionTargetType` | Card / Player / Slot |
| `card_kinds` | `CardType` (;리스트) | 카드 종류 목록 (CardData.xlsx의 enum 참조) |
| `ref_cards` | `_ID_CardData` (;리스트) | 카드 FK 목록 |
| `ref_clubs` | `_ID_Club` (;리스트) | 클럽 FK 목록 |
| `ref_traits` | `_ID_Trait` (;리스트) | 특성 FK 목록 |
| `ref_status` | `_ID_Status` (;리스트) | 상태 FK (StatusType→StatusData 매핑) |
| `sub_conditions` | `_ID_Condition` (;리스트) | Condition 자기참조 (CompositeOr) |

리스트 컬럼에 단일 값이 들어가는 것은 항상 허용(원소 1개 리스트).

---

## 1. Condition 시트 (18컬럼)

필드 행:

```
id | type | oper | value | range | flag | scope | stat | pile | pile_pos | last_type | target_kind | card_kinds | ref_cards | ref_clubs | ref_traits | ref_status | sub_conditions
```

타입 행:

```
!Id | ConditionType | ConditionOperator | !Int | !Int | !Int | ConditionPlayerType | ConditionStatType | PileType | PilePosType | ConditionLastType | ConditionTargetType | CardType | _ID_CardData | _ID_Club | _ID_Trait | _ID_Status | _ID_Condition
```

### 타입별 매핑 매트릭스 (코드 필드 → 컬럼, 누락 검증표)

| ConditionType | 사용 컬럼 ← 원본 필드 | 비고 |
|---|---|---|
| `Stat` | `stat`←type, `oper`, `value` | 카드/플레이어 기본 스탯 비교 |
| `StatCustom` | `ref_traits`←trait, `oper`, `value` | 커스텀 스탯(Trait) 비교 |
| `PlayerStat` | `stat`←type, `oper`, `value` | 대상 소유 플레이어의 스탯 (TODO-8: Stat과 통합 후보) |
| `ClubStatMatch` | `ref_clubs`←club, `ref_traits`←trait, `oper` | 시전자 trait vs 클럽카드 trait |
| `BossGauge` | `stat`←gauge(Boss*), `oper`, `value`, `flag`←compare_to_max(0/1) | boss_state 없으면 false |
| `CardType` | `card_kinds`←has_type, `ref_clubs`←has_club, `ref_traits`←has_trait, `oper` | 리스트 내부는 OR, 그룹 간 AND |
| `CardData` | `ref_cards`←card_types, `oper` | 특정 카드 목록 포함 여부 |
| `Status` | `ref_status`←has_status, `value`(최소 스택), `oper` | 스택 비교는 `>=` 고정 (TODO-9) |
| `Damaged` | `oper` | |
| `Exhaust` | `oper` | |
| `Equipped` | `oper` | |
| `Deckbuilding` | `oper` | |
| `Owner` | `oper` | |
| `OwnerAI` | `oper` | AI 전용 (TODO-7: Owner+flag 통합 후보) |
| `Self` | `oper` | |
| `Target` | `target_kind`←type, `oper` | 대상 카테고리 판별 |
| `Triggered` | `oper`←is_oper | ability_triggerer 일치 |
| `Count` | `scope`←target, `pile`, `oper`, `value`, `card_kinds`←has_type, `ref_clubs`←has_club, `ref_traits`←has_trait, `ref_cards`←has_card | 필터 그룹 간 AND, 그룹 내 OR |
| `CardPile` | `pile`←type, `oper` | |
| `PilePosition` | `pile`, `pile_pos`←mode, `value`←index, `oper` | index는 mode==Index일 때만 |
| `CanPlace` | `last_type`, `ref_cards`←place_card(단일), `scope`←card_owner, `oper` | last_type 우선, 없으면 place_card, 둘 다 없으면 caster |
| `SlotDist` | `range`←distance, `flag`←diagonals(0/1) | 이동거리 기준 |
| `SlotRange` | `oper` | caster.GetRange() 사용, 파라미터 없음 |
| `SlotNeighbor` | `range` | |
| `SlotPid` | `flag`←player(1)+opponent(2)+neutral(4) 비트합 | `SlotSideMask` |
| `SlotLocate` | `flag`←Inside(1)+Outside(2)+Neutral(4) 비트합 | `SlotZoneMask` |
| `SlotAttachmentEmpty` | `oper` | |
| `SlotUnitEmpty` | `oper` | |
| `Turn` | `oper` | 내 턴 여부 |
| `Rolled` | `oper`, `value` | 주사위 값 |
| `Once` | (없음) | 턴당 1회 |
| `Possibility` | `value`←possibility(만분율) | float→×10000 |
| `None` | (없음) | 항상 true |
| `LastTypeExist` | `last_type`←type, `oper` | 현재 LastSelected만 구현됨 |
| `LastTypeRange` | `last_type`←type, `range`, `oper` | |
| `CompositeOr` | `sub_conditions`←any | 빈 리스트=항상 true |

`ConditionWideAreaRange`는 이 시트에서 제외(전용 시트). `ConditionType.WideAreaRange` 멤버는 삭제 → TODO-4.

---

## 2. WideAreaRange 시트 (4컬럼)

```
id | dx | dy | thumbnail
!Id | !Int(;리스트) | !Int(;리스트) | !String
```

| 컬럼 | ← 원본 필드 | 비고 |
|---|---|---|
| `dx` / `dy` | directions[].dx / .dy | 인덱스 정렬된 페어 리스트. 두 셀의 원소 수 동일해야 함(검증 규칙) |
| `thumbnail` | thumnail(Sprite) | 스프라이트 에셋 경로 문자열. Sprite 직접 직렬화 불가 → 로더에서 경로 로드 (TODO-5) |

player_id==1일 때 dx/dy 부호 반전(미러링)은 **데이터가 아니라 룰** — 코드 유지, 테이블에 넣지 않음.

---

## 3. Filter 시트 (11컬럼)

```
id | type | value | range | flag | scope | stat | pile | card_kinds | ref_clubs | ref_traits
!Id | FilterType | !Int | !Int | !Int | ConditionPlayerType | ConditionStatType | PileType | CardType | _ID_Club | _ID_Trait
```

| FilterType | 사용 컬럼 ← 원본 필드 | 비고 |
|---|---|---|
| `First` | `value`←amount | 앞에서 N개 |
| `Random` | `value`←amount, `flag`←rest(0/1) | rest=1이면 "N개 제외한 나머지" |
| `RandomCount` | `pile`, `scope`←player_self, `card_kinds`←has_type, `ref_clubs`←has_club, `ref_traits`←has_trait | 더미 카운트만큼 랜덤 선택. 단일→리스트 통일: TODO-6 |
| `HighestStat` | `stat`←stat | 최고 스탯 전부 |
| `LowestStat` | `stat`←stat | 최저 스탯 전부 |
| `MostUnitSlot` | `range`←distance, `scope`←player_type | 주변 유닛 최다 슬롯 중 랜덤 1개 |
| `MostWoundedSlot` | `range`←distance, `scope`←player_type | 주변 부상 유닛 최다 슬롯 중 랜덤 1개 |

`FilterPlayerType`은 `ConditionPlayerType`과 중복 enum → 삭제·통일 (TODO-6). `player_self`(bool)도 enum으로 교체.

---

## 4. Sort 시트 (4컬럼)

```
id | type | flag | ref_traits
!Id | SortType | !Int | _ID_Trait
```

| SortType | 사용 컬럼 ← 원본 필드 | 비고 |
|---|---|---|
| `Trait` | `flag`←descending(0/1, base 클래스 필드), `ref_traits`←trait | 0=오름차순, 1=내림차순. trait 없는 대상은 0 취급 |

---

## 5. Repeat 시트 (8컬럼)

```
id | type | value | scope | pile | card_kinds | ref_clubs | ref_traits
!Id | RepeatType | !Int | ConditionPlayerType | PileType | CardType | _ID_Club | _ID_Trait
```

| RepeatType | 사용 컬럼 ← 원본 필드 | 비고 |
|---|---|---|
| `StaticValue` | `value` | 고정 N회 반복 |
| `CountType` | `pile`, `scope`←player, `card_kinds`←has_type, `ref_clubs`←has_club, `ref_traits`←has_trait | 카운트만큼 반복. 단일→리스트 통일 + `ref_cards`(has_card) 추가로 Count와 필터 시맨틱 일치: TODO-6 |

---

## 6. Ability 테이블 연동 (FK 컬럼)

| AbilityData 필드 | 테이블 컬럼 | 타입 토큰 |
|---|---|---|
| `conditions_trigger` | `cond_trigger` | `_ID_Condition` (;리스트) |
| `conditions_criteria_target` | `cond_criteria` | `_ID_Condition` (;리스트) |
| `condition_target` | `cond_target` | `_ID_Condition` (;리스트) |
| `condition_wide_range` | `wide_range` | `_ID_WideAreaRange` (단일) |
| `filters_target` | `filters` | `_ID_Filter` (;리스트, 순서=적용 순서) |
| `sort_target` | `sort` | `_ID_Sort` (단일) |
| `condition_repeat` | `repeat` | `_ID_Repeat` (단일) |

id를 전부 에셋 이름으로 통일하므로 기존 "Ability FK(RefName) vs `{Type}_{name}` key 불일치" 문제가 구조적으로 해소된다 (TODO-1).

---

## 7. Enum / Reference 시트 구성

- Enum 시트에 정의: `ConditionType`, `FilterType`, `SortType`, `RepeatType`, `ConditionOperator`, `ConditionPlayerType`, `ConditionStatType`(Boss 확장 포함), `PilePosType`, `ConditionLastType`, `ConditionTargetType`
- Reference 시트로 참조: `Effect.xlsx`(PileType), `CardData.xlsx`(CardType), `Club.xlsx`, `Trait.xlsx`, `Status.xlsx`
- `flag` 비트마스크 값표(SlotSideMask/SlotZoneMask)는 `_컬럼 설명` 시트에 기재 (외부 툴에 flags-enum 타입 없음)

---

## 8. TODO — 코드 수정 필요 사항

| # | 내용 | 필수/선택 |
|---|---|---|
| TODO-1 | Filter/Sort/Repeat의 export id를 `{Type}_{assetname}` → **에셋 이름 단독**으로 변경 (`DataTableExporter.cs`). Ability FK 불일치 해소 | **필수** |
| TODO-2 | `ConditionStatType`에 `BossSkill=40, BossAtg=41, BossGroggy=42` 추가, `ConditionBossGauge.gauge`(BossGaugeType) → 통합 enum 사용. `gauge` 전용 컬럼 제거의 전제 | **필수** |
| TODO-3 | `ConditionPilePosition.PosMode`(중첩 enum) → 최상위 `PilePosType`으로 승격 (`DataTableTypes.cs`) | **필수** |
| TODO-4 | `ConditionType.WideAreaRange` 멤버 삭제 (전용 테이블이므로 디스패치 대상 아님) | **필수** |
| TODO-5 | WideAreaRange `thumnail`(Sprite) → 테이블 `!String` 경로에서 로드하는 코드 (+오타 `thumnail`→`thumbnail` 정리) | **필수** |
| TODO-6 | Filter/Repeat 필터 시맨틱을 Count와 통일: `FilterPlayerType` 삭제→`ConditionPlayerType`, `FilterRandomCount.player_self`(bool)→enum, `FilterRandomCount`·`RepeatCountType`의 has_type/has_club/has_trait 단일→`List`, `RepeatCountType`에 `has_card` 추가 | **필수** (컬럼 공용화 전제) |
| TODO-7 | `ConditionOwnerAI` = `ConditionOwner` + `ai_only` flag로 통합 → 타입 1개 감소, `flag` 컬럼 재사용 | 선택 |
| TODO-8 | `ConditionPlayerStat` = `ConditionStat` + "대상의 소유 플레이어" 해석 차이뿐 → subject 플래그로 통합 | 선택 |
| TODO-9 | `ConditionStatus` 스택 비교가 `>=` 고정이고 `oper`는 bool 반전에만 사용 → 스택 비교에 `oper` 적용하도록 일반화 (기존 데이터는 Equal+value 유지 변환) | 선택 |
| TODO-10 | 런타임 `ConditionOperatorBool`/`ConditionOperatorInt` 2종 → 통합 `ConditionOperator` 1종으로 코드도 정리 (테이블은 이미 통합) | 선택 |
| TODO-11 | 테이블→에셋(또는 런타임 객체) 생성 로더: `type` 디스패치 팩토리 + 컬럼→필드 역매핑. 본 문서의 매트릭스가 명세 | **필수** (임포트 시점) |
