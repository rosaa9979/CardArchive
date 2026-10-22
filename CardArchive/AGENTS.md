# 프로젝트 작업 지침

## 1. 작업 및 Git 관리

- 변경 전 Git 상태와 관련 코드를 확인하고, 사용자가 수정한 내용을 보존한다. 작업 중 파일이 바뀌면 최신 내용을 다시 확인한다.
- 커밋은 작업 목적별로 나눈다. 기능, 데이터 수정, 빌드 설정, 문서, 폰트 갱신 등 무관한 변경을 한 커밋에 섞지 않는다.
- 서로 의존하는 코드·에셋·설정·검증은 함께 커밋하여 각 커밋이 가능한 한 실행 가능한 상태가 되게 한다. 전투 표시 코드와 이를 구동하는 FX 에셋은 같은 커밋으로 묶는다.
- 커밋 전 스테이징된 차이를 확인한다. 전체 추가로 무관한 파일이나 AI 산출물이 들어가지 않게 한다.
- 브랜치를 머지할 때는 `git merge --no-ff <branch>`를 사용해 머지 커밋과 브랜치 통합 기록을 남긴다. 사용자 지시 없이 fast-forward나 squash로 대체하지 않는다.
- 작업 후 수행한 검증, 수행하지 못한 검증, 남은 변경을 보고한다. 커밋과 원격 푸시는 별개로 취급한다.
- 작업을 마치면 미커밋 변경을 남기지 않는다. 작업 결과는 목적별로 커밋하고, 작업 전부터 있던 변경을 포함한 나머지 변경은 파일별 차이를 확인한다. 줄바꿈만 바뀐 파일이나 불필요한 에디터 자동 저장 결과처럼 커밋할 필요가 없는 변경은 원복한다. 실제 수정이 담겨 있어 원복할 수 없는 변경은 보존하고, 남긴 이유와 함께 보고한다.

### 줄바꿈 정리

- `.gitattributes`가 있으면 그 규칙을 우선하고, 없으면 기존 파일의 줄바꿈·인코딩을 유지한다. 작업 편의로 저장소 전체의 줄바꿈이나 Git 전역 설정을 바꾸지 않는다.
- 기능 수정과 줄바꿈·BOM·전체 포맷 변경을 섞지 않는다. 정규화 자체가 필요하면 별도 작업과 커밋으로 분리한다.
- 내용 차이 없이 줄바꿈만 바뀐 파일은 커밋에서 제외하고 원복한다. 반드시 파일별 차이를 확인하고, 사용자의 실제 수정까지 덮어쓰는 일괄 원복은 하지 않는다.
- 커밋 전 공백 오류를 검사한다. Unity 직렬화 파일의 기본 빈 필드 공백을 제거하려고 에셋 전체를 재작성하지 않는다.

## 2. 프로젝트 에셋 관리

- 실제 게임 에셋은 기존 `Assets/` 폴더 구조와 명명 규칙을 따른다. 재사용할 수 있는 공용 에셋·코드를 우선한다.
- Unity 에셋과 대응하는 `.meta`는 함께 추가·이동·삭제하고, 기존 GUID를 보존한다. 에셋 삭제 전 씬·프리팹·ScriptableObject 등의 참조를 확인한다.
- 에디터 자동 저장으로 생긴 대량 직렬화·폰트 아틀라스 변경은 기능 변경과 구분해 검토한다. 필요한 갱신만 별도 커밋하고, 불필요한 변경은 실제 수정이 없는지 확인한 뒤 원복한다.
- 테스트 덱·시작 패·디버그 설정을 정식 게임 규칙 수정과 섞지 않는다. 확인용 설정을 바꿨으면 작업 종료 전에 원복하거나 별도 커밋으로 명시한다.

### 연출 에셋 제작

- 상태를 표시하는 일회성 FX는 공용 `FXPlayback`과 Animator 이벤트 계약을 따른다. 이벤트를 받는 Animator와 `FXPlayback`은 같은 GameObject에 둔다.
- 결과 표시 시점에 `Impact(int resultIndex)` 이벤트를 배치한다. 기본 결과 인덱스는 `0`이며, 피해 수치 등 게임 규칙 값을 클립에 저장하지 않는다.
- 일회성 클립은 비루프이며 `Complete` 이벤트를 정확히 하나 둔다. `Impact`는 `Complete`보다 늦을 수 없고, 같은 시점이면 `Impact` → `Continue` → `Complete` 순서를 유지한다.
- 잔여 연출을 유지한 채 다음 단계 진행을 허용할 때만 선택적으로 `Continue`를 한 번 둔다. `Continue`는 결과를 표시하거나 FX 수명을 종료하는 이벤트가 아니다. 다음 단계 진행에는 해당 단계의 Impact 반영과 진행 게이트 해제가 필요하다.
- 장식용 FX는 `FXSetting.presentation_impact = false`로 설정하고 `Impact`를 넣지 않는다. 결과는 별도 피격 FX 등 명확한 담당 FX의 Impact로 표시한다. `Complete`는 유지한다.
- 파티클 전용 일회성 FX도 이벤트용 Animator 타임라인을 사용한다. `OnAnimationFinished`, 기존 파괴 수신기, 조기 Exit 전환, Particle System의 Stop Action Destroy가 `Complete`보다 먼저 오브젝트를 제거하지 않게 한다.
- 이벤트를 받을 Animator는 활성화하고 `fireEvents`를 켠다. 화면 밖에서도 필요한 이벤트를 놓치지 않도록 `AlwaysAnimate` 설정을 사용한다.
- 생성 지연은 `FXSetting.presentation_delay`, 수명 정보는 `presentation_duration` 및 기존 측정 경로를 사용한다. 임의의 타이머로 결과 표시 시점을 맞추지 않는다.
- 아티스트가 지정한 이벤트 시점을 보존한다. 에디터 시작이나 임포트 때 공용 클립을 자동 재작성하지 않는다. `Configure Linked Presentation Events`는 에셋을 수정하는 명시적 작업으로만 실행하고 차이를 검토한다.
- 수정 후 `TcgEngine/FX/Validate Animation Events`와 변경에 해당하는 Impact·Continue 검증을 수행한다. 기본 미리보기는 Unity 프리팹 모드를 사용하고, 일회성 미리보기 도구를 정식 에셋으로 남기지 않는다.

## 3. 게임 로직 및 능력 처리

- 게임 규칙과 상태 변경은 `GameLogic` 및 기존 Effect·Ability 경로에서 처리한다. 클라이언트 UI나 FX 이벤트에서 피해·회복·사망·능력 판정을 실행하지 않는다.
- 능력·비밀·공격·턴 진행은 기존 `ResolveQueue`를 통해 처리한다. 큐를 우회하는 직접 재귀 호출, 임의 타이머, 별도 병렬 큐로 처리 순서를 맞추지 않는다.
- 현재 큐의 우선순위와 경계를 보존한다: 능력 Phase 처리, 최상위 Phase 경계의 사망 처리, 비밀, 공격, 진행 콜백. 처리 지연을 고르는 코드와 실제 큐 실행 순서가 일치해야 한다.
- `BeginPhase`/`EndPhase`의 대응, 중첩 능력, 대상 선택으로 중단된 Phase의 재개를 보존한다. 턴 진행 콜백은 앞선 능력·사망 연쇄가 해결되기 전에 실행하지 않는다.
- 능력 간 충돌은 발동 조건·대상 확정 시점·대상 정렬·우선순위·반복 횟수·사망 경계로 명시적으로 해결한다. 프레임 순서나 컬렉션의 우연한 순회 순서에 의존하지 않는다.
- 처리 도중 대상이 사망·이동·변신·제거되었을 때의 유효성을 확인한다. 중첩 발동과 반복 능력이 중복 적용되거나 무한 연쇄를 만들지 않게 한다.
- 사망 대기 상태와 사망 처리의 일시 중단을 구분한다. 전투·게임 종료·큐 초기화 후 이전 작업의 중단 플래그나 콜백이 다음 작업에 남지 않게 한다.
- `RecordReplayState()`는 중간 규칙 상태 기록이며 표시 경계가 아니다. 대상의 효과·상태·연결 이벤트가 완성된 뒤 기존 Presentation 경계 경로로 표시 가능한 결과를 기록한다.
- `AbilityPlaybackMode.Simultaneous`는 한 능력 실행의 결과를 함께 표시하고, `Sequential`은 정렬된 대상별 결과를 나눈다. 다음 연쇄 능력이나 사망 단계가 앞선 능력의 결과에 섞이지 않게 한다.
- 규칙 변경은 일반 플레이·리플레이·AI 예측에서 일관되게 동작해야 한다. AI 예측의 작업 스레드에서 Unity 오브젝트 API를 호출하지 않는다.
- AI 선택 후보는 플레이어·보드·캐시된 카드 목록의 각 집합에서 한 번씩 열거한다. 플레이어별 순회 안에서 전체 보드나 전체 선택 목록을 반복하지 않는다. 중복 제거 후처리로 반복 구조의 오류를 숨기지 않으며, 합법적인 후보의 누락과 중복을 함께 검증한다.
- 변경에 해당하는 충돌 사례를 검증한다: 중첩 발동, 동시 사망, 다중 대상, 같은 능력의 연속 실행, 선택 중단·재개, 반복 공격, 턴 전환. 단순 정상 사례만 확인하고 완료하지 않는다.

## 4. 클라이언트 시각 표시 및 Presentation

- 표시용 게임 상태는 `PresentationState`가 관리한다. 보드·손패·플레이어 수치·프리뷰는 `GameClient.GetGameData()` 등 기존 공개된 표시 상태 경로를 사용하고, 최신 서버 상태나 이후 시점의 이벤트 페이로드를 직접 표시하지 않는다.
- `GameClient.Presentation`과 `GameClient.PresentationEffects`, `PresentationStream`, `FXTool.ScheduleStateFX` 등 기존 경로를 확장한다. 별도 표시 상태나 독립적인 결과 반영 로직을 만들지 않는다.
- 단계의 스냅샷을 Stage한 뒤 FX 생성 전에 Impact·완료·진행 콜백을 예약하고 바인딩한다. 등록을 Seal한 뒤 애니메이션을 시작해 첫 프레임 이벤트가 등록보다 먼저 발생하지 않게 한다.
- HP·마나·상태·카드 영역은 연결된 스냅샷에서 원자적으로 갱신한다. UI나 FX에서 값을 가감하거나 일부 필드만 먼저 바꾸지 않는다.
- 피격·등장·사망 등 실제 결과를 담당하는 FX를 명확히 정한다. 장식·시전자 연출이 더 정확한 결과 표시 시점을 덮어쓰지 않도록 기존 Impact 우선순위 규칙을 따른다.
- 단계 세대와 취소 처리를 보존하여 이전 FX의 늦은 이벤트가 다음 상태를 반영하지 못하게 한다. 중복 Impact·완료 이벤트도 결과를 중복 적용해서는 안 된다.
- FX 누락·실패·취소 시 기존 완료 및 경계 동기화 복구 경로를 사용한다. 대기 상태를 숨기기 위한 무조건 즉시 표시나 임의 지연 우회는 추가하지 않는다.
- 연출 중 입력은 `IsInputOpen`과 기존 서버 입력 검증 경로를 따른다. 드래그·타기팅·턴 종료 등 일부 UI만 우회해서 입력받지 않으며, 같은 카드의 중복 사용을 방지한다.
- 일반 플레이와 리플레이가 같은 표시 순서를 사용하게 한다. 변경에 따라 `Validate Atomic Presentation State`, `Validate Ability Presentation Boundaries`, `Validate Ability Impacts (Play Mode)`, `Validate Continue Playback`, `Validate Client Input Gates`를 선택해 실행한다.

## 5. AI 중간 산출물 및 내부 보고서

- AI 작업 중 생성하는 중간 산출물과 내부 보고서는 항상 프로젝트 루트의 `.ai-work/` 아래에 저장한다.
- 작업별 하위 폴더를 사용한다. 예: `.ai-work/card-audit/`, `.ai-work/ui-design/`.
- 임시 스크립트, 다운로드한 참고 자료, 분석 CSV/JSON/HTML, 검증 로그, 미리보기, 스크린샷, 초안, 캐시 등은 모두 이 디렉토리를 사용한다. 도구의 출력 경로도 이곳으로 지정한다.
- `.ai-work/`는 Git 추적 대상이 아니다. 중간 산출물을 `Assets/`, `docs/`, `Tools/`, `output/` 또는 별도의 루트 임시 폴더에 생성하지 않는다.
- 실제 게임 실행·빌드에 필요한 코드와 에셋, 유지보수할 검증 코드, 사용자가 요청한 정식 문서·최종 결과물은 해당 프로젝트의 정상 경로에 둔다. 이 지침은 그러한 정식 결과물을 숨기는 용도가 아니다.
- 도구가 출력 위치를 강제하여 다른 곳에 중간 산출물을 생성하면, 작업 종료 전에 `.ai-work/`로 옮기거나 삭제한다. 외부 폴더를 가리키는 링크는 대상 내용을 삭제하지 않고 링크만 제거한다.

## 6. 능력 데이터 명세

모든 능력의 신규 제작·수정과 처리 기능 확장에 적용한다. 설정은 ScriptableObject에, 발동별 상태는 실행 문맥에 보관한다. 각 설정은 지원 대상형·입력 범위·평가 시점·적용 결과를 정의해야 한다. 기존 ID/GUID와 ENUM 직렬화 값을 보존하고, 스키마 변경 시 참조 데이터와 소비 경로를 함께 이관한다.

| 데이터 | 책임 | 규격 |
| --- | --- | --- |
| AbilityData | 발동·반복·대상 선정·효과 연결·비용·표시 구성 | 6.1 |
| ConditionData | 시전자·이벤트·후보·수신자의 자격 판정 | 6.2 |
| EffectData | 확정된 수신자에 대한 규칙 상태 변경 | 6.3 |
| DataContainer / NumericModifierData | 능력이 소유하는 선언형 데이터와 피해·회복 수치 보정 | 6.4 |

이 절은 능력 제작·구현에 적용하는 확정된 데이터 규격과 처리 규칙이다. 공통 조건 정책은 `ConditionData.evaluation`과 `ConditionEvaluationPolicy`로 설정한다. 7절은 큐의 구성과 실행 경계를 설명한다.

- 능력 관련 데이터 클래스(AbilityData·ConditionData·EffectData·DataContainer와 반복·필터·정렬·설명값 등 참조 데이터)의 추가·수정·이름 변경·삭제 시 **같은 작업에서 AGENTS.md의 해당 명세도 갱신한다.** 새 클래스는 구현 완료 전에 클래스별 항목을 추가하고 소스 경로·상속 관계·Inspector 필드/자료형/입력 규칙·모든 오버라이드와 대상형·일반/지속/문맥 처리·계산식·빈 값/실패 동작을 기록한다.
- 필드·ENUM·오버라이드·지원 대상형·계산·평가 수명 변경은 기존 항목과 공통 규칙에 함께 반영한다. 삭제·이름 변경 시 이전 항목과 참조를 정리한다. 이 문서에는 확정된 규칙과 코드로 확인한 필드·오버라이드 계약만 기록한다. 미결정 제안, 구현 예정 사항, 명세 위반 목록과 작업별 검토 결과는 별도 보고서에 기록한다. 구현 결함을 의도된 규칙으로 바꾸거나 정의하지 않은 대상형을 지원한다고 기록하지 않는다.
- 문서 갱신 후 소스의 직접·간접 상속 관계, 직렬화 필드, 오버라이드 시그니처를 명세와 대조하여 누락·유령 항목·낡은 개수·깨진 소스 참조가 없는지 확인한다. 정상 플레이·AI·리플레이의 동작 검증과 문서의 정적 대조는 별도로 보고한다.

### 6.1 AbilityData 규격 및 규칙

#### 6.1.1 Inspector 필드

| 섹션 | 필드 | 형식 | 적용 규칙 |
| --- | --- | --- | --- |
| 식별 | `id` | 문자열 | 능력을 식별하는 고유 참조 키. 빈 값과 중복을 허용하지 않는다. |
| Trigger | `trigger` | AbilityTrigger | 설정한 이벤트에 능력을 연결한다. |
| Trigger | `conditions_trigger` | ConditionData 배열 | 발동 주체와 이벤트 원인에 대한 조건을 AND로 결합한다. 평가 시점은 각 조건의 검사 정책을 따른다. |
| Repeat | `condition_repeat` | RepeatConditionData 참조 | 참조한 반복 종류의 상한 산정과 계속 판정을 사용한다. |
| Target | `criteria_target` | AbilityTarget | 기준 후보의 영역·위치 산정·선택 방식을 결정한다. 공간 기준은 위치를 정하고 effect_target이 실제 수신자를 정한다. |
| Target | `effect_target` | AbilityEffectTarget | 일반 실행의 공간 기준(SelfSlot·AllSlots·PlayTarget·DeathSlot·AttachedSlot·SelectTarget·SelectSlot)에 적용한다. 범위 확장 후 위치를 Slot/Card/Player로 변환한다. 비공간 기준과 Ongoing 경로는 고유 대상형을 유지하며 이 필드는 기본값 Slot으로 둔다. |
| Target | `conditions_criteria_target` | ConditionData 배열 | 기준 후보 및 선택 가능한 대상을 제한한다. |
| Target | `condition_wide_range` | ConditionWideAreaRange 참조 | 기준 위치에 설정된 상대 좌표 패턴을 적용한다. |
| Target | `condition_target` | ConditionData 배열 | 이 단계를 지원하는 실행 경로에서 범위 확장 후 실제 효과 수신자를 제한한다. 경로별 적용 여부는 6.1.4의 계약을 따른다. |
| Target | `filters_target` | FilterData 배열 | 이 단계를 지원하는 실행 경로에서 배열 순서대로 후보 집합에 선택 규칙을 적용한다. |
| Target | `sort_target` | SortData 참조 | 이 단계를 지원하는 실행 경로에서 필터 결과의 적용 순서를 결정한다. 동률은 결정적 순서를 유지한다. |
| Effect | `effects` | EffectData 배열 | 최종 대상별로 배열 순서대로 효과를 실행한다. 각 효과는 해당 대상형의 실행 경로를 지원해야 한다. |
| Effect | `status` | StatusData 배열 | 효과 실행 후 설정된 상태를 부여한다. 일반 실행은 상태 수치·기간, 지속 실행은 ongoing 상태로 적용한다. |
| Effect | `value` | 정수 | 효과와 상태에 전달할 값. 소비하는 데이터가 단위·부호·허용 범위·계산 방식을 정의한다. |
| Effect | `duration` | 정수 | 효과와 상태에 전달할 기간. 상태에서 영구 여부와 유한 기간의 감소 경계는 상태 수명 규칙을 따른다. |
| Data Containers | `data_containers` | DataContainer 배열 | Ongoing 능력의 선언형 보정 데이터. 비어 있으면 수치 요청을 변경하지 않는다. effects/status와 독립적이며 value/duration을 암묵적으로 상속하지 않는다. 6.4를 따른다. |
| Cancel | `can_cancel` | 불리언 | 선택 취소 허용 여부. 선택 UI와 서버의 비용·영역·문맥 정리가 같은 정책을 따른다. |
| Chain/Choices | `chain_abilities` | AbilityData 배열 | 일반 실행에서는 후속 능력, 선택 실행에서는 선택지로 해석한다. |
| Activated Ability | `mana_cost` | 정수 | 해당 능력의 비용 처리 단계에서 차감하는 마나. 카드 사용 비용과 구분한다. |
| Activated Ability | `exhaust` | 불리언 | 해당 능력의 비용 처리 단계에서 소진을 적용할지 결정한다. |
| FX | `playback_mode` | AbilityPlaybackMode | 결과 표시 단위를 결정한다. 규칙의 사망 경계와 분리한다. |
| FX | `show_card_fx` | 불리언 | 발동 카드 확대 표시 여부를 결정한다. |
| FX | `board_fx`, `caster_fx`, `target_fx` | GameObject 참조 | 보드·시전자·대상 위치에 표시할 연출. 결과 반영은 2·4절의 Impact 계약을 따른다. |
| FX | `ongoing_fx` | GameObject 참조 | 지속 능력의 활성 수명에 연결된 장식 연출. 원천의 비활성화에 맞춰 제거한다. |
| FX | `cast_audio`, `target_audio` | AudioClip 참조 | 시전 및 대상 결과에 연결할 음향. |
| FX | `charge_target` | 불리언 | 대상 돌진 표시 여부를 결정한다. |
| Text | `title` | 문자열 | 능력 표시명. 참조 키와 독립적으로 관리한다. |
| Text | `desc` | 문자열 | 능력 설명. 호출하는 GetDesc 오버로드의 치환 규칙을 사용한다. |
| Text | `selector_desc` | 문자열 | 선택 안내. 8.7의 간결한 지정형으로 선택 대상과 단계를 안내한다. 대상 제한은 기존 조건과 선택 검증 경로를 따른다. |

