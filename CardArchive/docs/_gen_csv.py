"""Generate wide-format Conditions.csv / Effects.csv and per-class MD reference.

Each row = one ScriptableObject subclass. Each column = one parameter.
Cell value = field type (with default if non-trivial); empty = field not used.
Output is UTF-8 with BOM so Excel renders Korean correctly.

Also (re)generates ConditionEffectParameters.md with per-class subsections.
"""

import codecs
import os

OUT_DIR = os.path.dirname(os.path.abspath(__file__))

# ----- Conditions -----

CONDITION_COLS = [
    'Class', 'MenuPath',
    'oper', 'is_oper', 'value', 'type',
    'range', 'distance', 'pile', 'target',
    'has_type', 'has_club', 'has_trait', 'has_card', 'card_types',
    'last_type', 'place_card', 'card_owner',
    'compare_to_max', 'gauge',
    'Inside', 'Outside', 'Neutral',
    'directions', 'thumnail',
    'possibility', 'diagonals',
    'player', 'opponent', 'neutral',
    'trait', 'has_status',
]

# (Class, MenuPath, {field: type_string}, description, ordered_field_list)
CONDITIONS = [
    ('ConditionBossGauge',           'TcgEngine/Condition/BossGauge',           {'gauge':'BossGaugeType', 'oper':'ConditionOperatorInt(GreaterEqual)', 'value':'int', 'compare_to_max':'bool'},
        '보스전(Total Assault) 게이지 비교. compare_to_max=true면 max 기준 비교',
        ['gauge','oper','value','compare_to_max']),
    ('ConditionTriggered',           'TcgEngine/Condition/Triggered',           {'is_oper':'ConditionOperatorBool'},
        '다른 Ability의 트리거 카드인지 검사',
        ['is_oper']),
    ('ConditionLastTypeRange',       'TcgEngine/Condition/LastTypeRange',       {'type':'ConditionLastType', 'range':'int', 'oper':'ConditionOperatorBool'},
        '마지막 유형 타일/카드에서 range 안에 있는지. range=0이면 동일타입 검사',
        ['type','range','oper']),
    ('ConditionSlotUnitEmpty',       'TcgEngine/Condition/SlotUnitEmpty',       {'oper':'ConditionOperatorBool'},
        '슬롯의 유닛 비었는지',
        ['oper']),
    ('ConditionOwner',               'TcgEngine/Condition/CardOwner',           {'oper':'ConditionOperatorBool'},
        '타겟 소유자 = 시전자 소유자',
        ['oper']),
    ('ConditionLastTypeExist',       'TcgEngine/Condition/LastTypeExist',       {'type':'ConditionLastType', 'oper':'ConditionOperatorBool'},
        'LastSelected 존재 여부 등 (Trigger 시점 검사)',
        ['type','oper']),
    ('ConditionCount',               'TcgEngine/Condition/Count',               {'target':'ConditionPlayerType', 'pile':'PileType', 'oper':'ConditionOperatorInt', 'value':'int', 'has_type':'List<CardType>', 'has_club':'List<ClubData>', 'has_trait':'List<TraitData>', 'has_card':'List<CardData>'},
        '특정 파일의 카드 개수 카운트. 리스트는 OR 매칭, 비어있으면 통과',
        ['target','pile','oper','value','has_type','has_club','has_trait','has_card']),
    ('ConditionCardType',            'TcgEngine/Condition/CardType',            {'has_type':'List<CardType>', 'has_club':'List<ClubData>', 'has_trait':'List<TraitData>', 'oper':'ConditionOperatorBool'},
        '카드 타입/클럽/특성 검사',
        ['has_type','has_club','has_trait','oper']),
    ('ConditionCanPlace',            'TcgEngine/Condition/CanPlace',            {'last_type':'ConditionLastType', 'place_card':'CardData', 'card_owner':'ConditionPlayerType', 'oper':'ConditionOperatorBool'},
        '슬롯 배치 가능 여부',
        ['last_type','place_card','card_owner','oper']),
    ('ConditionSlotLocate',          'TcgEngine/Condition/SlotLocate',          {'Inside':'bool', 'Outside':'bool', 'Neutral':'bool'},
        '슬롯 위치 영역 (내부/외부/중립)',
        ['Inside','Outside','Neutral']),
    ('ConditionWideAreaRange',       'TcgEngine/Condition/WideAreaRange',       {'directions':'List<Direction>', 'thumnail':'Sprite'},
        '광역 범위 정의. 필드 오타: thumnail',
        ['directions','thumnail']),
    ('ConditionSlotNeighbor',        'TcgEngine/Condition/SlotNeighbor',        {'range':'int=1'},
        '시전자 슬롯 기준 인접 검사',
        ['range']),
    ('ConditionNone',                'TcgEngine/Condition/None',                {},
        '항상 true',
        []),
    ('ConditionCardData',            'TcgEngine/Condition/CardData',            {'card_types':'List<CardData>', 'oper':'ConditionOperatorBool'},
        '카드 ID 매칭 (Any)',
        ['card_types','oper']),
    ('ConditionPossibility',         'TcgEngine/Condition/Possibility',         {'possibility':'float'},
        '0~1 확률 검사',
        ['possibility']),
    ('ConditionTarget',              'TcgEngine/Condition/Player',              {'type':'ConditionTargetType', 'oper':'ConditionOperatorBool'},
        '타겟 카테고리(Card/Player/Slot) 검사',
        ['type','oper']),
    ('ConditionSlotRange',           'TcgEngine/Condition/SlotRange',           {'oper':'ConditionOperatorBool'},
        'caster.GetRange() 기준 사정거리 안인지',
        ['oper']),
    ('ConditionCardPile',            'TcgEngine/Condition/CardPile',            {'type':'PileType', 'oper':'ConditionOperatorBool'},
        '카드가 특정 파일에 있는지',
        ['type','oper']),
    ('ConditionDamaged',             'TcgEngine/Condition/Damaged',             {'oper':'ConditionOperatorBool'},
        '데미지 입은 상태인지',
        ['oper']),
    ('ConditionDeckbuilding',        'TcgEngine/Condition/CardDeckbuilding',    {'oper':'ConditionOperatorBool'},
        '덱빌딩 가능 카드인지',
        ['oper']),
    ('ConditionEquipped',            'TcgEngine/Condition/CardEquipped',        {'oper':'ConditionOperatorBool'},
        '장비 장착 여부',
        ['oper']),
    ('ConditionExhaust',             'TcgEngine/Condition/CardExhausted',       {'oper':'ConditionOperatorBool'},
        '소진 상태',
        ['oper']),
    ('ConditionOnce',                'TcgEngine/Condition/OncePerTurn',         {},
        '턴당 1회 실행 제한',
        []),
    ('ConditionOwnerAI',             'TcgEngine/Condition/CardOwnerAI',         {'oper':'ConditionOperatorBool'},
        'AI만 검사 (인간 플레이어는 항상 통과)',
        ['oper']),
    ('ConditionPlayerStat',          'TcgEngine/Condition/PlayerStat',          {'type':'ConditionStatType', 'oper':'ConditionOperatorInt', 'value':'int'},
        '플레이어 HP/Mana 비교 (Attack 값은 무시)',
        ['type','oper','value']),
    ('ConditionRolled',              'TcgEngine/Condition/RolledValue',         {'oper':'ConditionOperatorInt', 'value':'int'},
        '주사위 결과 비교',
        ['oper','value']),
    ('ConditionSelf',                'TcgEngine/Condition/CardSelf',            {'oper':'ConditionOperatorBool'},
        '타겟 = 시전자',
        ['oper']),
    ('ConditionSlotAttachmentEmpty', 'TcgEngine/Condition/SlotAttachmentEmpty', {'oper':'ConditionOperatorBool'},
        '슬롯 부착 카드 없음',
        ['oper']),
    ('ConditionSlotDist',            'TcgEngine/Condition/SlotDist',            {'distance':'int=1', 'diagonals':'bool'},
        '시전자와 타겟 슬롯 이동거리 검사',
        ['distance','diagonals']),
    ('ConditionSlotPid',             'TcgEngine/Condition/SlotPid',             {'player':'bool=true', 'opponent':'bool=true', 'neutral':'bool=true'},
        '슬롯 소유 진영 (player/opponent/neutral)',
        ['player','opponent','neutral']),
    ('ConditionStat',                'TcgEngine/Condition/Stat',                {'type':'ConditionStatType', 'oper':'ConditionOperatorInt', 'value':'int'},
        '카드/플레이어 스탯 비교 (Attack/HP/Mana)',
        ['type','oper','value']),
    ('ConditionStatCustom',          'TcgEngine/Condition/StatCustom',          {'trait':'TraitData', 'oper':'ConditionOperatorInt', 'value':'int'},
        '커스텀 스탯(특성 값) 비교',
        ['trait','oper','value']),
    ('ConditionStatus',              'TcgEngine/Condition/CardStatus',          {'has_status':'StatusType', 'value':'int', 'oper':'ConditionOperatorBool'},
        '상태이상 보유 여부. value는 임계치(이상 검사)',
        ['has_status','value','oper']),
    ('ConditionTurn',                'TcgEngine/Condition/Turn',                {'oper':'ConditionOperatorBool'},
        '자기 턴인지',
        ['oper']),
]

