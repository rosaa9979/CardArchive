# Condition / Effect Data 파라미터 정리

테이블 설계 참고용. 프로젝트 내 `ConditionData` / `EffectData` 를 상속한 모든 ScriptableObject 클래스의 직렬화 필드(=인스펙터 노출 파라미터)를 클래스별로 분리해서 정리했다.

- 베이스 클래스 자체 필드: 없음 (`ConditionData`, `EffectData` 모두 ScriptableObject 외 직렬화 필드 없음 — 메서드 오버라이드만 존재)
- `[Header]` 는 인스펙터 그룹 라벨이며 실제 필드가 아니다.
- 모든 Condition / Effect 는 `[CreateAssetMenu]` 로 에셋 생성된다.
- **wide CSV** 는 같은 폴더의 `Conditions.csv` / `Effects.csv` 참조 (각 클래스가 한 행, 파라미터마다 별도 컬럼).

---

## 1. 공통 파라미터 어휘 (모든 enum / 참조 타입 전수)

### 1-1. Condition 베이스 enum

| Enum | 정의 위치 | 값 (=정수값) |
| --- | --- | --- |
| `ConditionOperatorInt` | `ConditionData.cs` | Equal=0, NotEqual=1, GreaterEqual=2, LessEqual=3, Greater=4, Less=5 |
| `ConditionOperatorBool` | `ConditionData.cs` | IsTrue=0, IsFalse=1 |
| `ConditionLastType` | `ConditionLastTypeExist.cs` | None=0, LastAttacked=1, LastTargeted=2, LastSummoned=3, LastDestroyed=4, LastPlayed=5, LastSelected=6 |
| `ConditionPlayerType` | `ConditionCount.cs` | Self=0, Opponent=1, Both=2 |
| `ConditionStatType` | `ConditionStat.cs` | None=0, Attack=10, HP=20, Mana=30 |
| `ConditionTargetType` | `ConditionTarget.cs` | None=0, Card=10, Player=20, Slot=30 |

### 1-2. Effect 베이스 enum

| Enum | 정의 위치 | 값 (=정수값) |
| --- | --- | --- |
| `EffectStatType` | `EffectAddStat.cs` | None=0, Attack=10, HP=20, Mana=30, Range=40 |
| `EffectValueType` | `EffectDamage.cs` | Value=0, Attack=1, Health=2 |
| `EffectDamageType` | `EffectDamage.cs` | Card=0, Slot=1 |
| `EffectActionType` | `EffectAttack.cs` | Self=1, AbilityTriggerer=25, LastPlayed=70, LastTargeted=72, LastSelected=74 |
| `EffectPlayerType` | `EffectAddStatCount.cs` | All=0, Player=10, Opponent=20 |
| `EffectLastType` | `EffectUseCard.cs` | None=0, LastAttacked=1, LastTargeted=2, LastSummoned=3, LastDestroyed=4, LastPlayed=5 |
| `EffectStatusType` | `EffectClearStatus.cs` | BadStatus=0, GoodStatus=1, BothStatus=2 |
| `EffectTotalCountType` | `EffectAddStatTotalCount.cs` | None=0, TotalHeal=10 |
| `PileType` | `EffectSendPile.cs` | None=0, Club=5, Board=10, Hand=20, Deck=30, Discard=40, Secret=50, Equipped=60, Attached=70, Temp=90, PlayerAbility=100 |

### 1-3. 외부 enum

