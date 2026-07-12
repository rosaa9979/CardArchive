# Condition·Effect 계열 데이터테이블 설계 (v2 — 전체 재설계)

대상: `ConditionData`(36종) / `ConditionWideAreaRange` / `FilterData`(7종) / `SortData`(1종) / `RepeatConditionData`(2종) / `EffectData`(49종, 8절)
파일: `Condition.xlsx` 한 워크북에 5개 데이터 시트 번들 (Reference, Enum, _컬럼 설명, Condition, WideAreaRange, Filter, Sort, Repeat), `Effect.xlsx`에 Effect 시트

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
| `flag` | `!Int` | **순수 bool(0/1)** — compare_to_max·diagonals·rest·descending·use_stored_value 등. 비트마스크 용법은 폐지(side/zone으로 분리) |
| `scope` | `ConditionPlayerType` | Self / Opponent / Both — 플레이어 범위. Effect의 self/opp bool·EffectPlayerType(All→Both)도 여기로 통합 |
| `stat` | `ConditionStatType` | Attack/HP/Mana + **Range(Effect)·BossSkill/BossAtg/BossGroggy 확장** → TODO-2 |
| `side` | `SlotSide` (단일) | 슬롯 진영: Player/Opponent/Neutral (SlotPid). 다중은 CompositeOr 분해 |
| `zone` | `SlotZone` (단일) | 슬롯 구역: Inside/Outside/Neutral (SlotLocate). 다중은 CompositeOr 분해 |
| `pile` | `PileType` | 카드 더미 (Effect.xlsx의 enum 참조) |
| `pile_pos` | `PilePosType` | Top / Bottom / Index (ConditionPilePosition.PosMode 승격) → TODO-3 |
| `last_type` | `ConditionLastType` | LastAttacked/LastTargeted/LastSummoned/LastDestroyed/LastPlayed/LastSelected |
| `target_kind` | `ConditionTargetType` | Card / Player / Slot |
| `card_kind` | `CardType` (단일) | 카드 종류 (CardData.xlsx의 enum 참조). 빈 칸=필터 없음 |
| `ref_card` | `_ID_CardData` (단일) | 카드 FK |
| `ref_club` | `_ID_Club` (단일) | 클럽 FK |
| `ref_trait` | `_ID_Trait` (단일) | 특성 FK |
| `ref_status` | `_ID_Status` (단일) | 상태 FK (StatusType→StatusData 매핑) |
| `sub_conditions` | `_ID_Condition` (;리스트) | Condition 자기참조. **CompositeOr=OR 목록, Count=카드 필터(AND) 목록** |

**v2.1 — OR 구조화 원칙**: ref/enum 매칭 컬럼은 **긍정 매칭(oper=Equal)일 때 셀당 값 1개**만 담는다. 여러 값에 대한 OR은 데이터 구조로 표현한다 — exporter가 다중 값 에셋을 **단일 값 leaf 행 + CompositeOr 부모 행**으로 분해하고, 부모 행이 원래 에셋 id를 가지므로 Ability FK는 그대로 유효하다.

**예외 — AND는 리스트 허용**: 부정 매칭(oper=NotEqual)의 다중 값은 ¬(A∨B)=(¬A)∧(¬B), 즉 AND("모두 아님")이므로 `;` 리스트를 셀에 그대로 남긴다. NotEqual 행은 매칭식 전체를 부정한다. 그 외 리스트 셀은 `sub_conditions`(Count=AND, CompositeOr=OR)와 WideAreaRange의 `dx`/`dy`.

---

## 1. Condition 시트 (18컬럼)

필드 행:

```
id | type | oper | value | range | flag | scope | stat | pile | pile_pos | last_type | target_kind | side | zone | card_kind | ref_card | ref_club | ref_trait | ref_status | sub_conditions
```

타입 행:

```
!Id | ConditionType | ConditionOperator | !Int | !Int | !Int | ConditionPlayerType | ConditionStatType | PileType | PilePosType | ConditionLastType | ConditionTargetType | SlotSide | SlotZone | CardType | _ID_CardData | _ID_Club | _ID_Trait | _ID_Status | _ID_Condition
```

### OR 분해 규칙 (exporter가 자동 수행)