# ----- Effects -----

EFFECT_COLS = [
    'Class', 'MenuPath',
    'type', 'stat_type', 'use_stored_value',
    'gauge', 'set_to_value', 'delta', 'set_value',
    'bonus_damage', 'damage_type', 'value_type',
    'pile', 'player',
    'has_type', 'has_club', 'has_trait',
    'target_type', 'attacker_type',
    'heal_type', 'bonus_heal',
    'weapon',
    'use', 'use_opponent',
    'exhausted',
    'create_card', 'create_pile', 'is_same_possibility', 'create_opponent',
    'total_type',
    'ratio',
    'status', 'status_type',
    'attach', 'play_card',
    'gain_ability', 'remove_ability',
    'club', 'trait',
    'owner_opponent',
    'dice',
    'transform_to',
]

EFFECTS = [
    ('EffectAddStat',            'TcgEngine/Effect/AddStat',            {'type':'EffectStatType', 'use_stored_value':'bool'},
        'ability.value 적용. use_stored_value=true면 StoreValue 상태 누적',
        ['type','use_stored_value']),
    ('EffectBossGauge',          'TcgEngine/Effect/BossGauge',          {'gauge':'BossGaugeType', 'set_to_value':'bool', 'delta':'int', 'set_value':'int'},
        'set_to_value=true면 set_value로 설정, 아니면 delta 가산',
        ['gauge','set_to_value','delta','set_value']),
    ('EffectSetClubCardUI',      'TcgEngine/Effect/SetClubCardUI',      {},
        '동아리 카드의 인원수 UI 표시용',
        []),
    ('EffectDestroy',            'TcgEngine/Effect/Destroy',            {},
        '보드면 Kill, 그 외 Discard',
        []),
    ('EffectDamageCount',        'TcgEngine/Effect/DamageCount',        {'bonus_damage':'TraitData', 'damage_type':'EffectDamageType', 'pile':'PileType', 'player':'EffectPlayerType', 'has_type':'CardType', 'has_club':'ClubData', 'has_trait':'TraitData'},
        '카운트 × ability.value 데미지. 카운트 필터는 단일 값 (List 아님)',
        ['bonus_damage','damage_type','pile','player','has_type','has_club','has_trait']),
    ('EffectDamage',             'TcgEngine/Effect/Damage',             {'damage_type':'EffectDamageType', 'value_type':'EffectValueType', 'bonus_damage':'TraitData'},
        'ability.value 기반. value_type으로 ATK/HP 치환 가능',
        ['damage_type','value_type','bonus_damage']),
    ('EffectMoveUnit',           'TcgEngine/Effect/MoveUnit',           {'target_type':'EffectActionType'},
        '지정 유닛을 target 슬롯으로 이동. 파일명 EffectMove.cs',
        ['target_type']),
    ('EffectHeal',               'TcgEngine/Effect/Heal',               {'heal_type':'EffectValueType', 'bonus_heal':'TraitData'},
        '회복. heal_type으로 ATK/HP 치환 가능',
        ['heal_type','bonus_heal']),
    ('EffectChangeWeapon',       'TcgEngine/Effect/ChangeWeapon',       {'weapon':'WeaponData'},
        '무기 교체',
        ['weapon']),
    ('EffectAttack',             'TcgEngine/Effect/Attack',             {'attacker_type':'EffectActionType'},
        '지정 카드가 공격을 수행',
        ['attacker_type']),
    ('EffectUseCard',            'TcgEngine/Effect/UseCard',            {'use':'CardData', 'use_opponent':'bool'},
        '새 카드를 보드/핸드에 소환',
        ['use','use_opponent']),
    ('EffectStoreCount',         'TcgEngine/Effect/StoreCount',         {'pile':'PileType', 'player':'EffectPlayerType', 'has_type':'CardType', 'has_club':'ClubData', 'has_trait':'TraitData'},
        '카운트를 StoreValue 상태로 저장',
        ['pile','player','has_type','has_club','has_trait']),
    ('EffectSendPile',           'TcgEngine/Effect/SendPile',           {'pile':'PileType'},
        '카드를 특정 파일로 이동',
        ['pile']),
    ('EffectExhaust',            'TcgEngine/Effect/Exhaust',            {'exhausted':'bool'},
        '소진/소진 해제',
        ['exhausted']),
    ('EffectCreateCard',         'TcgEngine/Effect/CreateCard',         {'create_card':'List<WeightedCard>', 'create_pile':'PileType', 'is_same_possibility':'bool', 'create_opponent':'bool'},
        'WeightedCard={card:CardData, weight:float}',
        ['create_card','create_pile','is_same_possibility','create_opponent']),
    ('EffectCreate',             'TcgEngine/Effect/Create',             {'create_pile':'PileType', 'create_opponent':'bool'},
        'CardData target을 받아 생성',
        ['create_pile','create_opponent']),
    ('EffectAddStatTotalCount',  'TcgEngine/Effect/AddStatTotalCount',  {'stat_type':'EffectStatType', 'total_type':'EffectTotalCountType'},
        'total_type 누적값 기반 스탯 가산',
        ['stat_type','total_type']),
    ('EffectDamageRatio',        'TcgEngine/Effect/DamageRatio',        {'bonus_damage':'TraitData', 'ratio':'float'},
        '대상 현재 HP × ratio 데미지',
        ['bonus_damage','ratio']),
    ('EffectAttackRedirect',     'TcgEngine/Effect/AttackRedirect',     {'attacker_type':'EffectActionType'},
        '공격 대상 변경',
        ['attacker_type']),
    ('EffectPlay',               'TcgEngine/Effect/Play',               {},
        '타겟 카드를 무료 발동',
        []),
    ('EffectClearStatus',        'TcgEngine/Effect/ClearStatus',        {'status':'StatusData', 'status_type':'EffectStatusType'},
        'status=null이면 status_type 기준 일괄 제거',
        ['status','status_type']),
    ('EffectAddStatCount',       'TcgEngine/Effect/AddStatCount',       {'type':'EffectStatType', 'pile':'PileType', 'player':'EffectPlayerType', 'has_type':'CardType', 'has_club':'ClubData', 'has_trait':'TraitData'},
        '카운트 × ability.value 만큼 스탯 가산',
        ['type','pile','player','has_type','has_club','has_trait']),
    ('EffectAttachCard',         'TcgEngine/Effect/AttachCard',         {'attach':'CardData'},
        '슬롯에 카드 부착. 파일명 EffectAttach.cs',
        ['attach']),
    ('EffectPlayCard',           'TcgEngine/Effect/PlayCard',           {'play_card':'CardData'},
        '카드 생성 후 즉시 발동',
        ['play_card']),
    ('EffectAddAbility',         'TcgEngine/Effect/AddAbility',         {'gain_ability':'AbilityData'},
        '어빌리티 부여',
        ['gain_ability']),
    ('EffectAddClub',            'TcgEngine/Effect/AddClub',            {'club':'ClubData'},
        '클럽 부여 (caster.AddClub(target.card_id))',
        ['club']),
    ('EffectAddStatRoll',        'TcgEngine/Effect/AddStatRoll',        {'type':'EffectStatType'},
        'rolled_value 만큼 스탯 가산',
        ['type']),
    ('EffectAddTrait',           'TcgEngine/Effect/AddTrait',           {'trait':'TraitData'},
        'ability.value 만큼 특성 추가',
        ['trait']),
    ('EffectChangeOwner',        'TcgEngine/Effect/ChangeOwner',        {'owner_opponent':'bool'},
        '카드 소유 변경',
        ['owner_opponent']),
    ('EffectClearTemp',          'TcgEngine/Effect/ClearTemp',          {},
        'cards_temp 비우기',
        []),
    ('EffectDestroyEquip',       'TcgEngine/Effect/DestroyEquip',       {},
        '장비 파괴',
        []),
    ('EffectDiscard',            'TcgEngine/Effect/Discard',            {},
        'ability.value 장 폐기',
        []),
    ('EffectDraw',               'TcgEngine/Effect/Draw',               {},
        'ability.value 장 드로우',
        []),
    ('EffectMana',               'TcgEngine/Effect/Mana',               {},
        'ability.value 만큼 마나 변경',
        []),
    ('EffectRemoveAbility',      'TcgEngine/Effect/RemoveAbility',      {'remove_ability':'AbilityData'},
        '어빌리티 제거',
        ['remove_ability']),
    ('EffectRemoveTrait',        'TcgEngine/Effect/RemoveTrait',        {'trait':'TraitData'},
        '특성 제거',
        ['trait']),
    ('EffectResetStat',          'TcgEngine/Effect/ResetStat',          {},
        '원래 스탯으로 복구',
        []),
    ('EffectRoll',               'TcgEngine/Effect/RollDice',           {'dice':'int=6'},
        '주사위 굴림. 클래스명 EffectRoll',
        ['dice']),
    ('EffectSetStat',            'TcgEngine/Effect/SetStat',            {'type':'EffectStatType'},
        'ability.value로 스탯 세팅',
        ['type']),
    ('EffectSetStatCustom',      'TcgEngine/Effect/SetStatCustom',      {'trait':'TraitData'},
        'ability.value로 특성값 세팅',
        ['trait']),
    ('EffectShuffle',            'TcgEngine/Effect/Shuffle',            {},
        '덱 셔플',
        []),
    ('EffectTransform',          'TcgEngine/Effect/Transform',          {'transform_to':'CardData'},
        '다른 카드로 변신',
        ['transform_to']),
]