| 미지정 설정 | 규칙 |
| --- | --- |
| 조건 배열 | 해당 단계의 제한 없음. 조건 자체의 빈 값 해석은 6.2를 따른다. |
| condition_wide_range | 범위 조건 없음. |
| filters_target / sort_target | 후보 축소 없음 / 입력 순서 유지. |
| effects / status | 해당 종류의 처리 없음. |
| data_containers | 요청에 대한 컨테이너 보정 없음. |
| chain_abilities | 후속 능력 또는 선택지 없음. 선택형은 유효한 선택지 부재를 처리해야 한다. |

#### 6.1.2 ENUM 정의

ENUM은 의미와 직렬화 값을 함께 관리한다. 동일한 이름이 다른 ENUM에 속하면 해당 데이터형의 의미를 따른다.

| AbilityTrigger | 값 | 정의 및 용도 |
| --- | --- | --- |
| None | 0 | 연결 또는 명시 호출로 실행하는 능력. |
| OnGameStart | 1 | 게임 시작 이벤트. |
| Ongoing | 2 | 지속 수치·상태 재계산. |
| Activate | 5 | 사용자 활성화에 따른 실행. |
| OnPlay / OnPlayOther | 10 / 11 | 일반 카드 사용에 대한 본인/다른 카드의 반응. |
| OnUse / OnUseOther | 13 / 14 | 일반 사용 및 강제 사용·소환에 대한 반응. |
| StartOfTurn / EndOfTurn | 20 / 22 | 턴 시작/종료 이벤트. |
| OnBeforeAttack / OnBeforeAttackOther | 30 / 31 | 공격 피해 적용 전 이벤트. |
| OnAfterAttack / OnAfterAttackOther | 32 / 33 | 공격 적중에 따른 피해 처리 후 이벤트. |
| OnBeforeDefend / OnBeforeDefendOther | 34 / 35 | 방어 피해 적용 전 이벤트. |
| OnAfterDefend / OnAfterDefendOther | 36 / 37 | 방어 피해 처리 후 이벤트. |
| OnAfterDamage | 38 | 피해 원인 카드의 피해 후 반응. 피해 대상이 triggerer다. |
| OnKill | 39 | 처치 귀속 카드의 처치 반응. |
| OnDeath / OnDeathOther | 40 / 42 | 본인/다른 카드의 사망 이벤트. |
| OnDraw | 45 | 드로우 이벤트. |
| OnMove | 47 | 이동 처리 경로가 발행하는 이벤트. 효과로 이동한 경우에도 해당 경로가 발행하면 반응한다. |
| OnHeal / OnHealOther | 50 / 51 | 회복 처리 이벤트. 이벤트 발생과 실제 회복량은 별도 값이다. |
| OnAddClubOther | 55 | 동아리 가입 이벤트. 가입 카드가 triggerer다. |

발동 주체·이벤트 원인·효과 수신자를 구분한다. Other는 이벤트 관계이며 적/아군 제한은 조건으로 표현한다. 사용과 소환, 공격 시도와 적중, 드로우와 생성은 서로 다른 이벤트 의미를 갖는다. 이동 이벤트의 발생 여부는 이동 경로의 계약으로 정의하며, 효과에 의한 이동이라는 이유만으로 OnMove를 금지하지 않는다. 회복 이벤트는 실제 회복량과 독립적으로 발생하며 추가 상태 제한은 조건으로 검사한다.

| AbilityTarget | 값 | 정의 및 용도 |
| --- | --- | --- |
| None | 0 | 무대상 효과 실행. |
| Self | 1 | 자기 자신인 Card만 반환한다. 자신의 타일은 SelfSlot 또는 슬롯 후보의 위치 조건으로 지정한다. Condition은 후보를 제한하며 대상형을 변환하지 않는다. |
| SelfSlot | 2 | 시전자 위치를 Slot 기준으로 반환한다. 기준 조건·범위·최종 조건·필터·정렬을 적용한다. |
| ParentSlotPlayers | 8 | 부모 회차의 확정 슬롯을 기준 조건으로 걸러 진영별 Player로 대응한다. 최종 Player 조건·필터·정렬을 적용하며 중복 Player는 통합한다. |
| PlayerSelf / PlayerOpponent / AllPlayers | 4 / 5 / 7 | 소유 플레이어·상대 플레이어·전체 플레이어 후보. |
| AllCardsBoard | 10 | 양측 보드 카드 후보. |
| AllCardsHand | 11 | 양측 손패 카드 후보. |
| AllCardsAllPiles | 12 | 동아리·덱·버림·손패·비밀·보드·장비·부착·임시 영역의 카드 후보. |
| Club | 13 | 소유자의 동아리 카드에 대한 지속 효과 대상. |
| AllSlots | 15 | 보드 슬롯 후보. |
| AllCardData | 17 | CardData 정의 후보. |
| PlayTarget | 20 | 카드 사용 시 지정한 위치. |
| AbilityTriggerer | 25 | 능력을 일으킨 이벤트 원인 카드. |
| DeathSlot | 26 | 사망 이벤트 문맥에 기록된 위치. |
| EquippedCard | 27 | 장비와 장착자 간 연결 대상. |
| AttachedSlot | 28 | 부착 위치를 나타내는 슬롯. |
| SelectTarget | 30 | 기존 직렬화 값을 보존하는 슬롯 선택 이름. SelectSlot과 같은 위치 입력·검사·수신자 변환 경로를 사용한다. |
| SelectSlot | 31 | 위치 선택. 보드 Card 클릭은 해당 Slot, Player 클릭은 PlayerSlot으로 정규화한다. OnPlay는 play_target_slot에 위치를 저장하고 다른 발동은 해결 중 선택한다. 실제 수신자 및 PlayerSlot 허용 여부는 effect_target으로 정한다. |
| CardSelector | 40 | 조건과 필터로 구성한 카드 목록에서 선택. |
| ChoiceSelector | 50 | 연결된 능력 목록에서 선택. |

| AbilityEffectTarget | 값 | 정의 및 용도 |
| --- | --- | --- |
| Slot | 0 | 보드 위치 자체를 DoEffect(Slot)에 전달한다. 빈 타일도 수신자이며 PlayerSlot은 허용하지 않는다. |
| Unit | 1 | 각 보드 위치의 점유 유닛(Student/NonStudent/Place)을 Card로 변환한다. 빈 위치는 수신자 목록에서 제외하며 PlayerSlot은 허용하지 않는다. |
| Player | 2 | PlayerSlot을 Player로 변환한다. 보드 타일을 선생님으로 변환하지 않는다. |
| UnitOrPlayer | 3 | 보드 위치는 점유 유닛, PlayerSlot은 해당 Player로 변환한다. 실내 유닛을 지정해도 선생님에게 추가 적용하지 않는다. |

| 보조 ENUM | 정의 및 용도 |
| --- | --- |
| AbilityPlaybackMode | Simultaneous=0: 실행 결과 단위 표시. Sequential=1: 정렬된 대상 단위 표시. |
| CardType | None=0, Hero=5, Club=7, Student=10, NonStudent=11, Spell=20, Place=30, Secret=40, Equipment=50, Attachment=60, PlayerAbility=70. 카드 분류. 유닛은 Student/NonStudent/Place를 모두 포함한다. 용어와 대상 범위는 8절을 따른다. |
| PileType | None=0, Club=5, Board=10, Hand=20, Deck=30, Discard=40, Secret=50, Equipped=60, Attached=70, Temp=90, PlayerAbility=100. 카드 저장 영역. |

#### 6.1.3 반복

반복 종류가 실행 상한의 산정 방식과 회차 계속 조건을 결정한다. 반복 횟수는 최초 실행을 포함하며, 효과 자체의 수량과 별개다.

| 반복 종류 | 데이터 | 입력 | 상한 산정 | 계속 판정 |
| --- | --- | --- | --- | --- |
| 고정값 | RepeatStaticValue | `value` | 설정값을 실행 횟수로 적용한다. | 현재 회차를 설정 상한과 비교한다. |
| 조건부 개수 | RepeatCountType | `pile`, `player`, `has_type`, `has_club`, `has_trait` | 발동 시 조건에 맞는 카드 수를 실행 상한으로 산정한다. | 현재 회차를 산정된 상한과 비교한다. |
| 기본 실행 | condition_repeat 미지정 | 없음 | 능력의 기본 실행 상한을 사용한다. | 추가 반복 없이 기본 실행을 완료한다. |
| 확장 반복 | RepeatConditionData 파생형 | 파생 데이터가 선언한 설정 | GetMaxRepeatTimes가 정의한 방식으로 산정한다. | 반복 평가 메서드가 해당 실행 문맥과 상태로 계속 여부를 결정한다. |

- 발동 조건은 실행의 등록 여부, 반복 조건은 등록된 실행의 다음 회차 여부를 판정한다. 최초 발동 조건을 회차마다 재평가하지 않는다.
- 다음 회차의 판정은 선행 회차의 효과·연결 능력·유발 반응·사망 연쇄가 해결된 안정 시점에 수행한다.
- 상한 산정 시점과 계속 판정 시점을 구분한다. 상태에 따라 반복량을 갱신하는 종류는 갱신 시점과 종료 조건을 데이터 계약으로 정의한다.
- 시전자의 필수 존재 조건이 무효화되면 반복을 종료한다. 대상 재선정과 선택 재개는 회차 및 실행 문맥에 맞춰 처리한다.
- 회차마다 연결 능력을 등록한다. 능력 반복·연결 능력 반복·효과 수량의 조합으로 산정되는 총 적용량을 검증한다.

#### 6.1.4 Criteria Target과 적용 범위

아래 표는 대상 선정 단계의 의미를 정의한다. 일반 실행·선택 재개·지속 실행 및 Card/Player/Slot/CardData 경로가 모든 단계를 동일하게 사용해야 한다는 뜻은 아니다. 각 경로는 소비하는 필드, 적용 순서, 입력 대상형, 생략하는 단계와 이유를 명시한다. 해당 경로가 지원하지 않는 필드 설정은 Inspector 또는 데이터 검증에서 식별하며, 설정을 조용히 무시하지 않는다. 선택한 Ability 에셋의 기본 검사에는 `TcgEngine/Validate Selected Ability Fields`를 사용하고, 실행 구조의 검증에는 `TcgEngine/Validate Ability Pipeline`을 사용한다. 경로를 통합하기 전에 기존의 의도된 차이를 확인한다.

| 단계 | 입력 | 결과 |
| --- | --- | --- |
| 기준 후보 구성 | criteria_target | 대상형·영역·선택 방식에 따른 후보 집합. |
| 기준 조건 판정 | conditions_criteria_target | 기준 또는 선택 대상 자격을 충족한 후보. |
| 범위 확장 | condition_wide_range | 기준 위치와 상대 좌표 패턴으로 결정한 적용 범위. 같은 위치를 중복 제거한다. |
| 수신자 변환 | effect_target | 공간 기준의 위치를 Slot·점유 Card·PlayerSlot의 Player로 변환한다. |
| 수신자 제한 | condition_target | 범위 안에서 실제 적용 조건을 충족한 대상. |
| 필터 | filters_target | 수량·선택 기준을 반영한 대상 집합. |
| 정렬 | sort_target | 효과를 적용할 대상 순서. |
| 실행 | effects/status | 유효한 최종 대상에 적용한 규칙 결과. |

- 기준 대상의 자격과 최종 수신자의 자격을 독립적으로 설정한다. 각 조건은 해당 단계의 대상에만 적용한다.
- 범위는 중심 포함 여부·방향 변환·공간 관계로 정의한다. 같은 능력의 같은 실행 회차에서 범위가 겹치면 대상형과 식별자로 통합하여 동일 수신자를 한 번만 대상 목록에 넣는다. 최초로 확정된 순서를 보존한다. 서로 다른 Slot에서 같은 Player로 전달되는 실내 타일 효과는 별개의 적용이며 6.3.4를 따른다. 서로 다른 능력·명시적 반복 회차와 effects 배열의 개별 효과는 이 중복 제거로 생략하지 않는다.
- 대상형에 맞는 공간 표현을 사용한다. 카드 후보는 실제 카드를, 슬롯 후보는 점유 여부와 관계없는 위치를 표현한다.
- 확정된 대상 목록과 적용 시점의 유효성 검사를 구분한다. 실행 도중 생성된 객체는 다음 대상 선정 단계부터 후보가 된다.
- 선택 후보 캐시와 일회 판정 결과는 현재 대상의 유효성을 보증하지 않는다. 선택 확정·실행 재개·효과 적용 단계마다 해당 경로가 요구하는 존재·영역·소유·상태 조건을 확인한다. 이벤트 스냅샷과 현재 상태의 검사 역할을 구분하고, 사망 대기 및 사망 이벤트의 정상 수신자를 일괄 거부하지 않는다.
- 보드 선택 입력은 위치로 통일한다. 기준 조건은 보드 Slot을 검사하고 CardType·Owner 등 점유 조건은 그 슬롯의 유닛을 조회한다. `ConditionTarget(Card)`는 슬롯 선택의 기준 조건으로 사용하지 않는다. PlayerSlot의 기준 조건은 해당 Player에 적용한다. 유닛 적용 능력에서 기준 위치가 반드시 점유되어야 한다면 점유·카드 종류 조건으로 제한한다. 범위 능력은 빈 기준 위치를 허용할 수 있다.
- 유닛 적용 능력의 선택 시 기준 위치의 점유 유닛에는 현재 보드 존재·사망 대기·HP·은신·주문 면역 제한을 검사한다. 슬롯 적용 능력은 점유 유닛의 지정 제한으로 위치 선택을 막지 않는다. 범위 안의 다른 유닛은 최종 조건과 각 효과의 수치 적용 규칙을 따른다.
- `GetSpatialTargets`는 PlayTarget, OnPlay 선택, 해결 중 선택, 자동 위치 기준의 공통 범위 확장·수신자 변환·최종 조건·필터·정렬 경로다. `GetSlotTargets`는 이 결과 중 Slot 수신자를 요청하는 접근자이며, 선택된 Card/Player를 별도 목록 경로에서 다시 실행하지 않는다. `GetSelectableSlots`는 보드 위치와 허용된 PlayerSlot을 각각 한 번 열거하며 UI·서버 CanTarget·AI가 같은 자격 검사를 사용한다.
- 선택 패킷 `MsgSelectSlotRequest`와 Card의 사용 시 선택 상태는 슬롯만 저장한다. 보드 선택을 카드 UID나 별도 플레이어 ID로 재해석하지 않는다. 카드 클릭도 표시된 위치를 전달하며 일반 카드 조작의 UID 유효성 검사로 슬롯 선택을 차단하지 않는다. 서버 입력 게이트·선택 세대 검사는 유지한다.
- 저장된 선택은 위치이며 실행 대상 선정 시 그 위치의 현재 수신자를 확정한다. 확정된 Card 목록은 첫 DoEffect 전에 완성하고 실행 중 다른 카드로 대체하지 않는다. 보드에서 사라진 확정 유닛은 생략한다. 반복 회차는 새로 대상을 선정한다. CardSelector와 ChoiceSelector는 비공간 선택으로 기존 카드 UID/능력 선택을 유지한다.
- 선택 UI, 서버 확정 검사, 최종 적용은 같은 대상 제한을 사용한다. 미지원 대상형은 명시적으로 거부하고 기본 오버로드의 허용/무동작에 의존하지 않는다.
- 각 단계의 빈 결과와 대상 소멸은 종료·선택 취소·비용 정책에 따라 처리하며 실행 문맥을 누락하거나 중복 적용하지 않는다.

대상 자격의 판정은 6.2, 타일에서 실제 수신자로 전달하는 규칙은 6.3.4를 따른다.

현재 경로별 필드 소비는 다음과 같다. 이는 현황이며 차이가 있다는 이유만으로 모두 같은 경로로 변경하지 않는다. 각 차이의 의도와 참조 데이터의 설정을 대조한 뒤 유지할 계약과 수정할 불일치를 구분한다. 표의 `없음`은 해당 경로에서 그 단계를 실행하지 않는다는 뜻이다.

| 실행 경로 | 기준 조건 | 범위 | 최종 조건 | 필터·정렬 |
| --- | --- | --- | --- | --- |
| 일반 Card 목록 | 검사 | 보드 카드에 적용; 다른 영역은 직접 후보 | 검사 | 적용 |
| 일반 공간 기준(SelfSlot·AllSlots·DeathSlot·AttachedSlot) | 보드 Slot 검사 | 적용 후 중복 제거 | 변환된 수신자에 검사 | 변환된 대상형에 적용 |
| 일반 Player 목록 | 목록 구성 시 없음 | 없음 | 없음 | 적용 |
| ParentSlotPlayers | 부모 슬롯에 검사 | 없음 | 대응 Player에 검사 | 적용 |
| 일반 CardData 목록 | 검사 | 없음 | 없음 | 적용 |
| PlayTarget·OnPlay 선택·해결 중 선택의 보드 위치 | 같은 CanTarget(Slot) 검사 | 적용 후 중복 제거 | 변환된 수신자에 검사 | 변환된 대상형에 적용 |
| PlayTarget·OnPlay 선택·해결 중 선택의 PlayerSlot | 허용된 Player 검사 | 없음 | Player에 검사 | Player에 적용 |
| CardSelector | 후보 구성은 일반 Card 목록 경로; 확정은 캐시 UID 포함 및 현재 기준·최종 조건 재검사; 필터 재추첨 없음 | 후보 구성 시 적용 | 후보 구성 시 검사 | 후보 구성 시 적용 |
| Ongoing 보드 목록 | 검사 | 적용 | 검사 | 없음 |
| Ongoing Self/Player/Club/EquippedCard 및 손패·장비 목록 | 검사 | 없음 | 없음 | 없음 |
| Ongoing AttachedSlot | 위치·점유 카드 확인; 기준 조건 호출 없음 | 없음 | 없음 | 없음 |

Ongoing의 AllCardsAllPiles는 현재 손패·보드·장비를 순회하며 일반 실행의 전체 영역 순회와 다르다. 대상 존재·입력 권한·선택 유형·시전 가능 상태 등 기본 유효성 검사는 위 필드 소비와 별도로 정의한다. 기능 변경 시 이 표와 Inspector 지원 범위를 함께 갱신한다.

#### 6.1.5 Chain Ability

| 연결 방식 | 데이터 해석 | 등록 및 실행 규칙 |
| --- | --- | --- |
| 일반 연결 | chain_abilities를 순서 있는 후속 능력으로 해석한다. | 부모 효과 처리 후 배열 순서대로 chain에 등록한다. 이후 실행은 큐의 chain 우선순위를 따른다. |
| 선택 연결 | ChoiceSelector의 chain_abilities를 선택지로 해석한다. | 부모 완료 후 선택한 능력을 일반 연결과 같은 큐 등록 경로에 등록한다. 자식은 새 실행 문맥과 조건 캐시를 가지며 부모 결과 스냅샷을 복사한다. |
| 반복 연결 | 각 부모 회차에 연결된 후속 능력으로 해석한다. | 부모 회차마다 등록하며 연결 능력의 반복 정책은 독립적으로 적용한다. |

- 연결 능력의 조건·대상·범위·효과·상태·반복·비용은 해당 능력의 설정을 따른다. 부모의 설정을 암묵적으로 상속하지 않는다.
- 전달 문맥과 개별 실행 상태를 분리한다. caster, triggerer, 이벤트 스냅샷, 선택 결과의 전달 의미를 정의하고 조건 결과·회차·취소 상태는 실행별로 관리한다.
- 같은 데이터가 복수 발동에서 사용되어도 상태를 공유하지 않는다. 순환 연결은 유한 상한 또는 상태 기반 종료 조건을 가져야 한다.
- 부모 결과, 후속 능력, 그 결과로 유발된 반응의 실행 순서는 7절을 따른다. 배열의 등록 순서와 전체 하위 실행의 완료 순서를 구분한다.
- 부모의 대상에서 후속 대상을 구하는 연결은 해당 부모 회차의 확정된 대상 목록을 불변 스냅샷으로 전달한다. 유효한 빈 타일도 위치 대상으로 보존한다. 형제 Chain은 같은 부모 스냅샷을 참조하며 앞선 형제의 마지막 대상이나 전역 last_* 값으로 입력을 대체하지 않는다. AbilityExecutionContext는 부모 회차의 슬롯을 복사하여 각 자식의 parent_slots로 전달한다. ParentSlotPlayers는 이 목록을 소비한다.
- 실내 타일의 피해·회복은 6.3.4의 공통 타일 래퍼에서 선생님에게도 적용한다. 같은 결과를 내는 선생님용 Chain을 중복 연결하지 않는다. ParentSlotPlayers는 부모 슬롯을 플레이어 집합으로 변환하는 명시적 후속 능력에 사용하며, 타일마다 적용하는 래퍼와 달리 같은 Player를 통합한다.
- 같은 배열에서 먼저 등록한 능력의 모든 하위 Chain·유발 반응이 다음 형제보다 먼저 완료된다고 가정하지 않는다. 선행 능력의 전체 하위 처리 완료가 필요한 연결은 지원되는 완료 의존 방식으로 표현하고 검증한다. 단순 배열 순서나 임의 타이머로 완료 의존을 대신하지 않는다.

