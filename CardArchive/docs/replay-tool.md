# 대전 기록과 Replay Tool

## 실행

1. Unity 메뉴 **Tools → Card Archive → Open Replay Tool**을 선택하고 Play한다.
2. **Viewpoint username**에 경기 참가자의 계정명을 입력한다.
3. API 주소를 확인하고 **Load user's latest recordings**를 누른다. 조회에는 로그인·토큰·권한·참가자 검사가 없다. 입력한 사용자명의 목록과 선택한 경기 원본을 바로 요청한다.
4. 목록에서 경기를 선택하면 `ReplayGame` 씬이 해당 참가자 시점으로 열린다.
5. Pause/Resume, 0.5/1/2/4배속, Restart, Tool 복귀를 사용할 수 있다. 좌측 상단 Replay controls / Hide replay controls 버튼으로 설정 패널을 접거나 펼친다. 접어도 배속·일시정지 상태는 유지된다.

인게임 메뉴·결과 화면에는 진입 버튼을 추가하지 않았다. 씬 파일은 다음과 같다.

조회에는 아래 설정이 필요 없다. 에디터에서 로컬 API로 기록을 **업로드**할 때 서버 인증을 생략하려면 `.env`에 다음 두 값을 설정하고 API를 재시작한다.

```dotenv
NODE_ENV=development
REPLAY_LOCAL_TOOL_BYPASS=true
```

이 옵션은 리플레이 업로드 경로에만 적용된다. 같은 컴퓨터의 직접 연결과 에디터 툴 요청 표식이 모두 있어야 업로드 인증을 생략하며 브라우저 Origin과 프록시 전달 헤더가 있는 요청은 제외한다. 기본값은 비활성이며 운영 모드와 원격 업로드는 서버 인증이 필요하다. 조회는 환경에 관계없이 인증을 검사하지 않는다.

- `Assets/TcgEngine/Resources/Scenes/Tool/ReplayTool.unity`
- `Assets/TcgEngine/Resources/Scenes/Tool/ReplayGame.unity`

`ReplayGame`은 현재 `Game.unity`를 기반으로 한 별도 씬이며 같은 스크립트·프리팹·에셋을 참조한다. 기존 Game 씬의 배치가 변경되면 ReplayGame 배치에도 반영해야 한다. 에디터에서는 Build Settings 등록 없이 Tool을 사용할 수 있다. 별도 Tool 빌드를 만들 때는 두 씬을 해당 빌드에 포함한다. 일반 배포 빌드의 씬 목록은 수정하지 않았다.

## 기록 시작과 종료

`GameLogic.StartGame()`에서 선공·체력·코스트·동아리·초기 배치와 play order 설정이 끝난 뒤, 게임 시작 효과 및 최초 드로우 전에 초기 상태를 기록한다. 이후 드로우·멀리건·선택·카드 사용·공격·피해·연쇄 효과·사망·최종 종료 상태를 순서대로 기록한다.

`GameServer`마다 기록기가 하나 존재한다. 실제 게임 로직만 기록하며 AI 예측 트리는 기록하지 않는다. 효과 처리 경계와 서버 사건 발행 시 public 상태 필드를 비교하여 변경된 경로와 값만 저장한다. 매번 전체 상태를 저장하거나 전투 로직을 다시 실행하지 않는다.

`GameEnd` 알림 직후 파일을 닫지 않는다. 해당 서버 업데이트가 끝난 뒤 최종 상태까지 반영하여 압축·저장한다. 기록 오류가 나면 경기는 계속되며 불완전한 기록을 정상 리플레이로 저장하지 않는다.

## 저장 형식 v1

```text
ReplayRecord
  formatVersion, buildVersion
  matchId, players[]
  initialState: public-field state tree
  events[]:
    state       changes[{ path[], value }]
    event       packet (known GameAction presentation message)
    interaction kind, player, card, target, slot, choice, cards[]
    random      randomResult
  finalHash
```

순서는 배열 순서다. atMs나 마우스 경로는 저장하지 않는다. 상태 트리에는 전체 카드 정보·덱 순서·각 영역·장비·보스 상태 등이 포함된다. public 필드를 선언된 타입으로 복원하며, Unity 에셋·private 캐시·델리게이트·난수 객체는 직렬화하지 않는다. 카드 UID별로 복원 객체를 공유한다.

상태 본문은 명시적인 데이터 트리이며 BinaryFormatter를 사용하지 않는다. 표시 사건의 payload는 현재 Netcode 메시지의 바이트다. Connected/RefreshAll 등 전체 객체 역직렬화 메시지는 재현 시 거부한다. 따라서 프로토콜·콘텐츠가 변경될 때는 애플리케이션 버전 및 필요 시 formatVersion을 올려야 한다. v1은 같은 애플리케이션 버전만 지원하며 과거 콘텐츠 호환 레이어는 포함하지 않는다.

## 난수와 선택