def make_row(name, menu, fields, cols):
    row = [''] * len(cols)
    row[0] = name
    row[1] = menu
    for k, v in fields.items():
        assert k in cols, f"unknown col {k} for {name}"
        row[cols.index(k)] = v
    return ','.join(row)


def write_csv(path, cols, rows_data):
    lines = [','.join(cols)]
    for tup in rows_data:
        name, menu, fields = tup[0], tup[1], tup[2]
        lines.append(make_row(name, menu, fields, cols))
    body = '\r\n'.join(lines) + '\r\n'
    with open(path, 'wb') as f:
        f.write(codecs.BOM_UTF8)
        f.write(body.encode('utf-8'))


MD_HEADER = """\
﻿# Condition / Effect Data 파라미터 정리

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
"""

MD_TAIL = """\

## 4. 테이블 설계 시 주의점

1. **공용 수치는 `AbilityData.value`** — 다수 Effect 가 자체 value 필드 없이 부모 Ability 의 `value` 를 그대로 쓴다 (Damage, Heal, Draw, Discard, Mana, AddStat 계열, SetStat 등). value 와 duration 둘 다 Ability 레벨에 존재.
2. **카운트 계열 4종은 동일 패턴** — `ConditionCount`, `EffectDamageCount`, `EffectStoreCount`, `EffectAddStatCount` 모두 `pile / player(또는 target) / has_type / has_club / has_trait` 묶음을 반복. 테이블 설계 시 공통 컬럼 묶음으로 처리 권장.
3. **리스트 vs 단일 불일치** — `ConditionCount`, `ConditionCardType` 의 has_type / has_club / has_trait 는 List 지만, `EffectDamageCount` / `EffectStoreCount` / `EffectAddStatCount` 의 동일 필드는 단일 값. 통일 여부 검토 필요.
4. **Bool oper vs Int oper** — 다수 클래스가 둘 중 하나의 `oper` 필드를 가짐. CSV / MD 에 타입이 명시되어 있음.
5. **필드 오타** — `ConditionWideAreaRange.thumnail` (정상은 thumbnail). 마이그레이션 시 정정 고려.
6. **파일명 ≠ 클래스명** — `EffectMove.cs → EffectMoveUnit`, `EffectAttach.cs → EffectAttachCard`. `EffectRoll` 클래스의 메뉴 경로는 `TcgEngine/Effect/RollDice`.
7. **AbilityData 본체 파라미터도 같이 설계해야 함** — Condition / Effect 는 AbilityData 의 trigger / criteria_target / value / duration / status / mana_cost / exhaust 등과 짝을 이룬다.
8. **`SlotLocate.Inside/Outside/Neutral` vs `SlotPid.player/opponent/neutral`** — 대소문자만 다른 동일 이름 필드. DB 컬럼명 충돌 주의.
"""