#### 6.1.6 대상 가공 및 표시 데이터

| 데이터 종류 | 주요 필드 | 데이터 규격 및 적용 규칙 |
| --- | --- | --- |
| FilterData | 수량·선택 방식·수치 기준 | 후보의 일부 또는 조건에 맞는 후보를 선택한다. 대상형별 지원, 부족한 후보 수, 동률 및 무작위 판정 수명을 정의한다. |
| SortData | `descending`, 파생 정렬 기준 | 지정 기준으로 결과를 정렬한다. descending은 정렬 방향을 제어한다. |
| CardData | `abilities`, 기본 수치·분류·소속·무기·종족(`traits`)·Marker 초기값(`markers`) | 능력 참조와 카드의 기본 데이터를 구성한다. 전투 중 변경값은 런타임 카드에 보관한다. |
| CardData 표시 | `text`, `text_format`, `text_values`, `desc` | 카드 표면 문구·동적 치환값·상세 설명을 각각 관리한다. |
| DescValueData | 값 원천, `mapping` | 표시할 값을 산정하고 선택적으로 정수→문자열 매핑을 적용한다. mapping은 StringMapping(value, label) 배열이다. |

FilterData는 집합을 축소하고 SortData는 적용 순서를 정한다. ConditionData의 개별 자격 판정과 역할을 구분한다. 반복·필터·정렬의 결과 목록이나 진행 상태를 공유 에셋에 저장하지 않는다.

| 표시 경로 | 입력과 치환 규칙 |
| --- | --- |
| AbilityData.GetDesc(CardData) | desc의 `<name>`은 카드 title, `<value>`는 능력 value, `<duration>`은 능력 duration으로 치환한다. |
| AbilityData.GetDesc(ClubData) | desc의 `<name>`을 동아리 title로 치환한다. |
| AbilityData.GetDesc() | desc 원문을 사용한다. |
| selector_desc / AimTargetFX | 선택 안내 원문을 사용한다. 치환자를 해석하지 않는다. |
| CardData / CardUI | text_format의 인덱스 치환자 `{i}`에 text_values[i]의 Resolve 결과를 넣는다. text_format 미지정 시 text를 사용하고 desc는 상세 설명으로 표시한다. |
| StatusData.GetDesc(value) | desc의 `<value>`에 전달값을 적용한다. 인자 없는 호출은 API의 기본 표시값을 사용한다. |
| DescValueStatic | 설정된 text를 사용한다. |
| DescValueCardStat | 설정된 기본 수치를 읽는다. 런타임은 표시용 카드의 현재 값, 정적 화면은 CardData 기본값을 사용한다. |
| DescValueStat | 설정된 `marker`의 값을 읽는다. 런타임은 표시용 카드의 Marker 값, 정적 화면은 CardData.markers의 초기값을 사용한다. Marker가 플레이어에게 보이는 경로는 이 치환값과 카드 본문뿐이다. |
| DescValueStatus | 설정된 상태 수치를 읽는다. 런타임 상태가 없는 정적 화면은 기본 상태값으로 해석한다. |
| DescValueData.mapping | 산정값과 일치하는 label을 사용하며 매핑되지 않은 값은 수치 문자열로 표시한다. |

치환자 인덱스는 값 배열과 일치해야 한다. 리터럴 중괄호는 포맷 문자열의 이스케이프 규칙(`{{`, `}}`)을 따른다. 규칙 문구의 대상·수치·기간·횟수·조건·예외는 에셋 설정과 일치해야 하며, 정적·전투·확대·선택 표시에서 각각 검증한다.

배치와 이동은 CanOccupySlot의 점유·유효 좌표·카드 종류별 위치 제한을 공유한다. 진영 허용은 CanUseSlotTerritory(ownerId, slot, purpose)가 별도로 결정한다. 기본 정책은 두 목적 모두 자기 진영과 중립만 허용하며, 정책 변경 시 목적별로 확장한다. CanMoveCard는 보드 존재·이동 자격·현재 위치와의 차이를 추가 검사한다. 배치 가능성 조회는 CardData와 소유자 ID를 사용하며 Card 생성·등록·난수 소비를 하지 않는다.

### 6.2 ConditionData 규격 및 규칙

#### 6.2.1 판정 입력과 배치

조건은 전달된 상태와 실행 문맥을 읽어 불리언 결과를 반환한다. caster는 능력의 주체이고 target은 해당 호출에서 검사할 대상이다. 발동 조건의 target에는 이벤트 원인이 전달되며, 대상 선정 조건에는 후보 또는 최종 수신자가 전달된다.

| 공통 Inspector 필드 | 형식 | 규칙 |
| --- | --- | --- |
| `evaluation` | ConditionEvaluationPolicy | Revalidate는 호출 시점의 상태를 검사하고, EvaluateOnce는 동일 실행의 동일 검사 결과를 재사용한다. 기본값은 Revalidate다. 모든 파생 Condition이 상속하며 캐시의 검사 단위와 수명은 6.2.4를 따른다. |

| 판정 경로 | 입력 | 규칙 |
| --- | --- | --- |
| Evaluate | 공통 입력, object target, ConditionStage, context, 선택적 selected | Trigger 단계에서 발동 판정을 먼저 검사한 뒤 대상형별 판정을 호출한다. Slot과 selected가 함께 전달되면 해당 기준 포함 오버로드를 사용한다. 공통 평가기는 이 결과에 evaluation 정책을 적용한다. |
| IsTriggerConditionMet | Game, AbilityData, caster, context | 대상과 독립적인 시전자·게임·이벤트 자격을 검사한다. |
| IsTargetConditionMet | 공통 입력과 Card / Player / Slot / CardData | 전달된 대상형의 자격을 검사한다. 카드 인스턴스와 카드 정의를 구분한다. |
| IsTargetConditionMet의 기준 포함 오버로드 | 공통 입력과 Card 또는 Slot 기준, Slot 후보 | 기준과 후보 사이의 공간 관계를 검사한다. |
| conditions_trigger | 발동 주체와 이벤트 원인 | 각 조건의 발동 판정과 원인 대상 판정을 결합한다. |
| conditions_criteria_target | 기준 후보 또는 선택 후보 | 범위 확장의 기준이 될 자격을 검사한다. |
| condition_target | 범위 확장 후 수신자 | 실제 적용 대상의 자격을 검사한다. |
| 효과 내부의 condition | 분기 효과가 지정한 검사 대상 | 6.3.3의 분기 규칙과 동일한 공통 검사 정책을 적용한다. |

- 조건 배열은 AND로 결합하며 빈 배열은 제한이 없다. null 항목을 의도적인 조건으로 사용하지 않는다.
- OR형 복합 조건은 각 자식에 필요한 발동·대상 판정을 함께 만족한 결과를 OR로 결합한다. 서로 다른 자식의 부분 판정을 합쳐 하나의 자식이 통과한 것으로 취급하지 않는다.
- 현재 기본 ConditionData의 오버로드는 true를 반환한다. 파생형을 제작할 때 지원 대상형과 무관한 판정 경로를 구분하고, 미지원 대상형이 기본 반환값으로 통과하지 않게 한다.
- 조건은 대상형을 변환하거나 효과를 실행하지 않는다. 확정할 무작위 판정의 결과 보관은 공통 평가기와 실행 문맥이 담당한다.
- 이벤트 조건은 명시적인 이벤트 문맥을 사용한다. 이벤트 당시 정보와 현재 상태 중 어느 것을 검사하는지 정의하고, 문맥이나 대상이 없는 경우의 결과도 정의한다.

#### 6.2.2 전체 파생 클래스의 필드와 오버라이드

아래 표는 현재 ConditionData의 모든 직접·간접 파생 클래스를 조사한 결과다. 런타임 제작용 클래스는 38개이며 모두 직접 상속한다. Editor 검증 전용 조건은 제작용 목록에 포함하지 않는다. 각 행은 필드의 선언형, 실제 오버라이드, 대상형별 판정을 함께 기록한다. 지원 대상형과 입력 제약은 해당 오버라이드를 기준으로 읽는다.

- 메서드 표기에서 공통 인자 `Game data, AbilityData ability, Card caster`와 선택적 `AbilityEventContext context`는 생략한다. `IsTriggerConditionMet()`는 발동 판정, `IsTargetConditionMet(...)`의 괄호는 나머지 대상 인자다.
- 표에 없는 오버라이드는 ConditionData의 기본 `true`를 상속한다. 명시적으로 `false`를 반환하는 경로와 기본 메서드를 상속하는 경로를 구분한다. 선언된 오버라이드가 모든 ENUM 값·대상 상태를 지원한다는 뜻은 아니다.
- ConditionEvaluation.Matches가 검사 역할과 대상에 맞춰 Evaluate를 호출하고 실행별 캐시를 관리한다. 복합 조건은 자식도 동일 평가기로 처리한다. 최근 행동 조건은 실행 문맥이 있으면 그 스냅샷을 우선하며, 문맥 없는 호출은 Game 기록을 사용한다.
- `CompareBool`은 oper에 따른 유지/반전, `CompareInt`는 정수 비교다. null·빈 입력을 별도 처리한다고 명시한 경우 외에는 유효한 객체·초기화된 컬렉션·필수 참조가 필요하다. 빈 목록과 null 목록은 같은 입력이 아니다.

| 클래스 / 소스 | Inspector 필드 : 형식 | 선언된 오버라이드 | 현재 판정 및 입력 규칙 |
| --- | --- | --- | --- |
| [ConditionBossGauge](Assets/TcgEngine/Scripts/Conditions/ConditionBossGauge.cs) | `gauge: BossGaugeType`; `oper: ConditionOperatorInt`; `value: int`; `compare_to_max: bool` | `IsTriggerConditionMet()`<br>`IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Player)`<br>`IsTargetConditionMet(Slot)` | 모든 오버라이드가 동일 게이지를 검사한다. boss_state가 없으면 false. 현재값을 compare_to_max에 따라 게이지 최대값 또는 value와 비교한다. |
| [ConditionCanPlace](Assets/TcgEngine/Scripts/Conditions/ConditionCanPlace.cs) | `purpose: BoardDestinationPurpose`; `last_type: ConditionLastType`; `place_card: CardData`; `card_owner: ConditionPlayerType`; `oper: ConditionOperatorBool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Player)`<br>`IsTargetConditionMet(Slot)`<br>`IsTargetConditionMet(Card selected, Slot)` | Card는 해당 위치의 Slot 검사로 전달한다. 이동/배치 목적에 따라 CanMoveCard/CanPlaceCard를 호출한다. 최근 지정·선택 원인은 문맥의 UID로 조회하고 누락 시 다른 카드로 대체하지 않는다. 명시 CardData는 Placement와 Self/Opponent만 지원하며 인스턴스 생성 없이 검사한다. Player는 false다. |
| [ConditionCardData](Assets/TcgEngine/Scripts/Conditions/ConditionCardData.cs) | `card_types: List<CardData>`; `oper: ConditionOperatorBool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(CardData)`<br>`IsTargetConditionMet(Player)`<br>`IsTargetConditionMet(Slot)` | Card.card_id 또는 CardData.id가 card_types의 id 중 하나인지 검사하고 oper를 적용한다. 빈 목록은 포함되지 않음으로 판정한다. Player/Slot은 항상 false다. |
| [ConditionCardPile](Assets/TcgEngine/Scripts/Conditions/ConditionCardPile.cs) | `type: PileType`; `oper: ConditionOperatorBool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Player)`<br>`IsTargetConditionMet(Slot)` | Card는 Club/Hand/Board/Equipped/Deck/Discard/Secret/Temp 소속 여부에 oper를 적용하며 null 및 미지원 영역은 false다. Player는 false. Slot은 type=Board이고 Slot.None이 아니면 true이며 oper와 실제 점유는 검사하지 않는다. |
| [ConditionCardType](Assets/TcgEngine/Scripts/Conditions/ConditionCardType.cs) | `has_type: List<CardType>`; `has_club: List<ClubData>`; `has_trait: List<TraitData>`; `oper: ConditionOperatorBool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Player)`<br>`IsTargetConditionMet(Slot)`<br>`IsTargetConditionMet(CardData)` | 각 분류 목록 내부는 OR, 목록 간은 AND이며 빈 목록은 제한이 없다. Card는 결과에 oper를 적용한다. Slot은 점유 카드의 Card 판정으로 전달하며 빈 타일은 false다. Player는 false. CardData는 같은 분류식을 사용하지만 현재 oper를 적용하지 않는다. |
| [ConditionClubStatMatch](Assets/TcgEngine/Scripts/Conditions/ConditionClubStatMatch.cs) | `club: ClubData`; `marker: MarkerData`; `oper: ConditionOperatorInt` | `IsTriggerConditionMet()` | caster 소유자의 cards_club에서 지정 club에 속한 첫 카드를 찾고, caster와 그 카드의 Marker 값을 oper로 비교한다. club/marker 또는 일치하는 동아리 카드가 없으면 false다. |
| [ConditionCompositeOr](Assets/TcgEngine/Scripts/Conditions/ConditionCompositeOr.cs) | `any: ConditionData[]` | `Evaluate(...)`<br>`IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Player)`<br>`IsTargetConditionMet(Slot)`<br>`IsTargetConditionMet(CardData)` | 전달된 검사 역할에 맞는 각 자식의 전체 판정을 OR로 결합한다. 발동 역할은 자식별 Trigger AND Target 결과를 사용한다. 자식의 문맥·캐시 정책을 유지한다. 빈 배열은 true, null 자식만 있으면 false다. 직접 대상 오버로드는 Target 역할로 평가기에 위임한다. |
| [ConditionCount](Assets/TcgEngine/Scripts/Conditions/ConditionCount.cs) | `target: ConditionPlayerType`; `pile: PileType`; `oper: ConditionOperatorInt`; `value: int`; `has_type: List<CardType>`; `has_club: List<ClubData>`; `has_trait: List<TraitData>`; `has_card: List<CardData>` | `IsTriggerConditionMet()` | caster 기준 target 소유자 범위의 Hand/Board/Equipped/Deck/Discard/Secret/Temp에서 개수를 산정해 oper/value와 비교한다. 분류 목록 내부는 OR, 목록 간은 AND, 빈 목록은 제한 없음이다. 미지원 pile의 개수는 0이다. |
| [ConditionDamaged](Assets/TcgEngine/Scripts/Conditions/ConditionDamaged.cs) | `oper: ConditionOperatorBool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Player)`<br>`IsTargetConditionMet(Slot)` | Card는 damage가 양수인지, Player는 hp가 hp_max 미만인지 검사해 oper를 적용한다. Slot은 false다. |
| [ConditionDeathEvent](Assets/TcgEngine/Scripts/Conditions/ConditionDeathEvent.cs) | `causes: DeathCause[]`; `victim_conditions: ConditionData[]`; `killer_conditions: ConditionData[]` | `IsTriggerConditionMet()` | DeathEventContext가 있어야 한다. causes가 null/비어 있으면 사망 원인을 제한하지 않는다. victim_uid/killer_uid로 현재 Game의 카드를 조회해 각 조건 배열의 Trigger와 Card 판정을 AND로 결합하고 동일 문맥을 전달한다. 유효한 자식 조건이 있는데 해당 카드가 없으면 false; null 자식은 건너뛴다. |
| [ConditionDeckbuilding](Assets/TcgEngine/Scripts/Conditions/ConditionDeckbuilding.cs) | `oper: ConditionOperatorBool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(CardData)` | Card의 CardData 또는 전달된 CardData의 deckbuilding 값을 oper로 검사한다. |
| [ConditionEquipped](Assets/TcgEngine/Scripts/Conditions/ConditionEquipped.cs) | `oper: ConditionOperatorBool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Player)`<br>`IsTargetConditionMet(Slot)` | Card.equipped_uid가 null이 아닌지 검사해 oper를 적용한다. 빈 문자열과 실제 장비 존재는 따로 검증하지 않는다. Player/Slot은 false다. |
| [ConditionExhaust](Assets/TcgEngine/Scripts/Conditions/ConditionExhaust.cs) | `oper: ConditionOperatorBool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Player)`<br>`IsTargetConditionMet(Slot)` | Card.exhausted에 oper를 적용한다. Player/Slot은 false에 oper를 적용하므로 IsFalse 설정에서는 통과한다. |
| [ConditionLastTypeExist](Assets/TcgEngine/Scripts/Conditions/ConditionLastTypeExist.cs) | `type: ConditionLastType`; `oper: ConditionOperatorBool` | `IsTriggerConditionMet()` | 현재 LastSelected만 지원하며 last_selected가 빈 문자열이 아닌지 oper로 검사한다. 다른 type은 false다. UID에 해당하는 카드의 실제 존재는 검사하지 않는다. |
| [ConditionLastTypeRange](Assets/TcgEngine/Scripts/Conditions/ConditionLastTypeRange.cs) | `type: ConditionLastType`; `range: int`; `oper: ConditionOperatorBool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Slot)`<br>`IsTargetConditionMet(Player)` | Card의 LastPlayed는 실행 스냅샷의 last_played와 target.uid의 동일 여부에 oper를 적용하고 나머지는 Slot 판정으로 전달한다. Slot은 실행 스냅샷의 최근 공격·지정·소환·파괴 위치의 GetNeighborSlot(range) 포함 여부에 oper를 적용한다. 플레이어 공격 기록은 이웃 기준에서 제외한다. Slot의 LastPlayed와 Player는 false; None/LastSelected는 기본 false에 oper를 적용한다. |
| [ConditionNone](Assets/TcgEngine/Scripts/Conditions/ConditionNone.cs) | 추가 필드 없음 | `IsTargetConditionMet(Slot)`<br>`IsTargetConditionMet(Slot selected, Slot)` | 두 Slot 오버라이드는 항상 true다. 다른 경로도 기본 구현을 상속하므로 제한을 추가하지 않는다. |
| [ConditionOnce](Assets/TcgEngine/Scripts/Conditions/ConditionOnce.cs) | 추가 필드 없음 | `IsTriggerConditionMet()` | Game.ability_played에 ability.id가 없으면 true다. 제한 키는 caster UID가 아닌 ability.id이므로 같은 ID의 복수 카드가 기록을 공유한다. 기록 초기화 경계까지 제한한다. |
| [ConditionOwner](Assets/TcgEngine/Scripts/Conditions/ConditionOwner.cs) | `oper: ConditionOperatorBool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Player)`<br>`IsTargetConditionMet(Slot)`<br>`IsTargetConditionMet(Slot selected, Slot)` | Card/Player는 caster와 소유자 ID가 같은지 oper로 검사한다. Slot 단독은 점유 카드로 전달하고 빈 타일은 false다. (Slot selected, Slot)은 selected를 사용하지 않고 Slot.GetP(caster.player_id)와 target.p를 비교한다. |
| [ConditionOwnerAI](Assets/TcgEngine/Scripts/Conditions/ConditionOwnerAI.cs) | `oper: ConditionOperatorBool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Player)`<br>`IsTargetConditionMet(Slot)` | caster 소유자가 AI가 아니면 항상 true다. AI일 때 Card/Player의 소유자, Slot의 진영을 caster와 비교해 oper를 적용한다. Slot은 점유 카드의 소유자를 조회하지 않는다. |
| [ConditionPilePosition](Assets/TcgEngine/Scripts/Conditions/ConditionPilePosition.cs) | `pile: PileType`; `mode: PosMode`; `index: int`; `oper: ConditionOperatorBool` | `IsTargetConditionMet(Card)` | target 소유자의 지정 영역에서 객체의 인덱스를 찾는다. Top은 선두, Bottom은 끝, Index는 index와 비교하여 oper를 적용한다. Deck/Discard/Hand/Board/Secret/Temp/Equipped/Attached/Club/PlayerAbility를 지원한다. null target·미지원 pile·목록에 없는 target은 false에 oper를 적용한다. |
| [ConditionPlayerStat](Assets/TcgEngine/Scripts/Conditions/ConditionPlayerStat.cs) | `type: ConditionStatType`; `oper: ConditionOperatorInt`; `value: int` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Player)` | Card는 그 카드 소유자의 Player 판정으로 전달한다. Player의 HP 또는 Mana를 value와 비교한다. 그 외 type은 false다. |
| [ConditionPossibility](Assets/TcgEngine/Scripts/Conditions/ConditionPossibility.cs) | `possibility: float` | `Evaluate(...)` | 대상형과 검사 역할에 공통인 확률 판정이다. 공통 평가기가 evaluation 정책에 따라 결과를 재검사하거나 재사용한다. 실제 실행은 게임 난수와 possibility를 비교한다. 조회는 캐시가 있으면 그 결과, 없으면 양의 확률 여부를 사용하며 난수를 소비하지 않는다. 입력 범위는 [0, 1]이며 양 끝값은 추첨 없이 확정한다. |
| [ConditionRolled](Assets/TcgEngine/Scripts/Conditions/ConditionRolled.cs) | `oper: ConditionOperatorInt`; `value: int` | `IsTriggerConditionMet()`<br>`IsTargetConditionMet(Player)`<br>`IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Slot)` | 모든 오버라이드가 대상과 무관하게 Game.rolled_value를 oper/value와 비교한다. 새 난수를 생성하지 않는다. |
| [ConditionSelf](Assets/TcgEngine/Scripts/Conditions/ConditionSelf.cs) | `oper: ConditionOperatorBool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Player)`<br>`IsTargetConditionMet(Slot)` | Card는 caster와 객체 동일성, Player는 caster의 소유자 ID, Slot은 caster.slot과의 동일성을 oper로 검사한다. AbilityTarget.Self의 반환 규격과 별개로, 전달된 후보에 대한 조건이다. |
| [ConditionSlotAttachmentEmpty](Assets/TcgEngine/Scripts/Conditions/ConditionSlotAttachmentEmpty.cs) | `oper: ConditionOperatorBool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Player)`<br>`IsTargetConditionMet(Slot)` | Slot은 GetAttachCard(target)가 없는지 oper로 검사한다. Card/Player는 false에 oper를 적용한다. |
| [ConditionSlotDist](Assets/TcgEngine/Scripts/Conditions/ConditionSlotDist.cs) | `distance: int`; `diagonals: bool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Slot)` | Card는 target.slot으로 전달한다. Slot은 caster.slot 기준으로 diagonals에 따라 IsInDistance 또는 IsInDistanceStraight를 호출하고 distance를 적용한다. |
| [ConditionSlotLocate](Assets/TcgEngine/Scripts/Conditions/ConditionSlotLocate.cs) | `Inside: bool`; `Outside: bool`; `Neutral: bool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Slot)` | Card는 target.slot으로 전달한다. Slot은 Inside일 때 양측 내부 타일, Outside일 때 양측 내부도 중립 행도 아닌 위치, Neutral일 때 중립 행을 OR로 허용한다. 현재 중립 행 판정은 y=3이다. |
| [ConditionSlotNeighbor](Assets/TcgEngine/Scripts/Conditions/ConditionSlotNeighbor.cs) | `range: int` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Slot)` | Card는 target.slot으로 전달한다. Slot은 caster.slot.GetNeighborSlot(range)에 포함되는지 검사한다. |
| [ConditionSlotPid](Assets/TcgEngine/Scripts/Conditions/ConditionSlotPid.cs) | `player: bool`; `opponent: bool`; `neutral: bool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Slot)` | Card는 target.slot으로 전달한다. Slot의 p를 caster 소유자 ID·상대 ID·중립값과 비교해 켜진 항목을 OR로 허용한다. 현재 중립값은 2이며 Slot.GetP 변환 없이 player_id와 직접 비교한다. |
| [ConditionSlotRange](Assets/TcgEngine/Scripts/Conditions/ConditionSlotRange.cs) | `oper: ConditionOperatorBool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Slot)` | Card는 target.slot으로 전달한다. Slot은 caster.slot.GetNeighborSlot(caster.GetRange()) 포함 여부에 oper를 적용한다. |
| [ConditionSlotUnitEmpty](Assets/TcgEngine/Scripts/Conditions/ConditionSlotUnitEmpty.cs) | `oper: ConditionOperatorBool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Player)`<br>`IsTargetConditionMet(Slot)` | Slot은 GetSlotCard(target)가 없는지 oper로 검사한다. Card/Player는 false에 oper를 적용한다. |
| [ConditionStat](Assets/TcgEngine/Scripts/Conditions/ConditionStat.cs) | `type: ConditionStatType`; `oper: ConditionOperatorInt`; `value: int` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Player)` | Card는 현재 Attack/HP/Mana, Player는 HP/Mana를 oper/value와 비교한다. 각 경로가 처리하지 않는 type은 false다. |
| [ConditionStatCustom](Assets/TcgEngine/Scripts/Conditions/ConditionStatCustom.cs) | `marker: MarkerData`; `oper: ConditionOperatorInt`; `value: int` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Player)` | Card/Player의 Marker 값(영구값 + 지속값)을 oper/value와 비교한다. Marker가 없으면 0으로 비교하며 marker가 null이어도 0으로 비교한다. |
| [ConditionStatus](Assets/TcgEngine/Scripts/Conditions/ConditionStatus.cs) | `has_status: StatusType`; `value: int`; `oper: ConditionOperatorBool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Player)`<br>`IsTargetConditionMet(Slot)` | Card/Player는 has_status 보유와 상태값이 value 이상임을 함께 검사한 뒤 oper를 적용한다. Slot은 점유 카드로 전달하며 빈 타일은 oper와 무관하게 false다. |
| [ConditionTarget](Assets/TcgEngine/Scripts/Conditions/ConditionTarget.cs) | `type: ConditionTargetType`; `oper: ConditionOperatorBool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Player)`<br>`IsTargetConditionMet(Slot)`<br>`IsTargetConditionMet(Slot selected, Slot)` | Card/Player/Slot의 전달 형식과 type의 일치 여부에 oper를 적용한다. (Slot selected, Slot)도 Slot 형식만 검사한다. None을 무대상 검사로 처리하는 오버라이드는 없다. |
| [ConditionTriggered](Assets/TcgEngine/Scripts/Conditions/ConditionTriggered.cs) | `is_oper: ConditionOperatorBool` | `IsTargetConditionMet(Card)`<br>`IsTargetConditionMet(Slot)` | 실행 문맥의 trigger_uid로 카드를 조회하여 UID 또는 현재 위치를 비교한다. 문맥 없는 호출은 Game.ability_triggerer를 사용한다. 원인 누락은 비교 false로 처리한 뒤 is_oper를 적용한다. |
| [ConditionTurn](Assets/TcgEngine/Scripts/Conditions/ConditionTurn.cs) | `oper: ConditionOperatorBool` | `IsTriggerConditionMet()` | caster 소유자가 Game.current_player인지 검사해 oper를 적용한다. |
| [ConditionWideAreaRange](Assets/TcgEngine/Scripts/Conditions/ConditionWideAreaRange.cs) | `directions: List<Direction>`; `thumnail: Sprite` | `IsTargetConditionMet(Slot)`<br>`IsTargetConditionMet(Slot selected, Slot)` | directions의 상대 좌표를 기준 슬롯에 더하고 소유자 방향에 따라 두 축의 부호를 변환한다. 유효한 좌표만 후보로 삼는다. Slot 단독은 기준이 Slot.None이고, (Slot selected, Slot)은 selected를 기준으로 한다. 빈 directions는 false이며 null 목록은 보호하지 않는다. thumnail은 판정에 사용하지 않는다. Direction의 필드는 int dx/dy다. |

