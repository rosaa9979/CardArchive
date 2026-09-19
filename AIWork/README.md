# AI 작업 기록

AI와 함께 작성한 분석, 설계, 마이그레이션 및 영상 작업 Markdown을 모은다.
기존 `CardArchive/docs` 문서는 이 폴더로 이동했다. 문서 본문의 `Assets/`,
`Library/`, `Logs/` 경로는 별도 표기가 없으면 Unity 프로젝트 `CardArchive/` 기준이다.
과거 결과와 초안은 기록으로 보존하며 현재 기능과 다를 수 있다.

## Unity 마이그레이션

- [Unity 6.6 정리와 검증](unity66-migration.md)
- [Unity 6.0 마이그레이션](unity6-migration.md)
- [Unity 6.0 경고 분석](unity6-warnings.md)

## 리플레이

- [Replay Tool](replay-tool.md)
- [데모 시나리오와 재생](demo-scenario.md)
- [어그로 데모 분석](demo-showcase-20260914111522-aggro.md)
- [저장된 샘플](../CardArchive/Samples/Replays/README.md)

## 동영상

- [편집 소스](../CardArchive/Tools/VideoEditing/README.md)
- [편집 가이드](video-editing-guideline.md)
- [섹션과 자막](video-current-sections-and-subtitles.md)
- [자막 초안](video-subtitle-script.md) / [v5 초안](video-subtitle-script-v5.md)
- [공격 예시 검토](video-attack-examples-review.md)
- [v9 동기화 검토](video-v9-normal_speed_sync_audit.md)
- [v9 촬영 계획](video-v9-shooting_plan.md)

## 게임 구조·개발 도구

- [전투 흐름](combat-flow.md) / [효과 처리](effect-resolution-flow.md)
- [ResolveQueue 설계](resolve-queue-hearthstone-redesign.md)
- [Trigger와 Ongoing](trigger-vs-ongoing.md)
- [조건·효과 파라미터](ConditionEffectParameters.md)
- [조건 데이터 테이블](condition-family-datatable-schema.md)
- [효과 테스트 씬 작업 기록](effect-test-scene.md) (관련 코드 작업은 별도 미커밋 상태)

## 보관 원칙

완료된 일회성 생성·변환 코드는 제거하고 결과물과 재사용 가능한 도구를 남긴다.
검증용 로그/빌드/MP4/미디어 캐시는 커밋하지 않는다. 영상 편집 원본과 자막,
작업 문서는 영상 커밋에, 리플레이 샘플과 의존 에셋은 리플레이 커밋에 포함한다.