실제 `System.Random`의 정수·실수·바이트 결과도 기록한다. 효과가 실패하여 상태 변화가 없는 확률 판정도 남는다. 셔플·생성·피해 등의 적용 결과는 상태 변경분에 포함된다. 재현은 RNG와 GameLogic을 호출하지 않고 확정값을 적용한다.

카드 선택 후보는 선택 창을 열 때 서버에서 한 번 확정하여 `selector_card_uids`에 저장한다. 클라이언트 표시·서버 선택 검증·재현이 같은 후보 목록을 사용한다. 선택 창을 다시 계산하며 필터 난수를 재추첨하는 것을 방지한다.

## 시점과 인터랙션

원본은 경기당 하나이며 양쪽의 전체 정보를 가진다. Tool에서 고른 플레이어 ID로 기존 손패·보드·선택 UI를 표시하고 상대 비공개 멀리건·선택 과정은 표시하지 않는다. 조회 API는 로그인·권한·참가 여부를 검사하지 않으며 사용자명으로 목록을, 경기 ID로 원본을 반환한다. 다운로드 본문은 양쪽 정보를 포함하는 원본이고 상대 정보 숨김은 게임씬의 표시 정책이다. 업로드는 서버 권한을 유지한다.

수락된 카드 사용·이동·대상/카드/선택지 선택·멀리건을 기록한다. 카드 사용과 이동은 출발점에서 목표 슬롯까지 직선으로 이동하고, 선택은 눌림과 기존 선택 애니메이션으로 나타낸다. 리플레이에서는 마우스를 따라가는 대상 커서·연결선과 별도의 포인터를 표시하지 않는다. 실제 입력 대기·망설임·실패한 드롭은 제외한다. 조작 표현 후 상태 변경과 사건을 순서대로 적용한다. 이펙트 파일이나 프레임을 기록하지 않는다.

Replay 모드에서는 네트워크 접속, 로컬 서버, AI 진행, 실제 입력 전송, 보상 처리를 실행하지 않는다. 피해 사건의 기존 클라이언트 예측 변경도 생략하여 기록된 피해가 두 번 적용되지 않는다. 재현 전용 씬의 timeScale로 이벤트·드래그·Tween·대기를 함께 제어하며 원래 경기의 턴 타이머를 로컬에서 감소시키지 않는다.

## DB와 보관

`config.js`의 `replay_limit_per_user` 기본값은 **10**이다.

- `Replays`: matchId(유일), players, gzip payload, digest.
- 기존 `Users`: `replay_match_ids`에 최신 저장 순서로 최대 10개 참조.
- 저장과 참조 갱신·정리는 Mongo transaction 안에서 처리한다. 기존 API와 동일하게 replica set 구성이 필요하다.
- 동일한 matchId·본문의 재시도는 성공으로 처리하고 목록 순서를 바꾸지 않는다. 다른 본문으로 덮어쓰기는 거부한다.
- 어느 사용자도 참조하지 않는 오래된 원본만 삭제한다. 기존 Matches 승패·보상 기록은 삭제하지 않는다.

API:

| 메서드 | 경로 | 용도 |
|---|---|---|
| POST | `/replays` | 완료 기록 업로드 |
| GET | `/replays/user/:username` | 최신 경기 ID 목록 |
| GET | `/replays/:matchId` | 경기 원본 조회 |

압축 본문은 현재 최대 12MiB, 해제 본문은 최대 128MiB로 검증한다. 초과 기록은 저장 오류로 보고되며 조용히 잘라 저장하지 않는다.

전용 서버는 완료 기록을 먼저 `Application.persistentDataPath/Replays`에 outbox로 남기고 업로드를 세 번 시도한다. 성공하면 outbox 파일을 제거한다. 실패 파일은 유지하며 Tool의 Upload로 재시도할 수 있다. 로컬/솔로 경기는 같은 위치에 파일로 저장하여 Tool에서 열 수 있다. 로컬 파일/outbox에는 DB의 최신 10개 삭제 정책을 적용하지 않는다.

경기 중 서버가 종료되면 메모리의 진행 중 기록은 유실된다. 저장은 경기 종료 후에만 수행한다.

## 검증

- `node --test Api/TcgEngineAPI/replays/replays.test.js`
- `node --test Api/TcgEngineAPI/replays/replays.access.test.js Api/TcgEngineAPI/replays/replays.public.test.js` (업로드 인증 생략 조건과 공개 조회 HTTP 검증)
- `node --test Api/TcgEngineAPI/replays/replays.integration.test.js` (API 개발 의존성 설치 필요. 최초 실행 시 MongoDB 테스트 바이너리를 다운로드하고 격리된 임시 replica set을 실행한다. 운영 DB는 사용하지 않는다.)
- `dotnet run --project Tools/ResolveQueueTests`
- Unity 메뉴 **Tools → Card Archive → Validate Replay Codec** 또는 배치 `-executeMethod ReplayValidation.Run -quit`
- 실제 게임씬 재현 배치: `-executeMethod ReplayPlaybackValidation.Run` (자동 종료하므로 `-quit` 미사용)