#### 6.2.3 ENUM 정의

| ENUM | 정의 및 용도 |
| --- | --- |
| ConditionEvaluationPolicy | Revalidate=0: 상태 재검사. EvaluateOnce=1: 문맥별 판정 재사용. 기본 정책은 Revalidate다. |
| ConditionStage | Trigger / Criteria / Target / Range / Effect. 공통 평가기의 검사 역할이며 Inspector 대상 분류와 구분한다. |
| BoardDestinationPurpose | Placement=0 / Movement=1. 공통 점유·지형 검사를 공유하되 진영 정책을 독립적으로 확장할 수 있는 목적 구분이다. |
| ConditionTargetType | None=0, Card=10, Player=20, Slot=30. 판정에 전달된 대상형. |
| ConditionPlayerType | Self=0, Opponent=1, Both=2. 소유자 범위. |
| ConditionOperatorBool | IsTrue=0, IsFalse=1. 판정 유지/반전. |
| ConditionOperatorInt | Equal=0, NotEqual=1, GreaterEqual=2, LessEqual=3, Greater=4, Less=5. 수치 비교 연산. |
| ConditionStatType | None=0, Attack=10, HP=20, Mana=30. 비교할 기본 수치. |
| ConditionLastType | None=0, LastAttacked=1, LastTargeted=2, LastSummoned=3, LastDestroyed=4, LastPlayed=5, LastSelected=6. 조회할 최근 행동 기록. |
| ConditionPilePosition.PosMode | Top=0, Bottom=1, Index=2. 영역 안의 위치 지정 방식. |

CardType·PileType은 6.1.2의 분류를 공유한다. StatusType·DeathCause·BossGaugeType처럼 다른 도메인이 소유하는 ENUM은 해당 정의와 실제 소비 경로를 함께 확인한다. 이름이 유사한 ENUM 간에 직렬화 값을 대입하지 않는다.

#### 6.2.4 검사 정책과 수명

| 정책 | 평가 시점 | 결과의 수명 |
| --- | --- | --- |
| Revalidate | 해당 실행 경로가 조건을 호출할 때마다 | 호출 시점의 상태로 재판정한다. 최초 실패한 발동은 등록하지 않는다. |
| EvaluateOnce | 실제 실행 문맥에서 해당 조건을 처음 평가할 때 | 능력 실행·Condition 정의·검사 역할·대상형/식별자·범위 기준이 같은 검사에서 성공/실패 결과를 재사용한다. 대상 재선정이 있는 반복은 회차를 구분한다. |

- 같은 Condition 에셋을 같은 역할·대상에 재사용하면 판정 결과도 공유한다. 독립 판정이 필요한 경우 독립된 Condition 정의 또는 Revalidate 정책으로 표현한다.
- Condition 데이터 테이블 내보내기는 evaluation을 원래 조건 행에 유지한다. CanPlace의 purpose와 대응 ENUM도 함께 내보낸다.
- 조건 정책은 모든 Condition에 공통으로 적용한다. 클래스나 카드 ID별 예외로 평가 횟수를 제어하지 않는다.
- 조회와 실제 판정을 분리한다. UI·가능 대상 검색·AI 후보 조회는 난수와 실행 상태를 변경하지 않으며, 실제 실행 또는 AI 분기 시뮬레이션이 판정을 확정한다.
- 평가 결과의 재사용은 대상의 존재·영역·선택 유효성·시전 가능 상태 검사를 면제하지 않는다. 취소·종료·문맥 교체 시 해당 결과를 정리한다.
- 지속 재계산은 일반 능력 실행과 별도 수명이며 AbilityExecutionContext의 실행별 캐시를 사용하지 않는다.

#### 6.2.5 제작 및 검증 규칙

- 조건은 사용하는 배치 위치와 대상형의 조합으로 검증한다. 같은 조건이 발동 검사와 대상 검사에서 같은 객체를 받는다고 가정하지 않는다.
- 복합 조건은 자식의 문맥·평가 정책을 보존한다. 단락 평가로 호출되지 않은 자식은 평가한 것으로 기록하지 않는다.
- 빈 목록·없는 참조·경계값·무효 대상·상태 변경·선택 재개·반복 회차에 대한 결과를 명시한다. 조회 횟수와 실제 발동 확률이 서로 영향을 주지 않아야 한다.

### 6.3 EffectData 규격 및 규칙

#### 6.3.1 실행 입력과 대상형

EffectData 기본형에는 공통 직렬화 필드가 없다. 공통 수치와 기간은 AbilityData가 전달하고 효과별 설정은 파생 ScriptableObject가 정의한다.

| 실행 경로 | 입력 | 규칙 |
| --- | --- | --- |
| DoEffect | GameLogic, AbilityData, caster, context 및 선택적 대상 | 일반 효과를 실행한다. 무대상·Card·List<Card>·Player·Slot·CardData 오버로드를 구분한다. |
| DoOngoingEffect | GameLogic, AbilityData, caster, Card 또는 Player | 지속 상태 재계산에 기여한다. 일반 효과와 별도의 구현 경로다. |
| AbilityData.value / duration | 정수 수치 / 기간 | 각 파생형이 소비하는 단위·부호·수량·계산식·기간 의미를 정의한다. 모든 효과가 두 값을 모두 사용하는 것은 아니다. |
| AbilityEventContext | 이벤트 당시의 실행 정보 | 필요로 하는 문맥 유형과 누락 시 동작을 정의한다. 중첩 효과에도 문맥을 전달한다. |

현재 context 오버로드의 기본 구현은 문맥 없는 오버로드로 전달하며, 일반·지속 기본 구현은 무동작이다. 지원 여부는 메서드 존재가 아니라 실제 오버라이드와 호출 경로로 확인한다. List<Card> 경로가 개별 Card 실행을 자동 대체한다고 가정하지 않는다.

#### 6.3.2 전체 파생 클래스의 필드·오버라이드·ENUM

아래 표는 현재 EffectData의 모든 직접·간접 파생 클래스를 조사한 결과다. 런타임 제작용 클래스는 49개다. EffectConditionalCaster는 EffectConditional을 상속하고 나머지는 EffectData를 직접 상속한다. 클래스 이름을 기준으로 기록하며 파일명과 다른 경우도 소스 링크로 구분한다.

- 메서드 표기에서 공통 인자 `GameLogic logic, AbilityData ability, Card caster`는 생략한다. `DoEffect()`는 무대상, 괄호의 형식은 실제 수신자, `DoOngoingEffect(...)`는 지속 실행이다.
- EffectConditional과 EffectMoveUnit은 문맥 포함 오버라이드를 제공한다. EffectConditionalCaster는 부모의 실행 오버로드를 상속한다. 다른 context 호출은 기본 클래스가 문맥 없는 오버라이드로 전달한다. DoEffect(List<Card>)의 제작용 오버라이드는 없으며 기본 무동작이다. 표에 없는 일반·지속 오버라이드도 상속 경로를 확인한다.
- 표의 현재 동작과 6.3의 공통 제작 규칙을 구분한다. null 검사·용량 검사·입력 보정이 명시되지 않은 경로에는 해당 보호가 있다고 가정하지 않는다. 하위 GameLogic 호출의 사망·이벤트·큐 처리는 그 API의 규칙을 따른다.
- AddStatCount·DamageCount·StoreCount의 조건부 개수는 caster 기준 player 범위의 Hand/Board/Equipped/Deck/Discard/Secret/Temp에서 산정한다. has_type=None 및 null has_club/has_trait는 각각 제한 없음이고 나머지 조건은 AND다. 미지원 pile의 개수는 0이다.
- Damage/DamageCount의 Slot 경로는 GameLogic.ApplyTileEffect로 점유 카드, 부착물 반응, 실내 진영의 Player를 순서대로 처리한다. 부착물에는 기존 공격자의 OnAfterDamage 반응을 등록한다. DamageRatio와 Heal의 Slot 경로도 같은 래퍼를 사용하며 부착물 반응은 등록하지 않는다.