| Enum | 정의 위치 | 값 |
| --- | --- | --- |
| `BossGaugeType` | `BossState.cs` | Skill=0, Atg=10, Groggy=20 |
| `CardType` | `CardData.cs` | None=0, Hero=5, Club=7, Student=10, NonStudent=11, Spell=20, Place=30, Secret=40, Equipment=50, Attachment=60, PlayerAbility=70 |
| `WeaponType` | `CardData.cs` | NONE=0, FRONT=1, MIDDLE=2, BACK=3 |
| `StatusType` | `StatusData.cs` | None=0, AddAttack=4, AddHP=5, AddManaCost=6, StoreValue=7, Stealth=10, Invincibility=12, Shell=13, Protection=14, Protected=15, Armor=16, SpellImmunity=18, Deathtouch=20, Fury=22, Intimidate=23, Flying=24, Trample=26, LifeSteal=28, Silenced=30, Paralysed=32, Poisoned=34, Sleep=36, MassShooting=41, Evasion=42 |
| `AbilityTrigger` | `AbilityData.cs` | None=0, Ongoing=2, Activate=5, OnPlay=10, OnPlayOther=11, OnUse=13, OnUseOther=14, StartOfTurn=20, EndOfTurn=22, OnBeforeAttack=30, OnBeforeAttackOther=31, OnAfterAttack=32, OnAfterAttackOther=33, OnBeforeDefend=34, OnBeforeDefendOther=35, OnAfterDefend=36, OnAfterDefendOther=37, OnAfterDamage=38, OnKill=39, OnDeath=40, OnDeathOther=42, OnDraw=45, OnHeal=50, OnHealOther=51 |
| `AbilityTarget` | `AbilityData.cs` | None=0, Self=1, PlayerSelf=4, PlayerOpponent=5, AllPlayers=7, AllCardsBoard=10, AllCardsHand=11, AllCardsAllPiles=12, AllSlots=15, AllCardData=17, PlayTarget=20, AbilityTriggerer=25, EquippedCard=27, AttachedSlot=28, SelectTarget=30, CardSelector=40, ChoiceSelector=50 |

### 1-4. 참조 ScriptableObject 타입

| 타입 | 정의 위치 | 필드 전체 |
| --- | --- | --- |
| `CardData` | `CardData.cs` | string id; string title; Sprite art_full; Sprite art_board; CardType type; int mana; int attack; int hp; ClubData[] clubs; WeaponData weapon; TraitData[] traits; TraitStat[] stats; AbilityData[] abilities; string text; string desc; GameObject spawn_fx; GameObject death_fx; GameObject attack_fx; GameObject damage_fx; GameObject idle_fx; AudioClip spawn_audio; AudioClip death_audio; AudioClip attack_audio; AudioClip damage_audio; bool deckbuilding=false; int cost=100; PackData[] packs |
| `AbilityData` | `AbilityData.cs` | string id; AbilityTrigger trigger; ConditionData[] conditions_trigger; RepeatConditionData condition_repeat; AbilityTarget criteria_target; ConditionData[] conditions_criteria_target; ConditionWideAreaRange condition_wide_range; ConditionData[] condition_target; FilterData[] filters_target; EffectData[] effects; StatusData[] status; int value; int duration; bool can_cancel=false; AbilityData[] chain_abilities; int mana_cost; bool exhaust; GameObject board_fx; GameObject caster_fx; GameObject target_fx; AudioClip cast_audio; AudioClip target_audio; bool charge_target; string title; string desc; string selector_desc |
| `ClubData` | `ClubData.cs` | string id; string title; AcademyData academy; Sprite icon |
| `TraitData` | `TraitData.cs` | string id; string title; Sprite icon |
| `StatusData` | `StatusData.cs` | StatusType effect; string title; Sprite icon; bool bad_status; string desc; GameObject status_fx; int hvalue |
| `WeaponData` | `WeaponData.cs` | (NonSerialized만 존재 — 서브클래스에서 확장: id, type:WeaponType, range:int, weapon_color:Color32) |
| `VariantData` | `VariantData.cs` | string id; string title; Sprite frame; Sprite frame_board; Color color; int cost_factor=1; bool is_default |
| `Direction` (struct) | `ConditionWideAreaRange.cs` | int dx; int dy |
| `WeightedCard` (struct) | `EffectCreateCard.cs` | CardData card; float weight |

`AbilityData.value` 는 거의 모든 Effect 가 참조하는 공용 수치(데미지/회복/스탯 증감 등) 라는 점에 유의.