def md_class_section(item, defaults_map):
    name, menu, fields, desc, ordered = item
    out = []
    out.append(f"### {name}\n")
    out.append(f"- **메뉴**: `{menu}`")
    if desc:
        out.append(f"- **요약**: {desc}")
    if not fields:
        out.append("- **파라미터**: 없음\n")
        return '\n'.join(out) + '\n'
    out.append("")
    out.append("| 파라미터 | 타입 | 기본값 |")
    out.append("| --- | --- | --- |")
    for fname in ordered:
        type_str = fields[fname]
        default = ''
        if '(' in type_str and type_str.endswith(')'):
            t, d = type_str.split('(', 1)
            type_str = t
            default = d.rstrip(')')
        elif '=' in type_str:
            t, d = type_str.split('=', 1)
            type_str = t
            default = d
        out.append(f"| `{fname}` | {type_str} | {default} |")
    out.append("")
    return '\n'.join(out) + '\n'


def write_md(path):
    out = [MD_HEADER]
    out.append("\n## 2. Conditions (총 " + str(len(CONDITIONS)) + " 종)\n")
    for item in CONDITIONS:
        out.append(md_class_section(item, None))
    out.append("\n---\n")
    out.append("\n## 3. Effects (총 " + str(len(EFFECTS)) + " 종)\n")
    for item in EFFECTS:
        out.append(md_class_section(item, None))
    out.append("\n---\n")
    out.append(MD_TAIL)
    body = ''.join(out)
    with open(path, 'wb') as f:
        f.write(body.encode('utf-8'))


# Generate
write_csv(os.path.join(OUT_DIR, 'Conditions.csv'), CONDITION_COLS, CONDITIONS)
write_csv(os.path.join(OUT_DIR, 'Effects.csv'),    EFFECT_COLS,    EFFECTS)
write_md(os.path.join(OUT_DIR, 'ConditionEffectParameters.md'))

print(f"Conditions: {len(CONDITIONS)} rows, {len(CONDITION_COLS)} cols")
print(f"Effects:    {len(EFFECTS)} rows, {len(EFFECT_COLS)} cols")
print("MD: ConditionEffectParameters.md regenerated")