| 클래스 / 소스 | Inspector 필드 : 형식 | 선언된 오버라이드 | 현재 효과 및 입력 규칙 |
| --- | --- | --- | --- |
| [EffectAddAbility](Assets/TcgEngine/Scripts/Effects/EffectAddAbility.cs) | `gain_ability: AbilityData` | `DoEffect(Card)`<br>`DoOngoingEffect(Card)` | Card에 gain_ability를 AddAbility로 부여한다. 지속 경로는 AddOngoingAbility를 사용한다. ability.value/duration은 사용하지 않는다. |
| [EffectAddClub](Assets/TcgEngine/Scripts/Effects/EffectAddClub.cs) | `club: ClubData` | `DoEffect(Card)` | 현재 club 필드는 사용하지 않는다. target.card_id를 동아리 ID로 간주하여 logic.AddClub(caster, target.card_id)를 호출하므로 변경 대상은 caster다. 유효한 신규 가입은 지속 재계산과 OnAddClubOther를 유발한다. |
| [EffectAddMarker](Assets/TcgEngine/Scripts/Effects/EffectAddMarker.cs) | `marker: MarkerData` | `DoEffect(Player)`<br>`DoEffect(Card)`<br>`DoOngoingEffect(Card)`<br>`DoOngoingEffect(Player)` | Card/Player의 Marker에 ability.value를 AddMarker로 가산한다. 지속 Card/Player는 AddOngoingMarker를 사용한다. marker 참조는 필수다. |
| [EffectAddStat](Assets/TcgEngine/Scripts/Effects/EffectAddStat.cs) | `type: EffectStatType`; `stored_value: MarkerData` | `DoEffect(Player)`<br>`DoEffect(Card)`<br>`DoOngoingEffect(Card)` | 적용값은 ability.value에 stored_value가 지정된 경우 caster의 해당 Marker 값을 더한 값이다. Card의 Attack/HP/Mana/Range를 가산하고 지속 Card는 대응 ongoing 필드를 가산한다. Player는 HP와 최대 HP, 또는 Mana와 최대 Mana를 가산한다. 플레이어 마나는 0 이상, 최대 마나는 게임 상한 안으로 보정한다. 저장값을 소비·제거하지 않는다. |
| [EffectAddStatCount](Assets/TcgEngine/Scripts/Effects/EffectAddStatCount.cs) | `type: EffectStatType`; `pile: PileType`; `player: EffectPlayerType`; `has_type: CardType`; `has_club: ClubData`; `has_trait: TraitData` | `DoEffect(Player)`<br>`DoEffect(Card)`<br>`DoOngoingEffect(Card)` | 적용값은 조건부 개수 × ability.value다. Card 및 지속 Card는 Attack/HP/Mana/Range 또는 대응 ongoing 필드를 가산한다. Player Mana/최대 Mana는 계산값을 가산·보정한다. 현재 Player HP는 계산값을, 최대 HP는 개수와 무관한 ability.value만 가산한다. |
| [EffectAddStatRoll](Assets/TcgEngine/Scripts/Effects/EffectAddStatRoll.cs) | `type: EffectStatType` | `DoEffect(Player)`<br>`DoEffect(Card)` | Game.rolled_value를 Card의 Attack/HP/Mana 또는 Player의 HP/최대 HP·Mana/최대 Mana에 가산한다. 플레이어 마나 하한·최대 마나 상한을 보정한다. Range·지속 효과는 지원하지 않으며 ability.value는 사용하지 않는다. |
| [EffectAddStatTotalCount](Assets/TcgEngine/Scripts/Effects/EffectAddStatTotalCount.cs) | `stat_type: EffectStatType`; `total_type: EffectTotalCountType` | `DoEffect(Player)`<br>`DoEffect(Card)`<br>`DoOngoingEffect(Card)` | TotalHeal이면 caster 소유자의 total_heal, 그 외에는 0을 사용한다. Card의 Attack/HP/Mana/Range 및 지속 ongoing 필드에 가산한다. Player는 HP/최대 HP 또는 Mana/최대 Mana를 가산·보정한다. ability.value를 곱하지 않는다. |
| [EffectAttachCard](Assets/TcgEngine/Scripts/Effects/EffectAttach.cs) | `attach: CardData` | `DoEffect(Slot)` | attach와 caster의 VariantData로 caster 소유 카드를 생성하고 대상 Slot에 AttachCard를 호출한다. 파일명은 EffectAttach.cs다. 실제 부착 규칙과 기존 부착물 교체는 GameLogic에 위임한다. |
| [EffectAttack](Assets/TcgEngine/Scripts/Effects/EffectAttack.cs) | `attacker_type: EffectActionType` | `DoEffect(Player)`<br>`DoEffect(Card)` | Player는 attacker_type으로 찾은 공격자가 있을 때 AttackPlayer(attacker, target, true)를 호출한다. Card 오버라이드는 비어 있다. 공격자 조회는 Self/AbilityTriggerer/LastPlayed/LastTargeted를 지원하며 LastSelected는 null이다. LastTargeted는 Game.last_target을 사용한다. |
| [EffectAttackRedirect](Assets/TcgEngine/Scripts/Effects/EffectAttackRedirect.cs) | `attacker_type: EffectActionType` | `DoEffect(Player)`<br>`DoEffect(Card)` | Card/Player에 대해 조회한 공격자의 RedirectAttack을 호출한다. 공격자가 없으면 무동작. 조회는 Self/AbilityTriggerer/LastPlayed/LastTargeted를 지원하며 LastSelected는 null이다. LastTargeted는 Game.last_target을 사용한다. |
| [EffectBossGauge](Assets/TcgEngine/Scripts/Effects/EffectBossGauge.cs) | `gauge: BossGaugeType`; `set_to_value: bool`; `delta: int`; `set_value: int` | `DoEffect()`<br>`DoEffect(Card)`<br>`DoEffect(Player)`<br>`DoEffect(Slot)` | 대상과 무관하게 boss_state의 gauge를 set_value로 대입하거나 delta만큼 가산한다. boss_state가 없으면 무동작. ability.value/duration은 사용하지 않는다. |
| [EffectChangeOwner](Assets/TcgEngine/Scripts/Effects/EffectChangeOwner.cs) | `owner_opponent: bool` | `DoEffect(Card)` | Card를 caster 소유자 또는 owner_opponent로 선택한 상대 소유자로 ChangeOwner한다. |
| [EffectChangeWeapon](Assets/TcgEngine/Scripts/Effects/EffectChangeWeapon.cs) | `weapon: WeaponData` | `DoEffect(Card)`<br>`DoEffect(Slot)` | Card에 weapon.GetWeaponID()를 SetWeapon한다. Slot은 점유 카드가 있을 때 Card 경로로 전달하며 빈 타일은 무동작이다. weapon 참조는 필수다. |
| [EffectClearStatus](Assets/TcgEngine/Scripts/Effects/EffectClearStatus.cs) | `status: StatusData`; `status_type: EffectStatusType` | `DoEffect(Player)`<br>`DoEffect(Card)` | Card/Player에서 명시 status가 있으면 해당 상태를 제거한다. 없으면 StatusData.bad_status와 status_type으로 분류를 골라 제거한다. 상태 정의 조회가 가능해야 한다. |
| [EffectClearTemp](Assets/TcgEngine/Scripts/Effects/EffectClearTemp.cs) | 추가 필드 없음 | `DoEffect()`<br>`DoEffect(Card)` | 무대상과 Card 모두 caster 소유자의 cards_temp를 비운다. 전달된 Card와 ability.value는 사용하지 않는다. Discard나 사망 처리 메서드를 호출하지 않는다. |
| [EffectConditional](Assets/TcgEngine/Scripts/Effects/EffectConditional.cs) | `condition: ConditionData`; `effects_true: EffectData[]`; `effects_false: EffectData[]` | `DoEffect(Card/Player/Slot/CardData)` 및 각 문맥 포함 오버로드<br>`GetConditionTarget(...)` | 공통 평가기의 Effect 역할로 수신자 조건을 검사하고 선택한 분기를 같은 대상과 문맥으로 실행한다. 문맥 없는 오버로드는 문맥 포함 경로에 위임한다. 조건 없음은 참 분기, 빈 분기는 무동작이다. |
| [EffectConditionalCaster](Assets/TcgEngine/Scripts/Effects/EffectConditionalCaster.cs) | EffectConditional 필드 상속 | `GetConditionTarget(...)` | 검사 대상만 caster로 바꾸며 선택한 효과는 원래 수신자에게 적용한다. 나머지 실행·문맥 전달·분기 처리는 EffectConditional에서 상속한다. |
| [EffectCopyStat](Assets/TcgEngine/Scripts/Effects/EffectCopyStat.cs) | `marker: MarkerData`; `require_club: ClubData`; `only_if_missing: bool` | `DoEffect(Card)` | caster의 Marker 값을 Card에 SetMarker한다. marker/target이 없거나 require_club에 불일치하면 무동작. only_if_missing일 때 대상에 영구 Marker 객체가 있으면 값과 무관하게 건너뛴다. |
| [EffectCreate](Assets/TcgEngine/Scripts/Effects/EffectCreate.cs) | `create_pile: PileType`; `create_opponent: bool` | `DoEffect(CardData)`<br>`DoEffect(Card)` | 대상 정의 또는 Card의 정의로 카드 한 장을 생성한다. 변경 수치와 ability.value는 복사/수량에 사용하지 않는다. 생성·등록·목적지 배치는 CreateCardToPile에 위임한다. Deck/Discard/Hand/Temp/PlayerAbility를 지원하며 손패 초과는 Discard, 미지원 영역은 생성 전에 오류 처리한다. |
| [EffectCreateCard](Assets/TcgEngine/Scripts/Effects/EffectCreateCard.cs) | `create_card: List<WeightedCard>`; `create_pile: PileType`; `is_same_possibility: bool`; `create_opponent: bool` | `DoEffect()` | 생성 수량은 max(1, ability.value)이며 매 생성마다 후보를 다시 추첨한다. 균등 방식은 목록 인덱스, 가중 방식은 [0,1) 난수와 weight 누적합을 사용한다. 자동 정규화 없이 미선택 시 첫 후보로 폴백하므로 유효 후보·비음수 가중값·완전한 확률 분포가 필요하다. CreateCardToPile에 생성 결과 처리를 위임한다. 지원 영역·초과 처리·등록 규칙은 EffectCreate와 같다. 빈 후보 목록은 보호하지 않는다. WeightedCard의 필드는 CardData card와 float weight다. |
| [EffectCycleStat](Assets/TcgEngine/Scripts/Effects/EffectCycleStat.cs) | `marker: MarkerData` | `DoEffect()`<br>`DoEffect(Card)` | 무대상과 Card 모두 caster의 영구 Marker를 (현재값 + 1) % ability.value로 갱신한다. Marker가 없으면 현재값을 0으로 사용한다. marker가 없거나 주기가 양수가 아니면 무동작이며 전달된 Card는 갱신하지 않는다. |
| [EffectDamage](Assets/TcgEngine/Scripts/Effects/EffectDamage.cs) | `value_type: EffectValueType`; `bonus_damage: MarkerData`; `stored_slot: MarkerData` | `DoEffect()`<br>`DoEffect(Player)`<br>`DoEffect(Card)`<br>`DoEffect(Slot)` | 피해는 value_type으로 고른 ability.value/caster 공격력/현재 HP에 caster와 소유 Player의 bonus_damage Marker 값을 더한다. Card/Player는 해당 이벤트 피해 경로로, Slot은 공통 타일 래퍼로 전달한다. 무대상은 caster의 stored_slot Marker 값이 양수일 때 Slot.FromMarkerCode로 위치를 복원하여 같은 Slot 경로를 호출한다. stored_slot 미지정·저장 코드 없음·무효 위치는 무동작이다. 빈 실내도 Player에게 적용하며 저장값 소비는 별도 연결 효과의 책임이다. |
| [EffectDamageCount](Assets/TcgEngine/Scripts/Effects/EffectDamageCount.cs) | `bonus_damage: MarkerData`; `pile: PileType`; `player: EffectPlayerType`; `has_type: CardType`; `has_club: ClubData`; `has_trait: TraitData` | `DoEffect(Player)`<br>`DoEffect(Card)`<br>`DoEffect(Slot)` | 조건부 개수 × ability.value에 caster와 소유 Player의 bonus_damage Marker 값을 더한다. Card/Player는 각각 이벤트 피해 경로, Slot은 산정한 값을 공통 타일 래퍼로 전달한다. |
| [EffectDamageRatio](Assets/TcgEngine/Scripts/Effects/EffectDamageRatio.cs) | `bonus_damage: MarkerData`; `ratio: float` | `DoEffect(Player)`<br>`DoEffect(Card)`<br>`DoEffect(Slot)` | Card는 floor(현재 HP × ratio)를 DamageCard_Event로, Player는 floor(hp × ratio)를 DamagePlayer_Event로 전달한다. 두 경로 모두 NumericCause.Ability이며 공격으로 분류하지 않는다. Slot은 공통 타일 래퍼를 사용해 각 Card/Player의 현재 HP로 각각 계산한다. bonus_damage 및 ability.value는 사용하지 않는다. |
| [EffectDestroy](Assets/TcgEngine/Scripts/Effects/EffectDestroy.cs) | 추가 필드 없음 | `DoEffect(Card)`<br>`DoEffect(Slot)` | 보드 Card는 KillCard로 사망 대기 처리하고 다른 영역은 DiscardCard로 보낸다. Slot은 점유 카드가 있을 때 Card 경로로 전달한다. 빈 타일은 무동작이다. |
| [EffectDestroyEquip](Assets/TcgEngine/Scripts/Effects/EffectDestroyEquip.cs) | 추가 필드 없음 | `DoEffect(Card)` | 대상이 장비면 그 카드를, 아니면 equipped_uid로 조회한 장비를 DiscardCard에 전달한다. 조회 실패 처리는 DiscardCard의 null 보호를 따른다. |
| [EffectDiscard](Assets/TcgEngine/Scripts/Effects/EffectDiscard.cs) | 추가 필드 없음 | `DoEffect(Player)`<br>`DoEffect(Card)` | Card는 DiscardCard로 처리한다. Player는 DrawDiscardCard(target, ability.value)를 호출하여 덱 선두에서 버림 영역으로 옮긴다. 손패 버리기와 구분하며 덱이 비면 남은 수량은 처리하지 않는다. |
| [EffectDraw](Assets/TcgEngine/Scripts/Effects/EffectDraw.cs) | 추가 필드 없음 | `DoEffect(Player)`<br>`DoEffect(Card)` | Player 또는 Card의 소유자에게 ability.value를 수량으로 DrawCard를 호출한다. 드로우·손패 용량·덱 소진 규칙은 GameLogic에 위임한다. |
| [EffectExhaust](Assets/TcgEngine/Scripts/Effects/EffectExhaust.cs) | `exhausted: bool` | `DoEffect(Card)` | Card.exhausted를 설정값 exhausted로 대입한다. |
| [EffectHeal](Assets/TcgEngine/Scripts/Effects/EffectHeal.cs) | `heal_type: EffectValueType`; `bonus_heal: MarkerData` | `DoEffect(Player)`<br>`DoEffect(Card)`<br>`DoEffect(Slot)` | heal_type으로 고른 ability.value/caster 공격력/현재 HP에 caster와 소유 Player의 bonus_heal Marker 값을 더한다. Card/Player에 HealCard/HealPlayer를 호출한다. Slot은 계산한 기본 회복량을 공통 타일 래퍼로 전달한다. 점유 카드와 실내 진영 Player는 각자의 수치 보정과 회복 제한을 적용한다. 요청 회복량과 실제 회복량·이벤트는 GameLogic의 회복 규칙을 따른다. |
| [EffectKnockback](Assets/TcgEngine/Scripts/Effects/EffectKnockback.cs) | 추가 필드 없음 | `DoEffect(Card)` | caster와 target이 존재하고 ability.value가 양수일 때 caster→target 방향으로 최대 해당 칸 수만큼 빈 타일을 탐색한다. 현재 축 조건은 vy=0 또는 vx=0 또는 vx=vy이며 동일 위치는 제외한다. 각 경유 후보를 CanMoveCard(..., true)로 검사하여 최초 불허 위치 앞에서 멈추고 최종 후보가 같은 검사를 통과하면 MoveCard(..., true)한다. |
| [EffectMana](Assets/TcgEngine/Scripts/Effects/EffectMana.cs) | 추가 필드 없음 | `DoEffect(Player)` | Player.mana에 ability.value를 가산하고 0 이상으로 보정한다. 최대 마나는 변경하지 않으며 현재 마나의 상한도 보정하지 않는다. |
| [EffectMovePileTopToBottom](Assets/TcgEngine/Scripts/Effects/EffectMovePileTopToBottom.cs) | `pile: PileType` | `DoEffect()`<br>`DoEffect(Card)` | 무대상과 Card 모두 caster 소유자의 Deck/Discard/Hand 선두 카드를 끝으로 옮긴다. 미지원 pile 또는 항목 수가 순환에 부족하면 무동작이다. 전달 Card와 ability.value는 사용하지 않는다. |
| [EffectMoveUnit](Assets/TcgEngine/Scripts/Effects/EffectMove.cs) | `target_type: EffectActionType` | `DoEffect(Slot)`<br>`DoEffect(Slot, context)` | Self/AbilityTriggerer/LastPlayed/LastTargeted/LastSelected 원인을 선택하여 MoveCard로 이동한다. 문맥이 있으면 원인 UID를 사용하므로 같은 위치의 교체 카드를 이동시키지 않는다. 문맥 없는 호출은 기존 Game 기록을 사용한다. 목적지·이동 가능 여부는 공통 이동 검사를 따른다. |
| [EffectPlay](Assets/TcgEngine/Scripts/Effects/EffectPlay.cs) | 추가 필드 없음 | `DoEffect(Card)` | caster 소유자의 임의 빈 Slot을 고른 뒤 대상 Card를 그 플레이어의 그룹에서 제거하고 손패에 추가한다. 빈 Slot이 있으면 PlayCard(..., true)를 호출한다. 빈 Slot이 없으면 손패에 남는다. 소유자 변경과 손패 용량 검사를 이 클래스에서 하지 않는다. |
| [EffectPlayCard](Assets/TcgEngine/Scripts/Effects/EffectPlayCard.cs) | `play_card: CardData` | `DoEffect(Slot)` | play_card를 caster 소유로 생성해 손패에 추가한 뒤 Slot이 None이 아니면 PlayCard(..., true)를 호출한다. None이면 생성된 카드가 손패에 남는다. 이 클래스의 손패 추가에는 용량 검사가 없다. |
| [EffectRemoveAbility](Assets/TcgEngine/Scripts/Effects/EffectRemoveAbility.cs) | `remove_ability: AbilityData` | `DoEffect(Card)` | Card에서 remove_ability를 RemoveAbility로 제거한다. 지속 제거 경로는 없다. |
| [EffectRemoveMarker](Assets/TcgEngine/Scripts/Effects/EffectRemoveMarker.cs) | `marker: MarkerData` | `DoEffect(Player)`<br>`DoEffect(Card)` | Card/Player에서 영구 Marker를 RemoveMarker로 제거한다. marker 참조는 필수이며 지속 제거 경로는 없다. |
| [EffectResetStat](Assets/TcgEngine/Scripts/Effects/EffectResetStat.cs) | 추가 필드 없음 | `DoEffect(Card)` | Card.SetCard(target.CardData, target.VariantData)를 호출해 같은 정의로 재설정한다. 특정 수치만 복구하는 효과가 아니며 재설정 범위는 SetCard를 따른다. |
| [EffectRoll](Assets/TcgEngine/Scripts/Effects/EffectRoll.cs) | `dice: int` | `DoEffect(Card)`<br>`DoEffect(Player)`<br>`DoEffect(Slot)` | Card/Player/Slot 대상 자체를 사용하지 않고 RollRandomValue(dice)를 호출한다. [1, dice]의 정수 결과를 Game.rolled_value에 저장하고 굴림 이벤트와 큐 지연을 발생시킨다. 다중 수신자면 호출마다 다시 굴린다. dice는 양수로 설정한다. |
| [EffectSendPile](Assets/TcgEngine/Scripts/Effects/EffectSendPile.cs) | `pile: PileType`; `insert: DeckInsert` | `DoEffect(Card)`<br>`DoEffect(Slot)` | Card를 현재 소유자의 Deck/Hand/Discard/Temp/PlayerAbility로 옮기고 Clear한다. Deck은 insert에 따라 선두/끝, Hand 초과는 Discard와 onCardDissolved, PlayerAbility는 play_order 부여를 수행한다. 미지원 pile은 무동작. Slot은 점유 카드가 있을 때 Card 경로로 전달한다. |
| [EffectSetClubCardUI](Assets/TcgEngine/Scripts/Effects/EffectSetClubCardUI.cs) | 추가 필드 없음 | `DoOngoingEffect(Card)` | 지속 Card 경로만 있다. caster의 첫 동아리를 기준으로 caster 소유 보드의 같은 동아리 카드 수를 target.mana_ongoing에 더한다. ability.value는 사용하지 않는다. 첫 동아리 항목이 있어야 하며 빈 동아리 목록은 보호하지 않는다. |
| [EffectSetStat](Assets/TcgEngine/Scripts/Effects/EffectSetStat.cs) | `type: EffectStatType` | `DoEffect(Player)`<br>`DoEffect(Card)`<br>`DoOngoingEffect(Card)` | 일반 Card의 Attack/HP/Mana를 ability.value로 대입하며 HP 설정은 damage도 초기화한다. Player는 HP 또는 Mana를 대입하고 Mana 하한만 보정한다. 지속 Card는 ongoing 필드가 아닌 기본 attack/hp/mana를 직접 대입하며 damage를 초기화하지 않는다. Range는 모든 경로에서 처리하지 않는다. |
| [EffectSetStatCustom](Assets/TcgEngine/Scripts/Effects/EffectSetStatCustom.cs) | `marker: MarkerData` | `DoEffect(Player)`<br>`DoEffect(Card)`<br>`DoEffect(Slot)`<br>`DoOngoingEffect(Player)`<br>`DoOngoingEffect(Card)` | 일반·지속 Card/Player 모두 영구 Marker를 ability.value로 SetMarker한다. 지속 전용 저장소를 사용하지 않는다. Slot은 marker가 있을 때 대상 위치의 Slot.ToMarkerCode를 caster의 Marker에 저장하며 ability.value를 사용하지 않는다. Card/Player의 marker null 보호는 없다. |
| [EffectShuffle](Assets/TcgEngine/Scripts/Effects/EffectShuffle.cs) | 추가 필드 없음 | `DoEffect(Player)` | Player.cards_deck를 ShuffleDeck으로 섞는다. |
| [EffectStoreCount](Assets/TcgEngine/Scripts/Effects/EffectStoreCount.cs) | `pile: PileType`; `player: EffectPlayerType`; `has_type: CardType`; `has_club: ClubData`; `has_trait: TraitData`; `marker: MarkerData` | `DoEffect()`<br>`DoEffect(Card)` | 조건부 개수를 무대상에서는 caster, Card에서는 target의 영구 Marker에 AddMarker로 누적한다. marker가 없으면 무동작이다. ability.value/duration은 사용하지 않는다. |
| [EffectSummonSlot](Assets/TcgEngine/Scripts/Effects/EffectSummonSlot.cs) | `position: SlotXY`; `direction: SlotXY` | `DoEffect(Card)` | Card 소유자의 진영에 position을 적용하고 CanPlayCard(..., true)를 검사한다. 실패 시 direction이 있으면 유효 좌표와 내부 탐색 상한 안에서 대체 위치를 찾는다. 가능한 위치에서만 PlayCard(..., true)한다. target=null 또는 가능한 위치가 없으면 무동작이다. |
| [EffectTransform](Assets/TcgEngine/Scripts/Effects/EffectTransform.cs) | `transform_to: CardData` | `DoEffect(Card)` | Card를 transform_to 정의로 TransformCard한다. 기존 VariantData를 유지하며 재설정과 변신 이벤트는 GameLogic에 위임한다. |
| [EffectUseCard](Assets/TcgEngine/Scripts/Effects/EffectUseCard.cs) | `use: CardData`; `use_opponent: bool` | `DoEffect()`<br>`DoEffect(Player)`<br>`DoEffect(Card)`<br>`DoEffect(Slot)`<br>`DoEffect(CardData)` | 무대상은 caster 기준 소유자와 Slot.None으로 use를 SummonCard한다. Card/Slot은 대상 위치에 use를 SummonCard한다. Player는 해당 플레이어 기준 소유자에게 use를 SummonCardHand한다. CardData는 caster 기준 소유자에게 전달된 정의를 SummonCardHand하며 use 필드는 무시한다. 각 경로의 기준 소유자에 use_opponent를 적용한다. |

| ENUM | 정의 및 용도 |
| --- | --- |
| EffectValueType | Value=0: 설정값. Attack=1: 시전자 공격력. Health=2: 시전자 현재 HP. |
| DeckInsert | Bottom=0, Top=1. 덱 삽입 위치. |
| StatusType | 전투·행동·수치 상태의 분류. Data/StatusData.cs의 값과 해당 상태의 소비·만료 경로를 기준으로 한다. 7은 이전 StoreValue의 직렬화 값으로 예약하며 재사용하지 않는다. |
| EffectStatType | None=0, Attack=10, HP=20, Mana=30, Range=40. 변경할 기본 수치. |
| EffectPlayerType | All=0, Player=10, Opponent=20. 개수 산정의 소유자 범위. |
| EffectActionType | Self=1, AbilityTriggerer=25, LastPlayed=70, LastTargeted=72, LastSelected=74. 행동할 카드의 조회 원천. |
| EffectTotalCountType | None=0, TotalHeal=10. 참조할 누적 통계. |
| EffectStatusType | BadStatus=0, GoodStatus=1, BothStatus=2. 상태 제거의 분류 범위. |
| EffectLastType | None=0, LastAttacked=1, LastTargeted=2, LastSummoned=3, LastDestroyed=4, LastPlayed=5. 현재 EffectUseCard.cs에 선언되어 있으나 해당 클래스의 필드·분기에서는 사용하지 않는다. |

#### 6.3.3 실행·분기·지속 효과 규칙