| 원본 | 분해 결과 |
|---|---|
| `CardType` 다중 리스트, oper=Equal (그룹 간 AND, 그룹 내 OR) | 그룹들의 데카르트 곱(DNF) → 단일 값 leaf `CardType` 행들 + 부모 `CompositeOr` 행(원래 에셋 id). 예: `is_citizen`(Student∨NonStudent) → `is_kind_Student`, `is_kind_NonStudent` + CompositeOr |
| `CardData` 다중 카드, oper=Equal | leaf `CardData` 행들(`is_card_{id}`) + 부모 `CompositeOr` |
| `CardType`/`CardData` 다중, **oper=NotEqual** | 분해 없음 — `;` 리스트를 셀에 유지 (모두 아님 = AND) |
| `Count`의 has_type/club/trait/card 그룹 | 각 그룹 → 단일 leaf 또는 `CompositeOr`(`{id}_kinds` 등), 그룹 id들을 `sub_conditions`에 AND 목록으로 기재 |
| `SlotPid`/`SlotLocate` 체크박스 다중 | 단일 `side`/`zone` leaf 행(`is_side_Player`, `is_zone_Inside` …) + 부모 `CompositeOr`. 체크 1개면 그 행에 직접 |
| 단일 값(원소 ≤1) | 분해 없이 그 행에 직접 기재 |

- 합성(leaf/or) 행은 **내용 기준으로 dedup**된다 — 같은 leaf(`is_kind_Student` 등)는 여러 부모가 공유.
- 합성 행 id는 내용 유래(`is_kind_X`, `is_club_X`, `is_trait_X`, `is_card_X`), 실제 에셋 id를 먼저 선점한 뒤 생성하므로 충돌 시 합성 쪽에 `_2`가 붙는다.

### 타입별 매핑 매트릭스 (코드 필드 → 컬럼, 누락 검증표)