Unity 검증 결과와 실제 테스트 기록은 `Library/ReplayValidation`에 생성된다. 상태 직렬화·공유 카드 참조·변경분·난수 결과·압축·재시작과 실제 카드 데이터로 진행한 전투의 최종 해시 일치를 검사한다. 게임씬 검증은 같은 기록을 두 참가자 시점으로 재현하고 네트워크가 시작되지 않는지 확인한다.

현재 상태: API 단위 테스트 4개, 효과 큐 회귀 테스트 74개, Unity 상태 복원 및 양쪽 시점의 실제 씬 재현 검증이 통과했다. 씬 검증은 배치 실행으로 수행했으며 화면의 외형을 수동 검수한 결과는 아니다. MongoDB 통합 테스트는 작성했지만 테스트 바이너리 다운로드·실행 권한이 허용되지 않아 실행을 완료하지 못했다. 운영 API 배포도 수행하지 않았다.

실제 전투 종료 검증에서 발견한 효과 큐의 Clear 후 스택 Pop 문제도 수정했다. 공격·어빌리티 콜백 중 게임 종료가 발생하면 폐기된 스코프를 다시 처리하지 않는다. 관련 회귀 검증은 ResolveQueueTests에 포함된다.

공개 조회 변경 검증: 인증 없는 HTTP 목록·원본 조회, 잘못된 토큰을 보내도 조회 허용, 없는 경기의 404, 업로드 인증 유지와 기존 검증을 포함한 테스트 8개가 통과했다. HTTP 검증에서 DB 조회는 테스트 대역을 사용했다. 이 변경 이후 실제 DB 통합 테스트와 Unity 재컴파일은 실행하지 않았다.

## 카드 연출 차이의 원인과 수정

### 경기 종료 표시와 메뉴 복귀

리플레이의 모든 항목 적용과 최종 상태 해시 검증이 성공하고 경기 종료 상태이면, 기존 `GameBoard.EndGame()`을 호출한다. 선택한 플레이어 시점의 승리·패배 연출, 사운드, 결과창을 그대로 사용한다. 종료 연출부터는 1배속으로 고정하고 결과창이 표시되면 UI 클릭을 다시 허용한다. 결과창의 나가기 버튼은 기존 페이드 후 메인 메뉴로 이동하며, 이동 직전에 리플레이 세션을 정리한다. 툴로 돌아가는 버튼은 기존처럼 별도로 유지한다.

리플레이에서는 결과창의 보상 조회를 생략하며 실제 보상 지급도 기존 차단을 유지한다. 공통 종료 연출에서 패배 이펙트가 무승부 조건으로 잘못 검사되던 부분도 수정했다. Unity C# 컴파일 검증은 통과했으며, 이 종료 흐름의 실제 씬 재생·클릭 검증은 아직 수행하지 않았다.

기존 구현은 서버 상태와 사건 이펙트를 재사용했지만 드래그는 별도 `ScreenSpaceOverlay` 캔버스에 `Image`/`Text`를 복사해 표현했다. 원본 손패를 그대로 둔 채 복사본을 움직여 카드가 두 장처럼 보였다. 이 복사는 TMP·마스크·CanvasGroup·캔버스 스케일 같은 설정을 온전히 옮기지 않았고, 회전된 화면 경계로 크기를 계산했다. 상대 손패는 실제 뒷면 오브젝트 대신 120×165 이미지로 대체해 기본 뒷면과 비율도 달라질 수 있었다.

수정 후에는 실제 `HandCard`, `HandCardBack`, `BoardCard`를 원래 부모·캔버스 안에서 이동한다. 이동 중 자동 위치 갱신만 잠시 멈추고 기록 상태가 손패 제거·슬롯 이동을 확정할 때 기존 표시 흐름으로 넘긴다. 손패의 기존 hand/board 두 표현과 원래 뒷면 프리팹을 사용하며 그래픽 복사본을 만들지 않는다. 상대 손패는 실제 클라이언트처럼 카드 개수만 표시하므로 마지막 시각 카드를 사용하고 비공개 카드의 정체나 원래 손패 위치는 드러내지 않는다.

대상 이펙트는 `AimTargetFX`와 `MouseLineFX`가 입력 컴포넌트와 독립적으로 매 프레임 마우스 좌표를 읽기 때문에 입력 차단만으로 숨겨지지 않았다. 이제 리플레이 세션에서만 대상·설명·연결선을 숨기고 해당 갱신을 생략한다. 일반 플레이의 경로는 유지한다.

이 수정은 Unity 컴파일러로 검증했다. 실제 씬 검증기에도 원본 카드 오브젝트·부모 캔버스·입력 잠금·대상 커서 숨김 검사를 추가했다. 이번 수정 후 실제 게임씬 재현과 화면 외형 검수는 아직 수행하지 않았다.