- 대상 선정은 AbilityData의 처리 단계를 따른다. 효과는 전달된 대상형·존재·영역·소유·실행 가능 상태를 확인하고 GameLogic의 규칙 경로로 상태를 변경한다.
- 효과 배열은 대상별 배열 순서로 적용한다. 수치가 실행 당시 상태를 읽는지, 발동 당시 스냅샷을 읽는지 정의한다. 반복 횟수와 효과 자체 수량을 구분한다.
- EffectConditional은 실제 대상, EffectConditionalCaster는 caster를 조건의 검사 대상으로 사용한다. 선택된 분기의 효과는 원래 수신자에 배열 순서대로 적용한다. condition 미지정은 참 분기, 빈 분기는 처리 없음이다.
- 분기 판정은 6.2의 공통 평가 정책을 따른다. 분기 효과는 ConditionEvaluation을 사용하고 선택된 자식 효과에도 같은 실행 문맥을 전달한다.
- 지속 효과는 재계산 횟수에 따라 영구 수치가 누적되지 않게 한다. 원천의 비활성화·이동·제거와 재계산 시 기여분을 해제·재구성하는 수명을 따른다.
- 생성·이동·소환은 목적 영역의 용량·점유·배치 조건과 실패 동작을 정의한다. 효과 실행 중 생긴 이벤트·공격·능력은 기존 큐 경계에 등록한다.
- 새 카드 생성은 정의·수량·추첨을 결정하는 단계와 생성·등록·목적 영역 배치를 확정하는 공통 단계를 구분한다. CardData 대상 방식과 생성 후보 목록 방식의 입력·수량·추첨 의미를 보존하면서 실제 생성 결과 처리를 공유한다. 입력 경로 통합을 이유로 수량을 중복 적용하거나 추첨 횟수·소유자·Variant·영역 이벤트를 암묵적으로 변경하지 않는다.
- 손패 생성의 상한 초과분은 생성된 카드로서 묘지(Discard)에 배치한다. 전체 카드 목록에만 남거나 손패와 묘지에 동시에 존재하면 안 된다. 초과로 묘지에 간 처리는 보드 사망이 아니며 OnDeath를 발생시키지 않는다. 정의 자체가 없거나 잘못된 생성 요청의 오류와 손패 용량 초과를 구분한다.
- 보드 소환을 위한 생성·준비 단계는 손패 생성 경로와 분리한다. 보드 소환은 손패 상한에 의존하지 않고 목적 보드의 점유·배치 조건을 따른다. 중간 준비 상태를 실제 손패 획득으로 기록하거나 손패 관련 이벤트를 발생시키지 않는다.
- 드로우와 기존 카드의 영역 이동은 새 카드 생성과 구분한다. 각각의 덱 제거·소진 피해·초과 시 묘지 이동·카드 초기화·이벤트 계약을 유지한다. 하위 영역 배치 기능을 공유하더라도 생성에 OnDraw/OnPlay를 추가하는 등 행동의 의미를 바꾸지 않는다.
- 무작위 효과는 게임의 난수 경로를 사용하고 판정 단위·횟수·결과 수명을 정의한다. UI 조회나 결과 표시가 추가 추첨을 실행하지 않는다.
- 효과 실패·대상 소멸·부분 적용이 후속 효과·연결 능력·비용에 미치는 영향을 정의한다. 무동작을 정상 지원으로 간주하지 않는다.

#### 6.3.4 타일에 적용하는 효과

| 입력 | 수신자 | 적용 규칙 |
| --- | --- | --- |
| 점유된 보드 타일 | 점유 Card와 효과가 지원하는 부착물 | 해당 효과의 Card 처리와 선택적인 부착물 반응을 적용한다. |
| 빈 보드 타일 | 점유 Card 없음 | 카드 경로를 생략한다. 실내 Player 적용 여부와 독립적이다. |
| 실내 보드 타일 | 점유 Card와 타일 진영의 Player | 공통 타일 래퍼를 사용하는 효과는 각 수신자에게 적용한다. 점유 카드의 소유자로 Player를 정하지 않는다. |
| 실외·중립 타일 | 해당 타일의 카드 수신자 | 선생님에게 추가 적용하지 않는다. |
| Card 직접 지정 | 지정 Card | 실내에 서 있어도 Player를 추가하지 않는다. |
| Player 직접 지정 | 지정 Player | 기존 Player 효과 경로를 사용한다. |
| 무효 타일·PlayerSlot | 수신자 없음 | 보드 타일 래퍼에서 거부한다. PlayerSlot 입력은 기존 직접 Player 경로에서 처리한다. |

- `GameLogic.ApplyTileEffect(Slot, Action<Card>, Action<Player>, Action<Card> applyAttachment = null)`는 유효한 보드 위치의 수신자를 확정하고 Card → 선택적 부착물 반응 → 실내 Player 순서로 콜백을 호출한다. 비어 있는 콜백이나 해당 수신자가 없으면 그 단계만 생략한다. Player는 타일 진영과 `Slot.GetInsideSlot` 포함 여부로 정한다.
- `EffectDamage`, `EffectDamageCount`, `EffectDamageRatio`, `EffectHeal`의 Slot 경로는 이 래퍼를 사용한다. 추가 효과에 실내 선생님 적용을 지원할 때도 효과의 Slot 오버라이드에서 같은 래퍼에 Card/Player 처리를 전달하고 해당 클래스 명세를 갱신한다. `status` 배열이나 다른 효과에 이 규칙을 일괄 적용하지 않는다.
- 범위 확장 단계에서 같은 회차의 같은 Slot을 한 번만 최종 대상 목록에 넣고, 그 목록 확정 후 첫 DoEffect를 실행한다. 서로 다른 실내 Slot이 같은 Player에 대응해도 각 Slot의 DoEffect에서 각각 적용한다. Player별 중복 제거·값 합산으로 대체하지 않는다. 명시적 반복 회차와 effects 배열의 개별 효과도 독립적으로 적용한다.
- 기본 효과값은 효과의 계산 규칙을 따른다. 각 Card/Player 요청은 기존 수치 컨테이너·면역·피해·회복·이벤트 경로를 독립적으로 통과한다. 비율 피해는 각 수신자의 현재 HP를 기준으로 계산한다.
- 래퍼는 새로운 능력·Chain·큐·사망 경계를 만들지 않는다. 같은 Slot의 규칙 결과는 기존 대상 실행 범위 안에서 처리하고 기존 Presentation 경계에 기록한다. 유발 능력과 사망은 기존 ResolveQueue 순서를 따른다.
- 선택 위치와 적용 수신자를 데이터에서 구분한다. `effect_target=Unit`이면 같은 슬롯을 선택해도 점유 유닛의 Card 오버로드만 호출한다. `effect_target=Slot`일 때만 Slot 오버로드와 해당 효과의 타일 래퍼를 사용한다. 기준 조건은 선택할 슬롯의 점유·소유·종류·위치를 제한하며 대상형을 변환하지 않는다.
- 빈 실내·점유 실내·실외·중립·무효 위치, 여러 실내 타일의 같은 선생님 적용, 범위 중복, 유닛 직접 지정, 수치 전환, 반복 및 사망 순서를 검증한다.

#### 6.3.5 종족·상태·마커 데이터와 검증

카드와 플레이어에 붙는 부가 정보는 Trait·Status·Marker 세 데이터형으로 나눈다. 판단 기준은 그 정보가 무엇인지다. 카드가 무엇인지를 나타내는 분류면 Trait, 규칙을 바꾸는 키워드·상태이상이면 Status, 계산·분기에 쓰는 내부 값이면 Marker다.

| 데이터 종류 | 주요 필드 | 데이터 규격 및 적용 규칙 |
| --- | --- | --- |
| TraitData | `id/title/icon` | 종족 분류 태그(예: machine). 값이 없고 보유 여부만 의미가 있다. CardData.traits가 부여하며 런타임 CardTrait도 id만 갖는다. 화면에 태그로 표시한다. |
| StatusData | `effect`, `title/icon/desc/status_fx`, `bad_status`, `hvalue` | 규칙을 바꾸는 키워드·상태이상. 상태 종류, 표시 정보, 상태 분류 및 AI 평가값을 정의한다. 상태 수치와 수명은 부여 경로에서 전달한다. GameLogic은 StatusType으로 읽는다. |
| MarkerData / MarkerValue | `id/title/desc`, `marker/value` | 계산·분기용 내부 정수값(보너스 수치, 카운터, 저장 좌표·저장값, 분기 표식). CardData.markers가 초기값을 정의하고 런타임은 Card/Player의 `markers`·`ongoing_markers`에 보관한다. title·desc는 에디터용이며 태그로 표시하지 않는다. |

| 판단 | Trait | Status | Marker |
| --- | --- | --- | --- |
| 값 | 없음 | 수치·기간 | 정수 (없으면 0) |
| 플레이어 노출 | 태그로 표시 | 상태로 표시 | 카드 본문 또는 DescValueStat 치환값으로만 노출 |
| 해제·만료·침묵·EffectClearStatus | 영향 없음 | 영향 받음 | 영향 없음 (침묵은 지속 기여만 제거) |
| 소비자 | `has_trait` 조건·개수 산정 | GameLogic의 StatusType 분기, ConditionStatus | ConditionStatCustom·ConditionClubStatMatch·SortMarker·DescValueStat 및 Marker 필드를 가진 효과 |

- 새 정보는 위 기준으로 하나의 데이터형만 사용한다. 같은 id를 TraitData와 MarkerData에 동시에 두지 않는다.
- 코드는 Trait·Marker를 문자열 상수로 조회하지 않는다. 효과·조건은 직렬화된 TraitData/MarkerData 참조를 사용하고, GameLogic이 규칙 분기에 쓰는 값은 StatusType으로 표현한다.
- `has_trait` 계열 필드(ConditionCardType·ConditionCount·FilterRandomCount·RepeatCountType·AddStatCount·DamageCount·StoreCount)는 TraitData만 받으며 Marker를 종족으로 세지 않는다.
- Marker 값은 영구값과 지속값의 합이다. Set/Add/Remove는 영구값을, 지속 효과는 ongoing 값을 변경하며 지속값은 재계산마다 초기화한다. 카드가 영역 초기화(Card.Clear)를 거치면 CardData.markers의 초기값으로 되돌아간다.
- 슬롯 좌표를 Marker에 저장할 때는 Slot.ToMarkerCode/FromMarkerCode를 사용한다. 0은 저장값 없음이다.
- AbilityData.status는 효과 배열 실행 후 동일한 수신자 계약으로 적용한다. 일반 부여는 value·duration을, 지속 부여는 ongoing 상태 경로를 사용한다.
- 상태 기간은 상태 수명 규칙을 따른다. 현재 상태 모델의 영구값은 0이며 영구 상태는 기간 감소 없이 유지한다. 유한 기간의 감소 경계와 중복 부여 시 병합 규칙을 확인한다.
- Marker 정수값의 의미는 해당 소비자와 MarkerData.desc가 정의한다. 표시용 치환은 6.1.6을 따르고, 규칙 상태를 표시 문자열에서 역산하지 않는다.
- 지원 대상형별 결과, 카드/타일의 중복 전달, 효과 배열 순서, 분기·반복·연결 능력의 조합, 지속 재계산, 사망·이동·제거 후의 유효성을 검증한다.

### 6.4 데이터 컨테이너와 수치 보정 규격

#### 6.4.1 소유와 실행 경계

- `DataContainer`는 ScriptableObject 기반의 추상 데이터 클래스다. 실행 메서드·호출별 캐시·진행 상태를 갖지 않는다. 현재 자식 클래스는 `NumericModifierData`이며 피해·회복의 종류와 수치를 보정한다. EffectData나 StatusData를 상속하지 않는다.
- `AbilityData.data_containers`가 컨테이너를 참조한다. 소비 경로는 Ongoing 능력이며, 능력이 존재하고 시전자가 CanDoAbilities를 만족할 때 적용된다. 침묵·능력 제거·적용 영역 이탈은 보정을 해제한다. 컨테이너가 없는 기존 능력은 기존 경로를 따른다.
- effects/status가 모두 비어 있는 지속 능력은 일반 재계산에서 조건·대상을 평가하지 않는다. HasEffectOrStatus로 실행할 내용의 존재를 검사한다. 컨테이너 전용 능력의 조건은 실제 수치 요청에서만 평가한다.
- 보정은 기존 요청과 실제 상태 적용 사이의 계산 단계다. `ResolveNumericOperation`은 능력을 발동하거나 DoEffect/DoOngoingEffect를 호출하지 않는다. 큐·선택·Repeat·Chain·마나 비용·연출을 추가 실행하지 않는다. 기존 효과와 상태 필드는 원래 실행 경로가 소비한다.
- 보정 원천은 기존 지속 능력과 동일한 Hero·보드·장비·부착·동아리·player_ability 영역에서 조회한다. 손패 카드는 수신자가 될 수 있지만 보정 원천으로 조회하지 않는다. 대상은 `GetOngoingTargets`를 공유하여 정하며, 지속 능력의 필드 소비·범위 규칙을 그대로 따른다.
- 지원 기준 대상은 Self, PlayerSelf, PlayerOpponent, AllPlayers, Club, EquippedCard, AttachedSlot, AllCardsAllPiles, AllCardsHand, AllCardsBoard다. Self는 카드 자체다. Slot을 직접 수정하거나 실내 타일에서 Player를 암묵적으로 추가하지 않는다. 타일 효과는 공통 타일 래퍼가 정한 각 Card/Player에 독립적인 수치 요청으로 들어온다. 한 수신자의 종류·값 보정 결과를 다른 수신자에게 재사용하지 않는다.
- 선언형 데이터는 공유되며 실행 중 변경하지 않는다. 요청·최종 값은 호출별 값 객체에 보관한다. 활성 규칙을 별도 영구 목록에 등록하지 않고 현재 게임의 능력·조건·수신자를 조회한다. 복제된 게임은 복제된 카드·플레이어를 사용하며 Unity 오브젝트 생성·로딩 API를 호출하지 않는다.

#### 6.4.2 NumericModifierData Inspector 필드

| 필드 | 형식 | 규격 |
| --- | --- | --- |
| input_kind | NumericKind | 해당 연산 단계에서의 처리 종류를 검사한다. Any는 종류 제한 없음이다. 원래 종류가 아니라 전환 중/완료 후의 종류와 비교한다. |
| cause | NumericCause | 요청 발생 원인을 제한한다. Any는 제한 없음이다. 종류가 전환되어도 원인은 유지된다. |
| operation | NumericOperator | 종류 전환 또는 수치 연산을 선택한다. |
| value | int | Set의 설정값, Add의 증감량, Multiply의 분자. ConvertKind에서는 사용하지 않는다. Set·Multiply는 음수를 허용하지 않으며 Add는 부호 있는 값이다. |
| divisor | int | Multiply의 양수 분모. 다른 연산에서는 사용하지 않는다. |
| output_kind | NumericKind | ConvertKind의 결과 종류. Damage 또는 Heal만 허용한다. 다른 연산에서는 사용하지 않는다. |

DataTableExporter는 Ability의 data_containers 참조와 DataContainer 테이블의 자식 타입·필드·관련 ENUM을 내보낸다. 컨테이너 참조 키는 에셋 이름을 사용하므로 같은 테이블 내 이름은 고유해야 한다.

컨테이너의 값은 AbilityData.value/duration과 독립적이다. 정의되지 않은 ENUM, 유효하지 않은 전환 결과, 음수 Set/Multiply, 양수가 아닌 분모는 유효한 데이터가 아니다. Inspector의 Min 표시는 런타임 검증을 대체하지 않는다. `ValidateNumericModifier`가 공통 검증을 담당하며 에디터 필드 검사도 이를 사용한다.

#### 6.4.3 ENUM과 연산

| ENUM | 값 | 용도 |
| --- | --- | --- |
| NumericKind | Any=0, Damage=1, Heal=2 | Any는 조건의 와일드카드다. 실제 요청·전환 결과는 Damage/Heal이다. |
| NumericCause | Any=0, Attack=1, CounterAttack=2, Ability=3, Status=4, Fatigue=5, LifeSteal=6, Trample=7, Other=8 | 카드 종류와 별개인 발생 경로다. Ability는 유닛 능력과 이벤트 카드의 효과를 포함한다. Any는 컨테이너 조건의 와일드카드다. |
| NumericOperator | Set=0, Add=1, Multiply=2, ConvertKind=3 | 수치의 설정·증감·배율 적용과 처리 종류 전환이다. 비교용 ConditionOperator와 구분한다. |

| 연산 | 계산 |
| --- | --- |
| Set | 현재 수치를 value로 설정한다. |
| Add | 현재 수치에 value를 더한다. |
| Multiply | 현재 수치 × value ÷ divisor. 정수 계산으로 나머지를 버린다. |
| ConvertKind | 현재 종류를 output_kind로 바꾸며 수치는 유지한다. |

피해·회복 수치는 비음수다. 입력 음수는 0으로 보정하며 수치 연산별 결과는 0부터 int.MaxValue까지 제한한다. 중간 곱셈·가산은 long을 사용한다. 컨테이너가 없으면 유효한 입력 종류·수치는 그대로 통과한다. 버프·스탯 수치 변경은 이 피해·회복 경로의 지원 대상이 아니다.

#### 6.4.4 적용 순서·수명·중복

1. 활성 원천 능력에서 요청 원인에 맞는 선언 목록을 수집한다. 이 단계에서는 발동·대상 조건을 실제 평가하거나 난수를 소비하지 않는다. 조건 문맥과 적용 여부는 요청별·원천 능력별로 분리한다.
2. 원천은 play_order, player_id, UID의 ordinal 순서로 정렬한다. 동일 원천 안에서는 능력 배열 순서와 컨테이너 배열 순서를 보존한다.
3. ConvertKind 규칙을 순서대로 한 번씩 검사한다. 현재 종류가 일치할 때 원천 능력의 적용 여부를 확인한다. 앞선 전환 결과를 다음 규칙이 읽는다. 이미 지나간 규칙으로 돌아가거나 공개 피해·회복 API를 재귀 호출하지 않는다. 전환 순환이 있어도 유한한 한 번의 순회로 끝난다.
4. 최종 종류가 일치하는 수치 규칙에 대해 원천 능력의 적용 여부를 확인하고 순서대로 한 번씩 적용한다. 최초 입력 종류가 달라도 전환 후 일치하면 적용 대상이다. 설정·가산·배율 간 순서는 데이터 순서다. 여러 Set이 일치하면 뒤의 Set이 앞선 값을 대체한다.
5. 변경된 종류·수치를 실제 상태에 한 번 적용하고 기존 결과 이벤트 경로로 연결한다.

원천 능력의 적용 여부는 해당 요청에서 처음 필요한 순간에 판정한다. 공유 대상 경로의 Query로 수신 가능성을 확인하고, 가능할 때만 발동·대상 조건을 실제 평가한다. 조회는 난수를 소비하지 않는다. 실제 적용 여부는 성공·실패 모두 같은 능력의 다른 컨테이너와 전환·수치 단계에서 재사용하며 다음 요청에는 넘기지 않는다. 서로 다른 능력은 같은 ConditionData를 참조해도 독립 판정한다. 보정 함수는 UI 조회용으로 호출하지 않는다.

같은 원천의 같은 Ability ID와 같은 능력 안의 같은 컨테이너 참조는 중복 계산하지 않는다. 범위 중첩으로 동일 수신자가 여러 번 발견되어도 같은 요청의 규칙은 한 번만 수집한다. 서로 다른 원천·서로 다른 능력·별도 컨테이너 정의는 독립 규칙이며, 반복·다중 대상·연쇄로 생긴 각 요청은 독립 계산이다.

#### 6.4.5 요청·적용 경로

| 데이터/함수 | 계약 |
| --- | --- |
| NumericOperation | readonly 값 객체. kind·cause·value·source·target을 보관한다. target은 Card/Player 생성자 중 하나로 지정한다. 현재 게임의 호출 스택에만 존재하며 에셋이나 큐의 공유 상태에 저장하지 않는다. |
| NumericValue | readonly 최종 kind·value. 요청 원본을 변경하지 않는다. |
| ResolveNumericOperation | 활성 컨테이너를 조회하고 최종 종류·수치를 계산한다. HP 변경·사망·결과 이벤트는 실행하지 않는다. |
| ApplyNumericOperation / AcceptsNumericOperation | 수치 요청의 공용 적용 경로와 수락 검사. 원래 요청의 유효성·면역 검사 → 공통 보정 → 최종 종류의 유효성 검사 → 카드/플레이어 상태 반영을 연결한다. 이름은 범용이지만 현재 지원 종류는 NumericKind의 Damage/Heal(HP)뿐이며, 스탯 변경은 지원 범위를 확장할 때 이 명세와 함께 추가한다. |
| onCardDamaged / onPlayerDamaged / onCardHealed / onPlayerHealed | 상태 반영 직후의 결과 알림. 카드 피해는 적용 피해량, 플레이어 피해는 보정 후 피해량, 회복은 실제 HP 변화량을 전달한다. 피로 피해는 onExhaustDamage로 알린다. 규칙 판정에 사용하지 않는 표시용 알림이다. |
| 기존 Damage/Heal 진입점 | 요청의 원인·수신자를 지정해 같은 적용 경로를 호출한다. Card/Player 회복은 선택적 원인 카드 오버로드를 제공하고 기존 두 인자 API도 유지한다. |