---

## 2. Conditions (총 34 종)
### ConditionBossGauge

- **메뉴**: `TcgEngine/Condition/BossGauge`
- **요약**: 보스전(Total Assault) 게이지 비교. compare_to_max=true면 max 기준 비교

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `gauge` | BossGaugeType |  |
| `oper` | ConditionOperatorInt | GreaterEqual |
| `value` | int |  |
| `compare_to_max` | bool |  |

### ConditionTriggered

- **메뉴**: `TcgEngine/Condition/Triggered`
- **요약**: 다른 Ability의 트리거 카드인지 검사

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `is_oper` | ConditionOperatorBool |  |

### ConditionLastTypeRange

- **메뉴**: `TcgEngine/Condition/LastTypeRange`
- **요약**: 마지막 유형 타일/카드에서 range 안에 있는지. range=0이면 동일타입 검사

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `type` | ConditionLastType |  |
| `range` | int |  |
| `oper` | ConditionOperatorBool |  |

### ConditionSlotUnitEmpty

- **메뉴**: `TcgEngine/Condition/SlotUnitEmpty`
- **요약**: 슬롯의 유닛 비었는지

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `oper` | ConditionOperatorBool |  |

### ConditionOwner

- **메뉴**: `TcgEngine/Condition/CardOwner`
- **요약**: 타겟 소유자 = 시전자 소유자

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `oper` | ConditionOperatorBool |  |

### ConditionLastTypeExist

- **메뉴**: `TcgEngine/Condition/LastTypeExist`
- **요약**: LastSelected 존재 여부 등 (Trigger 시점 검사)

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `type` | ConditionLastType |  |
| `oper` | ConditionOperatorBool |  |

### ConditionCount

- **메뉴**: `TcgEngine/Condition/Count`
- **요약**: 특정 파일의 카드 개수 카운트. 리스트는 OR 매칭, 비어있으면 통과

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `target` | ConditionPlayerType |  |
| `pile` | PileType |  |
| `oper` | ConditionOperatorInt |  |
| `value` | int |  |
| `has_type` | List<CardType> |  |
| `has_club` | List<ClubData> |  |
| `has_trait` | List<TraitData> |  |
| `has_card` | List<CardData> |  |

### ConditionCardType

- **메뉴**: `TcgEngine/Condition/CardType`
- **요약**: 카드 타입/클럽/특성 검사

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `has_type` | List<CardType> |  |
| `has_club` | List<ClubData> |  |
| `has_trait` | List<TraitData> |  |
| `oper` | ConditionOperatorBool |  |

### ConditionCanPlace

- **메뉴**: `TcgEngine/Condition/CanPlace`
- **요약**: 슬롯 배치 가능 여부

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `last_type` | ConditionLastType |  |
| `place_card` | CardData |  |
| `card_owner` | ConditionPlayerType |  |
| `oper` | ConditionOperatorBool |  |

### ConditionSlotLocate

- **메뉴**: `TcgEngine/Condition/SlotLocate`
- **요약**: 슬롯 위치 영역 (내부/외부/중립)

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `Inside` | bool |  |
| `Outside` | bool |  |
| `Neutral` | bool |  |

### ConditionWideAreaRange

- **메뉴**: `TcgEngine/Condition/WideAreaRange`
- **요약**: 광역 범위 정의. 필드 오타: thumnail

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `directions` | List<Direction> |  |
| `thumnail` | Sprite |  |

### ConditionSlotNeighbor

- **메뉴**: `TcgEngine/Condition/SlotNeighbor`
- **요약**: 시전자 슬롯 기준 인접 검사

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `range` | int | 1 |

### ConditionNone

- **메뉴**: `TcgEngine/Condition/None`
- **요약**: 항상 true
- **파라미터**: 없음

### ConditionCardData

- **메뉴**: `TcgEngine/Condition/CardData`
- **요약**: 카드 ID 매칭 (Any)

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `card_types` | List<CardData> |  |
| `oper` | ConditionOperatorBool |  |

