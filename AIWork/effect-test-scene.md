# 효과 테스트 씬

Unity 메뉴 **Tools → Card Archive → Open Effect Test Scene**으로 열고 Play한다.

씬: `Assets/TcgEngine/Resources/Scenes/Tool/EffectTestGame.unity`

1. 기존 테스트 덱으로 로컬 게임을 시작하고 멀리건을 완료한다.
2. 패널에서 소유자 Player 0/1과 Hand/Field를 선택한다.
3. 카드 이름 또는 ID로 검색해 카드를 고른다. 필드는 학생·비학생·건물만 표시한다.
4. 필드에 추가할 때는 빈 슬롯을 선택한다. 슬롯 좌표의 P는 위치 구역이며 카드 소유자는 별도로 지정된다.
5. **Add card without triggering effects**를 누른다. 패널은 **Hide effect test**로 접을 수 있다.

AI는 기본 정지이고 턴 타이머도 정지한다. AI의 초기 멀리건은 진행된다. **End current main phase**로 현재 소유자와 관계없이 정상 게임 로직의 다음 단계를 진행하거나 AI 정지를 해제할 수 있다. 다음 단계에서 발생하는 효과는 정상 발동한다.

카드 추가는 실제 로컬 서버의 상태를 수정한다. Card.Create로 UID·기본 능력·스탯과 cards_all 참조를 생성하고, 선택한 소유자의 cards_hand/cards_board에 넣는다. 필드 카드는 이후 효과 처리 순서를 위한 play_order를 부여한다. 일반 PlayCard/SummonCard/DrawCard 경로를 호출하지 않으므로 카드 사용·소환·드로우와 관련 능력, 비용 지불은 발생하지 않는다. 별도의 소환 알림도 보내지 않고 기존 클라이언트의 상태 동기화로 표시한다.

추가 당시에는 UpdateOngoing도 호출하지 않는다. 이 함수에는 순수 스탯 계산 외에 효과 실행과 카드 제거가 포함되기 때문이다. 지속 효과는 이후 정상 게임 로직에서 재계산된다. 카드를 추가한 뒤 실제로 사용하거나 전투·턴을 진행하면 그때의 효과는 정상 작동한다.

효과 처리 중 상태가 섞이지 않도록 메인 단계이며 대상 선택·효과 큐·명령 대기가 없는 시점에만 추가할 수 있다. 점유 슬롯은 덮어쓰지 않는다. 비용·손패 제한·일반 배치 소유권 제한은 치트 추가에 적용하지 않는다.

기존 Game 씬을 기반으로 같은 클라이언트와 프리팹을 사용한다. 씬 레이아웃 변경 시 함께 반영해야 한다. 에디터 전용이며 일반 빌드에 치트 API나 패널이 포함되지 않는다. 원격 서버나 계정 덱 조회 없이 기존 오프라인 통신 경로를 사용하고, 게임 설정 에셋은 변경하지 않는다. 일반 배포 Build Settings에 이 씬을 추가하지 않았다.

검증: Unity 컴파일러로 런타임·에디터 코드를 컴파일했다. **Tools → Card Archive → Validate Effect Test Insertion**은 양쪽 소유자·손패/필드·공유 참조·트리거와 비용 미발생·점유 슬롯 및 단계 제한을 검증한다. 결과는 `Library/EffectTestValidation/result.txt`에 기록된다. 이번 작업에서는 실행 중인 Unity 세션을 변경하지 않았으며 실제 씬 실행과 해당 검증 메뉴 실행은 아직 수행하지 않았다.