- 공격·반격·효과·원인 없는 피해·중독·피로·흡혈 회복·관통 피해가 공통 보정을 통과한다. 스탯 증가·최대 HP 변경·초기화는 피해/회복으로 간주하지 않는다.
- 원래 피해의 무적·효과 면역 검사 후 전환을 처리한다. 카드 피해로 남았을 때 기존 보호막·방어도·피해 이벤트·사망 원인·공격 후 반응을 적용한다. 회복으로 전환되면 피해 보호막을 소모하거나 피해 이벤트를 발생시키지 않는다.
- 회복량 0은 취소가 아니다. 카드의 OnHeal/OnHealOther는 발생하며 실제 회복량 통계는 실제 HP 변화만 기록한다. HP 0 회복과 이미 dying인 카드의 처리 차이는 기존 사망 규칙을 유지한다.
- 플레이어 중독·관통·흡혈의 기존 상한 정책을 유지한다. 중독·관통 피해는 onPlayerDamaged, 흡혈 회복은 onPlayerHealed로 알린다. 일반 피해/회복·피로는 기존 HP 상한을 적용한다. 흡혈 회복은 기존처럼 상한 제한과 회복 통계 누적을 하지 않는다. 후속 흡혈·관통 요청도 자기 원인으로 별도 보정되며 그 요청에서 다시 흡혈·관통을 만들지 않는다.
- 준코와 이즈미는 SELF 지속 능력의 컨테이너로 정의하며 카드 ID 분기를 사용하지 않는다. 준코는 Heal/Set, 이즈미는 Damage·Ability/ConvertKind를 사용한다. 두 보정 모두 침묵 시 해제된다.
- `TcgEngine/Validate Selected Ability Fields`로 컨테이너 경로·연산 설정을 검사하고 `TcgEngine/Validate Numeric Containers`로 순서·중복·전환·침묵·제거·이벤트·AI 복제·리플레이 복원을 검증한다. 컨테이너 자식 클래스나 연산·수신자 지원을 추가하면 해석기·검증·이 명세를 함께 갱신한다.

## 7. 능력 처리 큐 구조

`Assets/TcgEngine/Scripts/Tools/ResolveQueue.cs`와 `GameLogic/GameLogic.cs`의 실행 구조를 기술한다. 큐는 작업의 등록·순서·중단·재개를 담당하고 GameLogic은 조건·대상·효과·사망·반복을 판정한다. 수치 컨테이너 보정은 실행 중 요청의 동기 계산 단계이며 새 능력 발동이나 별도 큐 항목을 만들지 않는다. 규칙 상태 변경과 결과 표시는 별도 경계로 관리한다.

### 7.1 구성 요소

| 구성 요소 | 저장 내용 | 책임 |
| --- | --- | --- |
| phase_queue / current_top | 대기 중인 최상위 Phase와 현재 Phase | 이벤트 묶음의 실행 및 최상위 경계 관리. |
| insert_stack | 작업을 등록할 스코프 | 실행 중 발생한 작업의 부모 관계 유지. |
| AbilityPhase | chains, triggers, attacks, children, parent, active, alive | 작업 트리와 등록·회수 수명 관리. |
| AbilityQueueElement | ability, caster, Card triggerer, 반복 상한/회차, context, callback | 능력 실행에 필요한 인자 전달. |
| secret_queue | 비밀 이벤트와 실행 인자 | 비밀 처리의 FIFO 순서 유지. |
| attack_queue | 공격자, 대상형별 대상/콜백, 비용 생략 설정 | 공격 단계의 FIFO 순서 유지. |
| callback_queue | 진행 콜백과 인자 | 선행 규칙 처리가 끝난 뒤 진행 작업 실행. |
| suspended_scope | 선택 대기 실행의 스코프 | 기존 위치에서 선택 실행 재개. |
| pending_repeats | 능력·시전자·원인·문맥·반복 상한·다음 회차 | 안정 시점까지 반복 판정 보류. GameLogic이 소유한다. |

### 7.2 등록과 실행 순서

| 등록 API | 배치 규칙 |
| --- | --- |
| BeginPhase / EndPhase | 스코프가 있으면 자식 Phase, 없으면 최상위 대기열의 끝에 등록한다. EndPhase는 등록을 닫고 빈 Phase를 회수한다. 선택 재개 시에는 보존 스코프를 복원한다. |
| BeginImmediatePhase | 최상위에서는 대기열 앞, 열린 스코프에서는 자식 위치에 등록한다. 등록 위치를 제어하며 즉시 재귀 실행하지 않는다. |
| AddAbility | is_chain에 따라 해당 스코프의 chains 또는 triggers에 등록한다. 스코프가 없으면 최상위 Phase를 생성한다. |
| AddTriggeredAttack | 열린 스코프의 attacks에 등록하며 스코프가 없으면 기본 공격 큐를 사용한다. |
| AddAttack / AddSecret / AddCallback | 종류별 기본 큐에 등록한다. |

이벤트 수신 배치는 동일 Phase로 묶는다. 카드의 능력은 능력 배열 순서, 다른 카드/플레이어 카드의 반응 배치는 play_order 정렬을 사용한다.

| 실행 계층 | 선택 규칙 |
| --- | --- |
| 최상위 경계 | 소진된 Phase를 정리한 뒤 대기 사망·지연 반복을 판정한다. |
| 최상위 Phase | 대기 목록의 선두를 선택한다. 일반 등록은 FIFO이며 즉시 Phase 등록은 선두 삽입이다. |
| Phase 내부 | `자기 chains → 자식 Phase의 재귀 탐색 → 자기 triggers → 자기 attacks` 순서다. 각 항목 큐는 FIFO다. |
| 기본 큐 | 실행할 Phase 항목이 없으면 `secret_queue → attack_queue → callback_queue` 순서로 처리한다. |

- 일반 자식 이벤트는 생성 순서로 배치한다. 개별 능력/유발 공격의 실행 스코프는 소유 Phase의 맨 앞 자식으로 삽입한다.
- FindNextPhase의 chain 우선순위는 자식 탐색보다 앞선다. 따라서 항목의 FIFO 등록과 하위 스코프의 전체 완료 순서는 별개다.
- 진행 콜백이 새 이벤트를 생성하면 해당 이벤트는 다시 Phase 우선순위를 따른다. 최상위 경계 외의 개별 항목 사이에 사망 처리를 삽입하지 않는다.

### 7.3 능력과 반복의 처리 단계

| 단계 | 담당 경로 | 처리 내용 |
| --- | --- | --- |
| 발동 등록 | TriggerCardAbility → EnqueueAbility → RepeatTriggerCardAbility → AddAbility | 새 실행 문맥을 만들고 시전 가능 상태·발동 조건·반복 정책을 검사하여 등록한다. 카드 원인과 플레이어 원인은 같은 진입점에서 원래 원인형으로 평가한다. |
| 능력 실행 | ResolveCardAbility | 시전 가능 상태와 최초 회차의 발동 조건을 검사한다. 선택 여부를 판단하고 대상형별 실행 경로로 전달한다. |
| 대상 처리 | PlayTarget·Player·Card·Slot·CardData·무대상 처리 경로 | 해당 경로의 대상을 구해 효과와 상태를 적용한다. 대상형 간 중복 수신자를 방지해야 한다. |
| 실행 완료 | AfterAbilityResolved | 사용 기록, 해당 trigger의 비용 처리, 지속 상태 재계산, 승리 확인, chain 등록, 종료 이벤트, 다음 반복 보류를 수행한다. |
| 반복 재개 | ProcessPendingRepeats | 안정 시점에서 보류 목록 역순으로 계속 조건과 시전자 유효성을 검사하고 다음 회차를 즉시 Phase에 등록한다. |
| 선택 능력 실행 | SelectChoice → CompleteSelection → TriggerCardAbility | 부모 완료 후 복원된 스코프에 선택한 자식을 chain으로 등록한다. 직접 ResolveCardAbility를 호출하지 않는다. |

실행 문맥의 전달 범위:

| 항목 | 현재 구현의 전달 방식 |
| --- | --- |
| 발동 조건 | 등록·최초 해결에서 동일 공통 평가기를 호출한다. Revalidate는 재검사하고 EvaluateOnce는 실행 문맥의 결과를 재사용한다. |
| 이벤트 원인 | 큐의 Card 인자와 별개로 AbilityExecutionContext가 trigger_uid/trigger_player를 보존한다. 재검사는 원래 원인형으로 수행한다. source_event는 사망 등 원래 이벤트의 불변 값이다. |
| 일반 연결 | 부모 caster를 원인으로 별도 실행 문맥을 생성한다. 원래 source_event를 유지하고 부모 회차의 슬롯·마지막 대상 UID/위치를 복사한다. 형제끼리 캐시나 결과 목록을 공유하지 않는다. |
| 반복 | 이전 실행의 caster·triggerer·context와 반복 상한을 다음 회차에 전달한다. 최초 발동 조건은 회차마다 재검사하지 않는다. |
| 비용 | AfterAbilityResolved에서 Activate/None에 설정된 마나·소진 비용을 적용한다. 연결 능력은 자체 완료 경로를 거친다. |

- AbilityExecutionContext는 실행 캐시·회차·결과를 보관하고 source_event는 불변 이벤트 정보만 보관한다. OpenSelection은 선택 종류에 공통으로 selector_context를 저장한다. 사용 시 선택은 PreparePlay에서 준비한 문맥을 PlayCard와 해당 능력의 EnqueueAbility까지 전달한다. 다른 능력과 새 발동은 독립 문맥을 생성한다. Game.Clone은 실행 문맥의 목록·캐시와 GameRandom 상태를 복제한다.
- PreparePlay는 예정된 사용 카드 UID와 보드 카드의 예정 소환 위치를 실행 입력으로 보관한다. 최근 행동 Condition은 실행 스냅샷을 읽으며 OnPlay 여부로 위치를 바꾸지 않는다. 선택 수락은 최초 실행 회차와 같은 판정 단위를 사용한다.
- CanTarget·GetSelectionCandidates·CanSelectAbility의 조회는 Query 문맥으로 판정하며 난수와 실행 캐시를 변경하지 않는다. 선택지 조회는 부모 캐시를 상속하지 않는 자식 문맥을 사용한다. 실제 대상 수락의 CanTarget은 evaluate=true로 검사하고, 선택한 자식의 실제 발동 조건은 EnqueueAbility에서 평가한다.
- CardSelector의 필터 결과는 OpenSelection에서 selector_card_uids에 확정한다. UI·AI·입력 검증·시간 초과 처리는 같은 후보 유효성 검사를 사용하며 필터를 다시 추첨하지 않는다. 캐시가 없으면 후보가 없는 선택으로 처리한다. 필터 결과가 비었으면 선택 대기를 열지 않는다.
- GameRandom은 게임별 복제 가능한 난수 상태다. ReplayRandom은 같은 난수 경로의 추첨을 기록하며 AI 복제는 원본 스트림과 기록 콜백을 공유하지 않는다. UID 생성 난수는 효과 후보 추첨에 사용하지 않는다.

### 7.4 사망과 공격의 경계

| 처리 단계 | 규칙 |
| --- | --- |
| 사망 후보 수집 | 최상위 경계에서 지속 상태를 갱신하고 보드·player_ability의 사망 후보를 play_order로 정렬한다. |
| 사망 확정 | 이벤트 스냅샷을 생성하고 해당 배치의 대상을 모두 제거한다. |
| 반응 등록 | 처치 반응 후 사망 반응을 즉시 Phase에 등록한다. 제거된 객체는 다른 사망의 생존 수신자로 참여하지 않는다. |
| 안정화 | 새 사망 배치와 그 반응을 해결한다. 사망 후보가 없는 안정 시점에 지연 반복을 판정한다. |
| 기본 공격 | 공격 항목을 최상위 실행 스코프로 처리하고 발생한 반응이 남으면 current_top으로 유지한다. |
| 다중 공격 | 진행 중 사망 처리를 보류하고 공격 대상 탐색이 끝나는 경계에서 해제한다. 이후 사망 배치를 해결한 뒤 진행 콜백을 처리한다. |

- `HasPendingDeaths`는 사망 후보와 보류 반복의 존재를 보고한다. `death_step_suspended`는 사망 단계를 실행할 수 있는지를 제어한다. 대기 작업의 존재와 실행 가능 여부를 분리한다.
- 장비·부착물의 즉시 제거 경로와 보드 사망 배치의 처리 범위를 구분한다. 영역별 제거 방식과 이벤트 발생을 검증한다.

### 7.5 선택 상태 전이

| 전이 | 문맥 및 스코프 처리 |
| --- | --- |
| 실행 → 선택 대기 | OpenSelection에서 selector 상태·입력 문맥·후보 캐시를 저장한다. 해결 중 선택은 실행 스코프를 suspended_scope로 보존하고 큐를 보류한다. |
| 선택 대기 → 확정 | SelectTarget에서 보드 Card·Player 클릭을 Slot·PlayerSlot으로 정규화하고 같은 슬롯 검사·GetSpatialTargets 경로로 처리한다. 비공간 CardSelector는 카드 후보 검사를 유지한다. FinishPlaySelection은 준비 문맥으로 PlayCard를 호출하고, CompleteSelection은 반복 인자를 보존한 뒤 같은 Phase를 재개한다. ResetSelectionState는 문맥과 후보 캐시를 해제한다. |
| 선택 대기 → 취소 | 선택 문맥을 정리하고 임시 영역 카드를 복구한다. 보존 스코프를 해제하며 빈 스코프는 회수한다. 이미 등록된 작업은 정상 소진한다. |
| 선택 대기 → 시간 초과 | 선택 종류와 취소 정책에 맞는 종료/자동 선택 경로를 적용한다. |

대상 선택 재개는 같은 실행, 선택한 자식과 새 발동은 별도 실행으로 취급한다. 중복 확정·무효 대상·취소 이후에는 이전 선택의 비용·효과·스코프가 다른 실행에 적용되지 않아야 한다.

### 7.6 실행 모드와 표시 경계

| 구분 | 실행 규칙 | 처리 한계 동작 |
| --- | --- | --- |
| 일반 | 단계별 실행 후 GameConfig.Timing의 지연을 적용한다. | 다음 갱신에서 실행 가능 여부를 재검사한다. |
| Immediate | 지연 없이 호스트 호출의 처리 예산까지 실행한다. | 남은 작업을 보존하고 다음 갱신으로 넘긴다. |
| AI / skip_delay | 지연 없이 실행 가능 작업을 처리한다. | 안전 상한에 도달하면 오류를 기록하고 큐를 정리한다. |
| 에디터 단계 실행 | 실제 게임 큐의 단계 진행을 제어한다. | 단계 입력까지 보류하며 AI 예측에는 적용하지 않는다. |

- ResolveAll의 재진입은 is_resolving으로 차단한다. CanResolve는 외부 중단·지연·게임 종료·선택 대기·에디터 중지 및 대기 작업을 검사한다.
- SetDelay는 기존 대기와 요청 지연 중 큰 값을 사용한다. 다음 지연의 종류는 실행할 작업의 우선순위와 사망 보류 여부에 맞춰 선택한다.
- IsResolving의 입력 게이트는 규칙 실행 중 다른 행동을 차단하면서 현재 선택·멀리건 응답은 받을 수 있어야 한다.
- onResolved는 개별 단계 종료 알림이다. 전체 큐의 소진 여부는 별도의 대기 작업 판정으로 확인한다.
- RecordReplayState는 중간 규칙 기록, CompletePresentationStep은 완료된 표시 결과 경계다. ResolvePresentedAbility/Target은 표시 단위를 묶으며 playback_mode에 따른 표시 방식은 규칙 실행 순서와 분리한다.

### 7.7 정리 책임과 검증

| 담당 경로 | 정리 범위 |
| --- | --- |
| ResolveQueue.Clear | 큐 버전, 항목·Phase 풀, 기본 큐, Phase 목록, 등록 스택, 현재/보존 스코프, 트리 오류 상태. |
| GameLogic.ClearResolve | 큐 정리와 GameLogic이 보관하는 지연 반복. |
| 선택 정리 경로 | selector 문맥, 선택 대기 카드 영역, 보존 스코프. |
| 게임/턴/AI 상태 전환 경로 | 외부 중단·지연·사망 보류 등 전환 대상의 잔여 실행 상태. |
| DisposePhase | 관련 참조를 해제하고 활성 수명을 끝낸 뒤 풀로 반환. |

- 실행 콜백 전후의 큐 버전을 비교해 정리된 스코프에 다시 접근하지 않는다. 회수한 객체는 재개하거나 중복 반환하지 않는다.
- 트리 구조와 반복 실행의 안전 상한은 오류 감지 수단이다. 해당 상한에 의한 중단을 능력의 정상 종료로 취급하지 않는다.
- 검증은 작업 종류별 우선순위, 형제 등록 순서, 선택 재개 위치, 사망 배치, 반복 안정 시점, 공격 보류, 진행 콜백, 상태 전환 후 잔여 문맥을 대상으로 한다. 일반·Immediate·AI의 규칙 결과가 일치해야 한다.

## 8. 게임 용어 및 카드 검증 범위

카드 설명, 능력 제작, 구현 검토와 검증 시 아래 정의를 공통으로 사용한다. 용어의 뜻은 게임 규칙으로 정하며, 기존 메서드·에셋 이름만 보고 뜻을 추정하지 않는다. 용어 정의를 바꾸는 것과 개별 카드의 대상 제한을 바꾸는 것은 별도 작업이다. 이미 합의한 카드별 제한은 보존하고 설명·조건과의 불일치는 검토 결과로 기록한다.

### 8.1 참여자와 카드 분류

| 용어 | 정의 및 데이터 대응 |
| --- | --- |
| 선생님 / 플레이어 / 본체 | 각 진영을 담당하는 `Player`. 선생님의 체력은 `Player.hp`다. 보드 유닛이나 `CardType.Hero` 카드와 구분한다. |
| 아로나 / 영웅 카드 | `CardType.Hero`, `Player.hero`. 플레이어가 사용할 능력 등을 담는 카드 객체이며 선생님 자체를 뜻하지 않는다. |
| 카드 | `CardData`로 정의하고 실제 게임에서는 `Card` 인스턴스로 사용하는 객체. 유닛·이벤트·동아리 등을 포괄한다. 카드 정의의 ID와 인스턴스의 UID를 구분한다. |
| 유닛 | **학생(`Student`)·비학생(`NonStudent`)·장소(`Place`)를 모두 포함한다.** 선생님, 이벤트, 동아리 카드는 포함하지 않는다. 유닛이라는 분류만으로 공격·이동 가능 여부를 결정하지 않는다. |
| 학생 | `CardType.Student`. 유닛의 하위 분류다. 학생 대상 조건은 비학생과 장소를 포함하지 않는다. |
| 비학생 | `CardType.NonStudent`. 유닛의 하위 분류다. 학생이 아니라는 논리 부정과 다르며, 장소나 이벤트까지 포함하지 않는다. |
| 장소를 제외한 유닛 | `Student`와 `NonStudent`의 합집합. 기존 `IsCitizen()`과 `is_citizen`이 가리키는 범위다. 대상 범위를 명시하는 카드 설명에서는 이 표기를 사용하며, 시민·전투원 등의 별칭이나 학생·비학생을 나열하는 표현과 혼용하지 않는다. 학생만 또는 비학생만 대상으로 하면 해당 하위 분류를 그대로 쓴다. |
| 장소 / 건물 | `CardType.Place`. 장소도 유닛이다. 현재 데이터의 building 계열 조건은 장소 분류에 대응한다. 장소의 공격·반격·이동 자격은 각 행동 규칙으로 검사한다. |
| 보드 카드 | 타입 판정 `IsBoardCard()`는 학생·비학생·장소에 대응한다. 실제 보드에 존재하는지는 별도의 영역 검사 `IsOnBoard`/`PileType.Board`로 확인한다. 타입 판정만으로 현재 보드에 있다고 가정하지 않는다. |
| 이벤트 카드 / 주문 카드 | `CardType.Spell`. 카드 사용으로 효과를 실행하는 분류다. 능력 발동 이벤트라는 처리 용어와 구분한다. |
| 동아리 / 소속 | `ClubData`로 정의하는 소속 분류. 카드의 기본 소속과 게임 중 추가한 소속을 포함한 현재 소속으로 조건을 검사한다. |
| 동아리 카드 | `CardType.Club`, `Player.cards_club`. 동아리 공통 능력을 담는 카드다. 보드에 놓인 부원과 별개다. |
| 부원 / 동아리 인원 | 해당 동아리 소속 조건을 만족하는 카드. 능력에서 세는 영역·소유자·학생 여부는 각각 명시한다. 동아리 카드 자체를 보드 부원 수에 자동 포함하지 않는다. |
| 입부 | 대상 카드에 동아리 소속을 추가하는 처리. `EffectAddClub` 및 `OnAddClubOther` 경로에 대응한다. 새 카드 생성이나 등교와 구분한다. |
| 토큰 / 생성 카드 | 다른 카드의 효과로 생성되거나 소환되는 카드. 별도 `CardType`이 아니며 생성 카드도 자신의 학생·비학생·장소·이벤트 분류를 유지한다. 생성 가능 여부와 덱 편성 가능 여부는 독립적이다. |
| 비밀 / 장비 / 부착 카드 / 플레이어 능력 카드 | 각각 `Secret` / `Equipment` / `Attachment` / `PlayerAbility`. 유닛의 하위 분류가 아니며 각 전용 영역과 실행 계약을 사용한다. |