### ConditionPossibility

- **메뉴**: `TcgEngine/Condition/Possibility`
- **요약**: 0~1 확률 검사

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `possibility` | float |  |

### ConditionTarget

- **메뉴**: `TcgEngine/Condition/Player`
- **요약**: 타겟 카테고리(Card/Player/Slot) 검사

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `type` | ConditionTargetType |  |
| `oper` | ConditionOperatorBool |  |

### ConditionSlotRange

- **메뉴**: `TcgEngine/Condition/SlotRange`
- **요약**: caster.GetRange() 기준 사정거리 안인지

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `oper` | ConditionOperatorBool |  |

### ConditionCardPile

- **메뉴**: `TcgEngine/Condition/CardPile`
- **요약**: 카드가 특정 파일에 있는지

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `type` | PileType |  |
| `oper` | ConditionOperatorBool |  |

### ConditionDamaged

- **메뉴**: `TcgEngine/Condition/Damaged`
- **요약**: 데미지 입은 상태인지

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `oper` | ConditionOperatorBool |  |

### ConditionDeckbuilding

- **메뉴**: `TcgEngine/Condition/CardDeckbuilding`
- **요약**: 덱빌딩 가능 카드인지

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `oper` | ConditionOperatorBool |  |

### ConditionEquipped

- **메뉴**: `TcgEngine/Condition/CardEquipped`
- **요약**: 장비 장착 여부

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `oper` | ConditionOperatorBool |  |

### ConditionExhaust

- **메뉴**: `TcgEngine/Condition/CardExhausted`
- **요약**: 소진 상태

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `oper` | ConditionOperatorBool |  |

### ConditionOnce

- **메뉴**: `TcgEngine/Condition/OncePerTurn`
- **요약**: 턴당 1회 실행 제한
- **파라미터**: 없음

### ConditionOwnerAI

- **메뉴**: `TcgEngine/Condition/CardOwnerAI`
- **요약**: AI만 검사 (인간 플레이어는 항상 통과)

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `oper` | ConditionOperatorBool |  |

### ConditionPlayerStat

- **메뉴**: `TcgEngine/Condition/PlayerStat`
- **요약**: 플레이어 HP/Mana 비교 (Attack 값은 무시)

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `type` | ConditionStatType |  |
| `oper` | ConditionOperatorInt |  |
| `value` | int |  |

### ConditionRolled

- **메뉴**: `TcgEngine/Condition/RolledValue`
- **요약**: 주사위 결과 비교

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `oper` | ConditionOperatorInt |  |
| `value` | int |  |

### ConditionSelf

- **메뉴**: `TcgEngine/Condition/CardSelf`
- **요약**: 타겟 = 시전자

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `oper` | ConditionOperatorBool |  |

### ConditionSlotAttachmentEmpty

- **메뉴**: `TcgEngine/Condition/SlotAttachmentEmpty`
- **요약**: 슬롯 부착 카드 없음

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `oper` | ConditionOperatorBool |  |

### ConditionSlotDist

- **메뉴**: `TcgEngine/Condition/SlotDist`
- **요약**: 시전자와 타겟 슬롯 이동거리 검사

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `distance` | int | 1 |
| `diagonals` | bool |  |

### ConditionSlotPid

- **메뉴**: `TcgEngine/Condition/SlotPid`
- **요약**: 슬롯 소유 진영 (player/opponent/neutral)

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `player` | bool | true |
| `opponent` | bool | true |
| `neutral` | bool | true |

### ConditionStat

- **메뉴**: `TcgEngine/Condition/Stat`
- **요약**: 카드/플레이어 스탯 비교 (Attack/HP/Mana)

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `type` | ConditionStatType |  |
| `oper` | ConditionOperatorInt |  |
| `value` | int |  |

### ConditionStatCustom