| ConditionType | 사용 컬럼 ← 원본 필드 | 비고 |
|---|---|---|
| `Stat` | `stat`←type, `oper`, `value` | 카드/플레이어 기본 스탯 비교 |
| `StatCustom` | `ref_trait`←trait, `oper`, `value` | 커스텀 스탯(Trait) 비교 |
| `PlayerStat` | `stat`←type, `oper`, `value` | 대상 소유 플레이어의 스탯 (TODO-8: Stat과 통합 후보) |
| `ClubStatMatch` | `ref_club`←club, `ref_trait`←trait, `oper` | 시전자 trait vs 클럽카드 trait |
| `BossGauge` | `stat`←gauge(Boss*), `oper`, `value`, `flag`←compare_to_max(0/1) | boss_state 없으면 false |
| `CardType` | `card_kind`·`ref_club`·`ref_trait`←has_type/has_club/has_trait, `oper` | 설정된 필드는 AND. 다중 OR(Equal)은 CompositeOr로 분해, NotEqual은 `;`목록 유지(모두 아님) |
| `CardData` | `ref_card`←card_types, `oper` | 다중 OR(Equal)은 CompositeOr로 분해, NotEqual은 `;`목록 유지(모두 아님) |
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
| `Count` | `scope`←target, `pile`, `oper`, `value`, `sub_conditions`←has_type/club/trait/card 그룹 분해 | 카드 필터: sub_conditions를 **모두** 충족한 카드만 셈 (그룹 간 AND, 그룹 내 OR은 CompositeOr) — 런타임 지원 TODO-12 |
| `CardPile` | `pile`←type, `oper` | |
| `PilePosition` | `pile`, `pile_pos`←mode, `value`←index, `oper` | index는 mode==Index일 때만 |
| `CanPlace` | `last_type`, `ref_card`←place_card, `scope`←card_owner, `oper` | last_type 우선, 없으면 place_card, 둘 다 없으면 caster |
| `SlotDist` | `range`←distance, `flag`←diagonals(0/1) | 이동거리 기준 |
| `SlotRange` | `oper` | caster.GetRange() 사용, 파라미터 없음 |
| `SlotNeighbor` | `range` | |
| `SlotPid` | `side`←player/opponent/neutral (단일) | 다중 체크는 CompositeOr로 분해 |
| `SlotLocate` | `zone`←Inside/Outside/Neutral (단일) | 다중 체크는 CompositeOr로 분해 |
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
id | type | value | range | flag | scope | stat | pile | card_kind | ref_club | ref_trait
!Id | FilterType | !Int | !Int | !Int | ConditionPlayerType | ConditionStatType | PileType | CardType | _ID_Club | _ID_Trait
```

| FilterType | 사용 컬럼 ← 원본 필드 | 비고 |
|---|---|---|
| `First` | `value`←amount | 앞에서 N개 |
| `Random` | `value`←amount, `flag`←rest(0/1) | rest=1이면 "N개 제외한 나머지" |
| `RandomCount` | `pile`, `scope`←player_self, `card_kind`←has_type, `ref_club`←has_club, `ref_trait`←has_trait | 더미 카운트만큼 랜덤 선택. 필터는 원래 단일 값이라 분해 불필요 |
| `HighestStat` | `stat`←stat | 최고 스탯 전부 |
| `LowestStat` | `stat`←stat | 최저 스탯 전부 |
| `MostUnitSlot` | `range`←distance, `scope`←player_type | 주변 유닛 최다 슬롯 중 랜덤 1개 |
| `MostWoundedSlot` | `range`←distance, `scope`←player_type | 주변 부상 유닛 최다 슬롯 중 랜덤 1개 |

`FilterPlayerType`은 `ConditionPlayerType`과 중복 enum → 삭제·통일 (TODO-6). `player_self`(bool)도 enum으로 교체.

---

## 4. Sort 시트 (4컬럼)

```
id | type | flag | ref_trait
!Id | SortType | !Int | _ID_Trait
```

| SortType | 사용 컬럼 ← 원본 필드 | 비고 |
|---|---|---|
| `Trait` | `flag`←descending(0/1, base 클래스 필드), `ref_trait`←trait | 0=오름차순, 1=내림차순. trait 없는 대상은 0 취급 |

---

## 5. Repeat 시트 (8컬럼)

```
id | type | value | scope | pile | card_kind | ref_club | ref_trait
!Id | RepeatType | !Int | ConditionPlayerType | PileType | CardType | _ID_Club | _ID_Trait
```

| RepeatType | 사용 컬럼 ← 원본 필드 | 비고 |
|---|---|---|
| `StaticValue` | `value` | 고정 N회 반복 |
| `CountType` | `pile`, `scope`←player, `card_kind`←has_type, `ref_club`←has_club, `ref_trait`←has_trait | 카운트만큼 반복. 필터는 원래 단일 값이라 분해 불필요 |

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

**공용 enum의 소유는 Effect.xlsx** (Condition→Effect 단방향 참조가 이미 있으므로 순환을 만들지 않는 배치):

- `Effect.xlsx` Enum 시트: `EffectType`, `EffectTotalCountType`, `EffectDamageType`, `EffectValueType`, `EffectStatusType`, `PileType`, **`ConditionStatType`(Range·Boss 확장), `ConditionPlayerType`, `ConditionLastType`(Self·AbilityTriggerer 확장), `PilePosType`(가상)**
- `Condition.xlsx` Enum 시트: `ConditionType`(WideAreaRange 제외), `FilterType`, `SortType`, `RepeatType`, `ConditionOperator`, `ConditionTargetType`, **`SlotSide`/`SlotZone`(가상, 값 1·2·4 = 런타임 마스크 비트)**
- 테이블에서 사라진 enum: `EffectStatType`→ConditionStatType, `EffectPlayerType`→ConditionPlayerType, `EffectActionType`→ConditionLastType, `DeckInsert`→PilePosType, `BossGaugeType`→ConditionStatType(Boss*), `FilterPlayerType`, `PosMode`
- Reference: Condition.xlsx→Effect.xlsx·CardData.xlsx·Club.xlsx·Trait.xlsx·Status.xlsx / Effect.xlsx→CardData·Club·Trait·Status·Ability·Weapon·**Condition.xlsx**(ref_condition FK — Condition↔Effect 상호 참조는 v1부터 존재, 툴 허용 여부 확인 필요)

---

## 8. Effect 시트 (Effect.xlsx, 29컬럼)

Condition 계열과 같은 철학: 셀당 값 1개, 공용 컬럼 어휘, 타입 디스패치. "전부 실행/사용" 의미의 목록만 리스트 셀(`effects_true`/`effects_false`/`weighted_cards`). id = 에셋 이름(Ability.effects FK와 일치).

필드 행:

```
id | type | value | flag | scope | stat | total | pile | pile_pos | last_type | damage_kind | value_kind | status_kind | card_kind | ref_card | ref_club | ref_trait | bonus_trait | ref_status | ref_ability | ref_weapon | ref_condition | effects_true | effects_false | weighted_cards | x | y | dx | dy
```

타입 행:

```
!Id | EffectType | !Int | !Int | ConditionPlayerType | ConditionStatType | EffectTotalCountType | PileType | PilePosType | ConditionLastType | EffectDamageType | EffectValueType | EffectStatusType | CardType | _ID_CardData | _ID_Club | _ID_Trait | _ID_Trait | _ID_Status | _ID_Ability | _ID_Weapon | _ID_Condition | _ID_Effect | _ID_Effect | !String | !Int | !Int | !Int | !Int
```

### 타입별 매핑 매트릭스 (49종, 누락 검증표)

| EffectType | 사용 컬럼 ← 원본 필드 | 비고 |
|---|---|---|
| `SetStat` | `stat`←type | 능력 value로 설정 |
| `AddStat` | `stat`←type, `flag`←use_stored_value | |
| `ResetStat` | (없음) | |
| `SetStatCustom` | `ref_trait`←trait | |
| `AddStatRoll` | `stat`←type | 주사위 값 가산 |
| `AddStatCount` | `stat`←type, `pile`, `scope`←player, `card_kind`·`ref_club`·`ref_trait`←has_* | 카운트 × value |
| `AddStatTotalCount` | `stat`←stat_type, `total`←total_type | |
| `CopyStat` | `ref_trait`←trait, `ref_club`←require_club, `flag`←only_if_missing | |
| `CycleStat` | `ref_trait`←trait | |
| `Damage` | `damage_kind`←damage_type, `value_kind`←value_type, `bonus_trait`←bonus_damage | |
| `DamageRatio` | `bonus_trait`←bonus_damage, `value`←ratio(만분율) | |
| `DamageCount` | `damage_kind`, `bonus_trait`, `pile`, `scope`←player, `card_kind`·`ref_club`·`ref_trait`←has_* | |
| `Heal` | `value_kind`←heal_type, `bonus_trait`←bonus_heal | |
| `Draw` / `Discard` / `Shuffle` / `ClearTemp` / `Play` / `Knockback` / `Destroy` / `DestroyEquip` / `Mana` / `SetClubCardUI` | (없음) | |
| `Create` | `pile`←create_pile, `scope`←create_opponent | |
| `CreateCard` | `weighted_cards`←create_card, `pile`←create_pile, `flag`←is_same_possibility, `scope`←create_opponent | `카드=가중치;…` |
| `SendPile` | `pile`, `pile_pos`←insert | DeckInsert 대체 |
| `MovePileTopToBottom` | `pile` | |
| `PlayCard` | `ref_card`←play_card | |
| `UseCard` | `ref_card`←use, `scope`←use_opponent | |
| `Transform` | `ref_card`←transform_to | |
| `SummonSlot` | `x`/`y`←position, `dx`/`dy`←direction | |
| `MoveUnit` | `last_type`←target_type | EffectActionType 대체 |
| `Attack` / `AttackRedirect` | `last_type`←attacker_type | |
| `AddAbility` / `RemoveAbility` | `ref_ability`←gain/remove_ability | |
| `AddTrait` / `RemoveTrait` | `ref_trait`←trait | |
| `AddClub` | `ref_club`←club | |
| `ClearStatus` | `ref_status`←status, `status_kind`←status_type | ref_status 빈 칸이면 kind 일괄 |
| `AttachCard` | `ref_card`←attach | |
| `ChangeWeapon` | `ref_weapon`←weapon | |
| `ChangeOwner` | `scope`←owner_opponent | |
| `Exhaust` | `flag`←exhausted | |
| `Roll` | `value`←dice | |
| `BossGauge` | `stat`←gauge(Boss*), `flag`←set_to_value, `value`←delta 또는 set_value | flag가 어느 int인지 선택 |
| `StoreCount` | `pile`, `scope`←player, `card_kind`·`ref_club`·`ref_trait`←has_* | |
| `Conditional` / `ConditionalCaster` | `ref_condition`←condition, `effects_true`, `effects_false` | 효과 목록은 순차 전부 실행 |

`EffectType` enum 멤버 2개를 클래스명 규약에 맞게 수정함: `Attach`→`AttachCard`, `Move`→`MoveUnit` (`DataTableTypes.cs`, export 전용이라 게임 로직 영향 없음).

---

## 9. TODO — 코드 수정 필요 사항

| # | 내용 | 필수/선택 |
|---|---|---|
| TODO-1 | Filter/Sort/Repeat의 export id를 `{Type}_{assetname}` → **에셋 이름 단독**으로 변경 (`DataTableExporter.cs`). Ability FK 불일치 해소 | **필수** |
| TODO-2 | `ConditionStatType`에 `Range=35, BossSkill=40, BossAtg=41, BossGroggy=42` 추가 → `ConditionBossGauge.gauge`(BossGaugeType)·`EffectStatType`·`EffectBossGauge.gauge`가 통합 enum 사용. `gauge`/`EffectStatType` 컬럼 제거의 전제 | **필수** |
| TODO-3 | `ConditionPilePosition.PosMode`(중첩 enum) → 최상위 `PilePosType`으로 승격 (`DataTableTypes.cs`) | **필수** |
| TODO-4 | `ConditionType.WideAreaRange` 멤버 삭제 (전용 테이블이므로 디스패치 대상 아님) | **필수** |
| TODO-5 | WideAreaRange `thumnail`(Sprite) → 테이블 `!String` 경로에서 로드하는 코드 (+오타 `thumnail`→`thumbnail` 정리) | **필수** |
| TODO-6 | `FilterPlayerType` 삭제→`ConditionPlayerType` 통일, `FilterRandomCount.player_self`(bool)→enum. (v2.1: 리스트 통일안은 폐기 — 모든 매칭 필드는 단일 값이 표준) | **필수** |
| TODO-7 | `ConditionOwnerAI` = `ConditionOwner` + `ai_only` flag로 통합 → 타입 1개 감소, `flag` 컬럼 재사용 | 선택 |
| TODO-8 | `ConditionPlayerStat` = `ConditionStat` + "대상의 소유 플레이어" 해석 차이뿐 → subject 플래그로 통합 | 선택 |
| TODO-9 | `ConditionStatus` 스택 비교가 `>=` 고정이고 `oper`는 bool 반전에만 사용 → 스택 비교에 `oper` 적용하도록 일반화 (기존 데이터는 Equal+value 유지 변환) | 선택 |
| TODO-10 | 런타임 `ConditionOperatorBool`/`ConditionOperatorInt` 2종 → 통합 `ConditionOperator` 1종으로 코드도 정리 (테이블은 이미 통합) | 선택 |
| TODO-11 | 테이블→에셋(또는 런타임 객체) 생성 로더: `type` 디스패치 팩토리 + 컬럼→필드 역매핑. 본 문서의 매트릭스가 명세 | **필수** (임포트 시점) |
| TODO-12 | `ConditionCount`가 `sub_conditions`(카드 필터, AND) 기반으로 동작하도록 런타임 확장 — 현재는 has_* 리스트 내장. 테이블은 이미 분해 형태로 export됨 | **필수** (임포트 시점) |
| ~~TODO-13~~ | 해결됨 (2026-07-05): NotEqual+다중 리스트는 AND("모두 아님")이므로 `;` 리스트를 셀에 유지하는 것으로 규칙 확정 — CompositeAnd 불필요 | 완료 |
| TODO-14 | `ConditionSlotPid`/`ConditionSlotLocate`: bool 3개 → 단일 enum(SlotSide/SlotZone) 필드로 코드 재구성(로더가 CompositeOr 분해 형태를 읽는 전제). 값 1·2·4는 기존 마스크 비트와 일치 | **필수** (임포트 시점) |
| TODO-15 | Effect 공용화의 코드 반영: `EffectStatType`→ConditionStatType, `EffectPlayerType`→ConditionPlayerType(All→Both, Player→Self), `EffectActionType`→ConditionLastType(+Self=10, AbilityTriggerer=11), `DeckInsert`→PilePosType, self/opp bool 필드들→ConditionPlayerType | **필수** (임포트 시점) |
| ~~TODO-16~~ | 완료 (2026-07-05): `EffectType` 멤버명 규약 일치 — `Attach`→`AttachCard`, `Move`→`MoveUnit` (`DataTableTypes.cs`) | 완료 |