대상 설명에서 **유닛이라고만 쓰면 장소를 포함**한다. 학생·비학생만 포함하는 범위는 원칙적으로 카드 설명은 **장소를 제외한 유닛**으로 통일한다. 선택 안내는 8.7의 간결한 지정형 규칙을 따른다. 단, 공격력만 증감시키는 효과처럼 장소의 행동 규칙상 결과가 없는 전체·범위 효과는 카드 설명에서 불필요한 장소 제외 문구를 생략할 수 있다. 체력·회복·이동·기절·손패 복귀·무기 변경처럼 별도 결과나 선택 제한을 설명해야 하는 경우에는 이 예외를 적용하지 않는다. 아군·적 등의 제한은 “장소를 제외한 아군 유닛”처럼 함께 쓴다. `is_not_building`은 장소가 아니라는 조건일 뿐이므로, 카드 영역과 다른 타입의 포함 여부까지 함께 확인한다. 유닛의 범위를 맞추기 위해 `IsCitizen()`의 의미를 일괄 변경하지 않는다.

### 8.2 진영·보드·타일

| 용어 | 정의 및 적용 규칙 |
| --- | --- |
| 아군 / 적 / 상대 | 능력의 기준 카드·플레이어와 대상의 소유 관계. 카드의 `player_id`와 조건의 비교 기준을 사용한다. 카드가 서 있는 타일의 진영과 소유자는 구분한다. |
| 자기 자신 | 능력의 시전자 카드. `AbilityTarget.Self`에 대응한다. 자신의 선생님이나 자신의 타일을 뜻하지 않는다. |
| 자기 진영 / 상대 진영 | 타일이 속한 플레이어 영역. 배치·이동의 진영 제한은 대상 카드의 소유자와 행동 목적을 기준으로 검사한다. |
| 전장 / 필드 / 보드 | 실제 유닛들이 배치되는 유효 타일들의 공간과 그 위의 유닛 영역. 손패·덱·묘지·동아리 영역은 포함하지 않는다. |
| 타일 / 칸 / 슬롯 | 보드의 위치 `Slot`. 카드가 없는 빈 타일도 유효한 대상이다. 위치와 점유 카드는 서로 다른 대상형이다. |
| 실내 타일 | `Slot.GetInsideSlot(playerId)`에 속하는 보드 타일. 타일 효과가 선생님에게도 적용되는 경우에는 공통 타일 래퍼가 이 타일의 진영에 대응하는 `Player`에게 적용한다. 점유 카드의 소유자로 선생님을 결정하지 않는다. |
| 실외 타일 | `Slot.GetOutsideSlot(playerId)`에 속하는 타일. 해당 플레이어 진영의 타일 중 실내를 제외한 공간이다. 중립 타일과 별도 분류다. |
| 중립 타일 | `Slot.GetNeutral()`에 속하는 공용 보드 공간. 어느 선생님에게도 자동 대응하지 않는다. 실외라는 말에 중립을 암묵적으로 포함하지 않는다. |
| 플레이어 직접 지정 위치 | `IsPlayerSlot()`으로 식별하는 선생님 지정 표현. 실제 보드의 실내 타일과 구분한다. |
| 빈 타일 / 점유 타일 | 현재 보드 점유 카드가 없는/있는 유효 위치. 생성·이동·사망 연쇄 후에는 현재 점유 상태를 다시 확인한다. |
| 주변 / 인접 / 거리 | `Slot`의 보드 이웃 관계와 거리 계산에 따른 공간 관계. 화면상의 직선거리나 사각 격자 대각선으로 임의 해석하지 않는다. |
| 사거리 | 현재 무기와 카드 수치로 결정한 공격 도달 범위. 능력의 `condition_wide_range` 패턴과 독립적이다. |
| 범위 / 광역 / 양옆 / 줄 | 기준 타일에 상대 좌표 패턴을 적용한 대상 공간. 중심 포함·방향·진영·최종 수신자 조건은 데이터로 명시한다. 열/행이라는 표현만으로 좌표축을 추정하지 않는다. |

타일 효과는 타일 수신 경로를 사용하고, 실내의 선생님 적용은 6.3.4의 공통 타일 래퍼 규칙을 따른다. 유닛 대상 효과가 실내의 유닛에게 적용됐다는 이유만으로 선생님에게도 적용하지 않는다.

### 8.3 자원·수치·영역

| 용어 | 정의 및 데이터 대응 |
| --- | --- |
| 청휘석 / 마나 | 카드·활성화 능력 비용에 사용하는 플레이어 자원. 현재량은 `Player.mana`, 최대량은 `mana_max`다. 카드 이름 청휘석과 자원 자체를 구분한다. |
| 비용 | 카드 사용의 `Card` 비용 또는 활성화 능력의 `AbilityData.mana_cost`. 비용 변경과 현재 청휘석 증감은 별도 효과다. |
| 체력 / 현재 체력 / 최대 체력 | 피해를 견디는 수치. 카드의 현재 체력은 `GetHP()`, 최대 체력은 `GetHPMax()`를 사용한다. `Card.damage`는 누적 피해량이다. 선생님은 `hp`/`hp_max`를 사용한다. |
| 공격력 | 공격 피해 산정에 사용하는 현재 수치 `GetAttack()`. 공격력 값을 참조한 능력 피해도 피해 원인이 자동으로 공격으로 바뀌지는 않는다. |
| 능력 피해 / 효과 피해 | Effect·Ability 경로에서 발생하는 피해. 수치 원인은 `NumericCause.Ability`다. 이벤트 카드뿐 아니라 학생·비학생·장소의 능력도 포함한다. |
| 회복 / 치유 | 체력을 회복하는 처리. 요청량·보정 후 수치·실제 회복량을 구분한다. 실제 회복량이 0이어도 회복 이벤트가 발생하는 계약을 따른다. 최대 체력 증가는 회복과 별개다. |
| 버프 / 디버프 | 유리한/불리한 수치·상태 변경을 가리키는 설명 용어. 실제 지속 기간과 해제는 Effect·Status 데이터가 정의한다. 상태 분류에는 `StatusData.bad_status`를 사용한다. |
| 손패 / 패 | `PileType.Hand`, `cards_hand`. 사용할 카드를 보유하는 영역이며 용량 제한을 따른다. |
| 덱 | `PileType.Deck`, `cards_deck`. 뽑을 카드의 순서를 보관하는 영역이다. 덱 편성 목록과 게임 중 남은 덱을 구분한다. |
| 묘지 / 버림 영역 | `PileType.Discard`, `cards_discard`. 버려지거나 사망 처리를 마친 카드가 이동하는 영역. 이 영역으로 이동한 모든 경우가 사망은 아니다. |
| 임시 영역 | 선택·생성·사용 준비를 위한 처리 영역. `cards_temp`와 사용 전 선택 대기의 `cards_board_temp`는 목적이 다르며 정상 손패나 보드로 간주하지 않는다. |
| 무기 / Front·Middle·Back·None | `WeaponData`와 `WeaponType.FRONT/MIDDLE/BACK/NONE`. 무기 종류·기본 사거리·공격 대상 규칙을 담는다. 타입 이름만으로 카드의 현재 사거리를 고정하지 않는다. |
| 종족 특성 / 상태 / 마커 | 종족 특성은 `TraitData`로 정의하는 값 없는 분류 태그, 상태는 `StatusData`/`StatusType`의 수치·기간을 갖는 키워드·상태이상, 마커는 `MarkerData`로 정의하는 계산·분기용 내부 값이다. 판단 기준은 6.3.5를 따른다. 동아리 소속, 종족 특성, 상태, 마커를 서로 대체해서 해석하지 않는다. |

### 8.4 행동·발동·진행

| 용어 | 정의 및 처리 관계 |
| --- | --- |
| 사용 | 카드 사용 규칙에 따라 비용·대상·영역 이동·능력을 처리하는 행동. 일반 사용의 `OnPlay`와 공통 사용/소환 반응의 `OnUse`를 구분한다. |
| 등교 | 카드 설명의 `OnPlay` 발동 키워드. 일반 카드 사용에 연결한다. 효과로 소환되는 모든 경우에 자동 발동한다고 해석하지 않는다. |
| 소환 | 카드를 보드에 배치하는 행동. 새로 생성하여 소환하는 경우와 기존 덱/손패 카드를 배치하는 경우를 구분한다. `OnUse` 반응과 목적지 배치 규칙을 따른다. |
| 생성 | `CardData`에서 새로운 카드 인스턴스를 만들어 목적 영역에 넣는 행동. 드로우나 기존 카드 이동과 구분한다. 손패 초과분은 공통 생성/영역 처리 규칙에 따라 묘지로 보낸다. |
| 뽑기 / 드로우 | 덱의 기존 카드를 손패로 가져오는 행동. 드로우 경로의 이벤트·빈 덱 처리·손패 용량 규칙을 따른다. 특정 카드를 손패로 이동시키는 효과와는 실행 경로를 확인해 구분한다. |
| 되돌리기 / 이동 / 버리기 | 기존 카드의 위치·영역을 변경하는 행동. 새 카드 생성이나 사망으로 자동 해석하지 않는다. |
| 파괴 | 효과 또는 전투 결과로 카드의 사망 처리를 요청하는 규칙 행동. 단순 손패 복귀나 덱 이동과 구분한다. |
| 사망 / 하교 | 사망은 큐의 사망 경계에서 확정·처리하는 사건이다. 하교는 자신의 사망에 연결된 `OnDeath` 능력을 뜻하며, 다른 카드의 사망 반응은 `OnDeathOther`다. |
| 처치 | 사망 결과의 원인 카드에 귀속되는 사건. `OnKill`에 대응하며 피해를 줬다는 사실만으로 성립하지 않는다. |
| 공격 / 반격 | 공통 공격 큐에서 처리하는 전투 행동과 그에 대한 대응 피해. `Attack`/`CounterAttack` 원인을 구분한다. 능력 피해와 동일시하지 않는다. |
| 이동 / 밀치기 | 보드의 기존 유닛을 목적 타일로 옮기는 행동. 밀치기는 공격 방향 등으로 목적지를 정하는 이동 효과다. 목적지 규칙과 `OnMove` 발행 여부는 공통 이동 경로의 계약을 따른다. |
| 소진 / 준비 | `Card.exhausted`로 표현하는 행동 준비 상태. 소진 해제와 카드의 다른 행동 제한 해제는 별개다. |
| 턴 / 차례 | 한 플레이어의 턴 시작·메인·공격·턴 종료 흐름. `GamePhase`의 단계를 말하며 능력 큐의 Phase와 구분한다. |
| 멀리건 | 게임 시작 시 시작 패를 교체하는 절차. 일반 드로우나 카드 사용으로 해석하지 않는다. |
| 능력 / 효과 / 조건 | Ability는 발동·반복·대상·연결 구성, Effect는 상태 변경, Condition은 자격 판정이다. 6절의 데이터 규격을 따른다. |
| 시전자 / 발동 원인 / 대상 | 각각 능력을 소유·실행하는 카드, 능력을 일으킨 사건의 카드/플레이어, 효과를 받는 수신자다. 같은 객체일 수도 있지만 역할은 별도로 보존한다. |
| 지속 능력 / 발동 능력 / 활성화 능력 | 각각 `Ongoing`, 지정 Trigger, `Activate`에 대응한다. 지속 재계산·이벤트 발동·사용자 실행의 수명과 비용을 구분한다. |
| 반복 / 연쇄 능력 | 반복은 같은 능력의 회차 실행, 연쇄는 `chain_abilities`에 연결한 후속 능력 실행이다. 효과 수량과 구분한다. |
| 선택 / 대상 확정 | 사용자 입력 또는 필터로 기준 대상을 결정하는 과정. 카드 선택과 타일 선택은 대상형이 다르며, 범위 확장 후 실제 수신자와도 구분한다. |

### 8.5 전투 키워드와 상태 이름

| 용어 | 정의 및 대응 |
| --- | --- |
| 속사 | 소환된 턴에도 공격할 수 있도록 소진을 해제하는 능력 표현. 현재 `OU_haste`와 `EffectExhaust` 구성으로 처리한다. 별도 속사 Status가 있는 것으로 가정하지 않는다. |
| 난사 | `StatusType.MassShooting`. 사거리 안의 복수 적을 공격 대상으로 구성하며 현재 공격 처리에서는 대상별 명중/회피 판정을 수행한다. |
| 회피 | `StatusType.Evasion`. 현재 공격 경로의 회피 판정에 사용하는 상태다. Trait로 대체하지 않는다. 현재 난사 또는 회피에 의한 판정은 공격 대상별 50%이며 둘이 겹쳐도 해당 분기에서 한 번 판정한다. |
| 선생님 우선 공격 | `StatusType.PrioritizeTeacher`. 공격 대상 탐색(`AttackSearch`)에서 사거리에 상대 실내 타일이 있으면 사거리 내 적 유닛보다 상대 선생님을 먼저 공격하게 하는 상태다. 트리니티 자경단 동아리 카드의 지속 능력 `OG_prioritize_teacher_TVC`가 보드의 아군 소속 유닛에게 부여하며, 동아리 소속만으로 적용하지 않는다. |
| 기절 | `StatusType.Paralysed`. 공격·활성화 능력 자격을 제한한다. 이동 등 다른 행동은 각각의 행동 검사 계약을 따른다. |
| 침묵 | `StatusType.Silenced`. 능력 실행과 지속 능력의 기여를 비활성화한다. 능력이 소유한 수치 컨테이너에도 적용한다. 카드의 타입·소유자 자체를 바꾸지는 않는다. |
| 도발 / 보호받음 | `Protection` / `Protected`. 도발은 공격 대상 우선순위에 관여하는 상태, 보호받음은 도발로 부여되는 보호 상태다. 두 상태를 같은 이름으로 취급하지 않는다. |
| 은신 / 무적 / 효과 면역 | `Stealth` / `Invincibility` / `SpellImmunity`. 각각 대상 지정 제한, 피해·파괴 등의 허용 검사, 효과 원인에 따른 제한에 쓰인다. 대상 선정과 수치 적용의 검사는 별개이며 이름만으로 모든 대상형·원인에 동일한 면제를 부여하지 않는다. |
| 보호막 / 방어도 | `Shell` / `Armor`. 각각 소비되는 피해 방어 상태와 적용 가능한 피해 경로에서 차감하는 방어 수치다. 회복량이나 최대 체력 증가와 구분한다. |
| 치명 / 연속 공격 / 위협 | `Deathtouch` / `Fury` / `Intimidate`. 각각 피해 후 파괴 처리에 쓰는 상태, 추가 공격 기회에 쓰는 상태, 공격 시 상대 반격을 막는 상태다. 적용 대상과 횟수는 공통 전투 경로를 따른다. |
| 비행 | `StatusType.Flying`에 대응하는 상태 이름. 이름만으로 이동·사거리·대상 우선순위 변경을 추론하지 않으며, 행동 규칙에 명시된 적용 범위를 따른다. |
| 관통 / 흡혈 | `Trample` / `LifeSteal`. 각각 초과 피해를 상대 선생님에게 전달하는 처리, 피해에 연결해 자기 선생님을 회복하는 처리다. 별도의 수치 원인으로 공통 수치 경로를 통과한다. |
| 중독 / 수면 | `Poisoned` / `Sleep`. 각각 턴 시작 피해에 사용하는 상태, 턴 시작의 소진 해제를 막는 상태다. |
| 공격력 증가 / 체력 증가 / 비용 변경 | `AddAttack` / `AddHP` / `AddManaCost`. 상태로 부여한 값과 Effect가 기본 수치를 변경하는 경우를 구분한다. |
| 저장값 | `MarkerData` `stored_value`. EffectStoreCount가 누적하고 EffectAddStat.stored_value가 더해 사용하는 내부 값이다. 체력·공격력·청휘석 자체로 해석하지 않는다. 이전 `StatusType.StoreValue`는 사용하지 않는다. |

키워드 이름만으로 카드별 수치·기간·선택 범위를 보충하지 않는다. 카드 설명의 값, 상태 데이터와 실제 소비 경로를 대조한다. 영문 상태 식별자를 번역한 설명과 현재 카드에 표시하는 고유 명칭을 혼동하지 않는다.

### 8.6 현재 사용 가능한 카드 및 수정·검증 범위

현재 사용 가능한 카드와 카드 수정 범위는 아래 다섯 종류로 한정한다.

1. 학생(`CardType.Student`), 비학생(`CardType.NonStudent`), 이벤트(`CardType.Spell`), 건물(`CardType.Place`) 중 `CardData.deckbuilding = true`인 카드.
2. 영웅(`CardType.Hero`) 카드 전체. `deckbuilding` 값과 관계없이 검사한다.
3. 1번 카드 중 학생 타입 카드가 소속된 동아리 카드.
4. 후공 보너스 카드(`GameplayData.second_bonus`).
5. 위 카드들이 만들어내는 토큰 카드. 토큰 카드가 다시 만드는 토큰 카드까지 따라가며 중복은 제거한다.

- 카드 검토·검증 및 수정은 모두 위 범위를 따른다.
- 카드 종류·동아리·생성 정의 참조는 실제 효과의 공급 관계를 확인한다. 조건에서 비교하는 CardData 참조만으로 그 카드가 생성된다고 판단하지 않는다.
- 현재 사용하지 않는 카드, 기존 기본/AI 덱에 남은 카드, 튜토리얼 전용 카드, 테스트·프로토타입 카드 등 위 목록에 없는 별도 공급 카드를 자동으로 검사 대상에 추가하지 않는다. 별도 공급 설정에 참조가 있다는 사실만으로 사용자가 정한 검증 범위를 확대하지 않는다.
- 검토할 때 위 기준으로 목록을 다시 산출한다. 개수는 고정 규칙으로 삼지 않으며, 사용자가 명시적으로 범위를 바꾼 경우에만 추가 범위를 조사·집계한다.
- 공통 처리 검사는 이 집합의 능력과 관련된 경로를 대상으로 한다. 현재 카드로 발생하지 않는 합성 상태·확장 API 사례를 현재 카드의 실패 건수에 포함하지 않는다.
- 보고서의 카드 목록·문제 목록·검사 집계가 같은 범위를 사용해야 한다. 범위 밖 결과와 잘못된 용어 해석에서 나온 지적은 현행 결함 목록에서 제외하고, 이전 기록을 보존할 때는 철회/범위 제외 사실을 명시한다.

### 8.7 카드 표시 문구

- 현재 사용 카드의 오른쪽 능력 안내는 기본적으로 `AbilityData.title`과 `desc`를 비워 숨긴다. 카드 본문에 “등교:” 또는 “하교:”가 명시된 경우에만 대응 능력에 각각 제목 “등교”·설명 “손패에서 소환 시”, 제목 “하교”·설명 “파괴 시”를 설정한다. OnPlay라는 이유만으로 이벤트 카드에 등교 설명을 추가하지 않는다. 카드 본문·선택 안내·상태 설명·범위 이미지는 이 규칙의 변경 대상이 아니다.

- 선택 안내(`selector_desc`)는 문장형 종결어미와 마침표 없이 “효과를 적용할 대상 지정”, “효과를 적용할 타일 지정”, “이동할 유닛 지정”, “이동할 타일 지정”, “소환할 타일 지정”처럼 짧은 “지정”형으로 통일한다. 효과 수치·결과·카드 이름·대상 조건을 반복하지 않고 선택 대상과 단계 구분에 필요한 정보만 남긴다. 선택 안내의 간결화는 대상 조건이나 게임 규칙을 변경하지 않는다.

- 카드 본문, 능력 설명, 선택 안내와 상태 표시명에 같은 용어를 사용한다. 자원은 청휘석, 카드 보유 영역은 손패, 보드 위치는 타일, 진행 단위는 턴, 수치 처리는 피해·회복으로 표기한다. `mana`, `IsCitizen()` 등 내부 식별자의 이름은 표시 용어와 별개로 유지한다.
- 발동 키워드는 “등교:”, “하교:”로 표기한다. 일반 문장은 “합니다” 체를 사용하며 범위는 “주변 1칸”처럼 일관되게 적는다. 학생 수는 “1명”, 카드 수는 “1장”, 장소를 포함할 수 있는 유닛 수는 “하나”처럼 대상에 맞게 쓴다.
- 동아리와 생성 카드의 고유명은 연결된 데이터의 표시명과 맞춘다. 동아리 소속 조건에 학생 제한이 없으면 “학생”으로 축약하지 않고 “소속 유닛”으로 적는다. 전체 유닛을 세는 조건과 학생만 세는 조건을 구분한다.
- 손패 생성, 기존 카드의 손패 이동, 덱에서 뽑기는 각각 생성·이동·뽑기로 구분한다. 수치 증가를 설명할 때는 공격력·체력을 명시하고, 소환 카드의 기본 수치 표기는 공격력/체력 순서를 따른다.
- 용어 정리에서는 조건·효과·수치·참조·GUID를 변경하지 않는다. `text_format`과 `text_values`, `<value>` 등 기존 치환자의 소비 경로를 확인하고, 설명을 고칠 때 실제 적용 범위와 다른 제한을 추가하거나 삭제하지 않는다. 설명 없는 카드에는 임의의 능력 설명을 만들지 않는다.