- **메뉴**: `TcgEngine/Condition/StatCustom`
- **요약**: 커스텀 스탯(특성 값) 비교

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `trait` | TraitData |  |
| `oper` | ConditionOperatorInt |  |
| `value` | int |  |

### ConditionStatus

- **메뉴**: `TcgEngine/Condition/CardStatus`
- **요약**: 상태이상 보유 여부. value는 임계치(이상 검사)

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `has_status` | StatusType |  |
| `value` | int |  |
| `oper` | ConditionOperatorBool |  |

### ConditionTurn

- **메뉴**: `TcgEngine/Condition/Turn`
- **요약**: 자기 턴인지

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `oper` | ConditionOperatorBool |  |


---

## 3. Effects (총 42 종)
### EffectAddStat

- **메뉴**: `TcgEngine/Effect/AddStat`
- **요약**: ability.value 적용. use_stored_value=true면 StoreValue 상태 누적

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `type` | EffectStatType |  |
| `use_stored_value` | bool |  |

### EffectBossGauge

- **메뉴**: `TcgEngine/Effect/BossGauge`
- **요약**: set_to_value=true면 set_value로 설정, 아니면 delta 가산

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `gauge` | BossGaugeType |  |
| `set_to_value` | bool |  |
| `delta` | int |  |
| `set_value` | int |  |

### EffectSetClubCardUI

- **메뉴**: `TcgEngine/Effect/SetClubCardUI`
- **요약**: 동아리 카드의 인원수 UI 표시용
- **파라미터**: 없음

### EffectDestroy

- **메뉴**: `TcgEngine/Effect/Destroy`
- **요약**: 보드면 Kill, 그 외 Discard
- **파라미터**: 없음

### EffectDamageCount

- **메뉴**: `TcgEngine/Effect/DamageCount`
- **요약**: 카운트 × ability.value 데미지. 카운트 필터는 단일 값 (List 아님)

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `bonus_damage` | TraitData |  |
| `damage_type` | EffectDamageType |  |
| `pile` | PileType |  |
| `player` | EffectPlayerType |  |
| `has_type` | CardType |  |
| `has_club` | ClubData |  |
| `has_trait` | TraitData |  |

### EffectDamage

- **메뉴**: `TcgEngine/Effect/Damage`
- **요약**: ability.value 기반. value_type으로 ATK/HP 치환 가능

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `damage_type` | EffectDamageType |  |
| `value_type` | EffectValueType |  |
| `bonus_damage` | TraitData |  |

### EffectMoveUnit

- **메뉴**: `TcgEngine/Effect/MoveUnit`
- **요약**: 지정 유닛을 target 슬롯으로 이동. 파일명 EffectMove.cs

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `target_type` | EffectActionType |  |

### EffectHeal

- **메뉴**: `TcgEngine/Effect/Heal`
- **요약**: 회복. heal_type으로 ATK/HP 치환 가능

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `heal_type` | EffectValueType |  |
| `bonus_heal` | TraitData |  |

### EffectChangeWeapon

- **메뉴**: `TcgEngine/Effect/ChangeWeapon`
- **요약**: 무기 교체

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `weapon` | WeaponData |  |

### EffectAttack

- **메뉴**: `TcgEngine/Effect/Attack`
- **요약**: 지정 카드가 공격을 수행

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `attacker_type` | EffectActionType |  |

### EffectUseCard

- **메뉴**: `TcgEngine/Effect/UseCard`
- **요약**: 새 카드를 보드/핸드에 소환

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `use` | CardData |  |
| `use_opponent` | bool |  |

### EffectStoreCount

- **메뉴**: `TcgEngine/Effect/StoreCount`
- **요약**: 카운트를 StoreValue 상태로 저장

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `pile` | PileType |  |
| `player` | EffectPlayerType |  |
| `has_type` | CardType |  |
| `has_club` | ClubData |  |
| `has_trait` | TraitData |  |

### EffectSendPile

- **메뉴**: `TcgEngine/Effect/SendPile`
- **요약**: 카드를 특정 파일로 이동

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `pile` | PileType |  |

### EffectExhaust

- **메뉴**: `TcgEngine/Effect/Exhaust`
- **요약**: 소진/소진 해제

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `exhausted` | bool |  |

### EffectCreateCard

- **메뉴**: `TcgEngine/Effect/CreateCard`
- **요약**: WeightedCard={card:CardData, weight:float}

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `create_card` | List<WeightedCard> |  |
| `create_pile` | PileType |  |
| `is_same_possibility` | bool |  |
| `create_opponent` | bool |  |

### EffectCreate

- **메뉴**: `TcgEngine/Effect/Create`
- **요약**: CardData target을 받아 생성

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `create_pile` | PileType |  |
| `create_opponent` | bool |  |

### EffectAddStatTotalCount

- **메뉴**: `TcgEngine/Effect/AddStatTotalCount`
- **요약**: total_type 누적값 기반 스탯 가산

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `stat_type` | EffectStatType |  |
| `total_type` | EffectTotalCountType |  |

### EffectDamageRatio

- **메뉴**: `TcgEngine/Effect/DamageRatio`
- **요약**: 대상 현재 HP × ratio 데미지

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `bonus_damage` | TraitData |  |
| `ratio` | float |  |

### EffectAttackRedirect

- **메뉴**: `TcgEngine/Effect/AttackRedirect`
- **요약**: 공격 대상 변경

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `attacker_type` | EffectActionType |  |

### EffectPlay

- **메뉴**: `TcgEngine/Effect/Play`
- **요약**: 타겟 카드를 무료 발동
- **파라미터**: 없음

### EffectClearStatus

- **메뉴**: `TcgEngine/Effect/ClearStatus`
- **요약**: status=null이면 status_type 기준 일괄 제거

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `status` | StatusData |  |
| `status_type` | EffectStatusType |  |

### EffectAddStatCount

- **메뉴**: `TcgEngine/Effect/AddStatCount`
- **요약**: 카운트 × ability.value 만큼 스탯 가산

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `type` | EffectStatType |  |
| `pile` | PileType |  |
| `player` | EffectPlayerType |  |
| `has_type` | CardType |  |
| `has_club` | ClubData |  |
| `has_trait` | TraitData |  |

### EffectAttachCard

- **메뉴**: `TcgEngine/Effect/AttachCard`
- **요약**: 슬롯에 카드 부착. 파일명 EffectAttach.cs

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `attach` | CardData |  |

### EffectPlayCard

- **메뉴**: `TcgEngine/Effect/PlayCard`
- **요약**: 카드 생성 후 즉시 발동

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `play_card` | CardData |  |

### EffectAddAbility

- **메뉴**: `TcgEngine/Effect/AddAbility`
- **요약**: 어빌리티 부여

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `gain_ability` | AbilityData |  |

### EffectAddClub

- **메뉴**: `TcgEngine/Effect/AddClub`
- **요약**: 클럽 부여 (caster.AddClub(target.card_id))

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `club` | ClubData |  |

### EffectAddStatRoll

- **메뉴**: `TcgEngine/Effect/AddStatRoll`
- **요약**: rolled_value 만큼 스탯 가산

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `type` | EffectStatType |  |

### EffectAddTrait

- **메뉴**: `TcgEngine/Effect/AddTrait`
- **요약**: ability.value 만큼 특성 추가

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `trait` | TraitData |  |

### EffectChangeOwner

- **메뉴**: `TcgEngine/Effect/ChangeOwner`
- **요약**: 카드 소유 변경

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `owner_opponent` | bool |  |

### EffectClearTemp

- **메뉴**: `TcgEngine/Effect/ClearTemp`
- **요약**: cards_temp 비우기
- **파라미터**: 없음

### EffectDestroyEquip

- **메뉴**: `TcgEngine/Effect/DestroyEquip`
- **요약**: 장비 파괴
- **파라미터**: 없음

### EffectDiscard

- **메뉴**: `TcgEngine/Effect/Discard`
- **요약**: ability.value 장 폐기
- **파라미터**: 없음

### EffectDraw

- **메뉴**: `TcgEngine/Effect/Draw`
- **요약**: ability.value 장 드로우
- **파라미터**: 없음

### EffectMana

- **메뉴**: `TcgEngine/Effect/Mana`
- **요약**: ability.value 만큼 마나 변경
- **파라미터**: 없음

### EffectRemoveAbility

- **메뉴**: `TcgEngine/Effect/RemoveAbility`
- **요약**: 어빌리티 제거

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `remove_ability` | AbilityData |  |

### EffectRemoveTrait

- **메뉴**: `TcgEngine/Effect/RemoveTrait`
- **요약**: 특성 제거

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `trait` | TraitData |  |

### EffectResetStat

- **메뉴**: `TcgEngine/Effect/ResetStat`
- **요약**: 원래 스탯으로 복구
- **파라미터**: 없음

### EffectRoll

- **메뉴**: `TcgEngine/Effect/RollDice`
- **요약**: 주사위 굴림. 클래스명 EffectRoll

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `dice` | int | 6 |

### EffectSetStat

- **메뉴**: `TcgEngine/Effect/SetStat`
- **요약**: ability.value로 스탯 세팅

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `type` | EffectStatType |  |

### EffectSetStatCustom

- **메뉴**: `TcgEngine/Effect/SetStatCustom`
- **요약**: ability.value로 특성값 세팅

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `trait` | TraitData |  |

### EffectShuffle

- **메뉴**: `TcgEngine/Effect/Shuffle`
- **요약**: 덱 셔플
- **파라미터**: 없음

### EffectTransform

- **메뉴**: `TcgEngine/Effect/Transform`
- **요약**: 다른 카드로 변신

| 파라미터 | 타입 | 기본값 |
| --- | --- | --- |
| `transform_to` | CardData |  |


---

## 4. 테이블 설계 시 주의점

1. **공용 수치는 `AbilityData.value`** — 다수 Effect 가 자체 value 필드 없이 부모 Ability 의 `value` 를 그대로 쓴다 (Damage, Heal, Draw, Discard, Mana, AddStat 계열, SetStat 등). value 와 duration 둘 다 Ability 레벨에 존재.
2. **카운트 계열 4종은 동일 패턴** — `ConditionCount`, `EffectDamageCount`, `EffectStoreCount`, `EffectAddStatCount` 모두 `pile / player(또는 target) / has_type / has_club / has_trait` 묶음을 반복. 테이블 설계 시 공통 컬럼 묶음으로 처리 권장.
3. **리스트 vs 단일 불일치** — `ConditionCount`, `ConditionCardType` 의 has_type / has_club / has_trait 는 List 지만, `EffectDamageCount` / `EffectStoreCount` / `EffectAddStatCount` 의 동일 필드는 단일 값. 통일 여부 검토 필요.
4. **Bool oper vs Int oper** — 다수 클래스가 둘 중 하나의 `oper` 필드를 가짐. CSV / MD 에 타입이 명시되어 있음.
5. **필드 오타** — `ConditionWideAreaRange.thumnail` (정상은 thumbnail). 마이그레이션 시 정정 고려.
6. **파일명 ≠ 클래스명** — `EffectMove.cs → EffectMoveUnit`, `EffectAttach.cs → EffectAttachCard`. `EffectRoll` 클래스의 메뉴 경로는 `TcgEngine/Effect/RollDice`.
7. **AbilityData 본체 파라미터도 같이 설계해야 함** — Condition / Effect 는 AbilityData 의 trigger / criteria_target / value / duration / status / mana_cost / exhaust 등과 짝을 이룬다.
8. **`SlotLocate.Inside/Outside/Neutral` vs `SlotPid.player/opponent/neutral`** — 대소문자만 다른 동일 이름 필드. DB 컬럼명 충돌 주의.
