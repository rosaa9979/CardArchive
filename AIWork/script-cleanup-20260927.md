# 일회용 제작 스크립트 정리 (2026-09-27)

완료된 영상·데모·PPT 제작 및 Unity 수정 작업에 묶인 스크립트 115개와 Python 캐시 30개를 로컬에서 삭제했다.
삭제 대상은 Git 제외 경로 또는 저장소 밖에 있었으므로 파일 삭제 자체는 Git 커밋에 포함되지 않는다.

## 삭제한 작업용 코드

| 위치 | 스크립트 수 | 용도 |
|---|---:|---|
| `CardArchive/Library/VideoAnalysis/` | 94 | 이전 영상 버전 전용 렌더링, 자막·화면 수정, 검수, 특정 카드 추출 |
| `CardArchive/Library/DemoScenario/` | 10 | 특정 데모 생성·녹화·검수 및 메모 수정 |
| `CardArchive/Logs/` | 3 | Unity 셰이더 일회성 수정 및 이전 변경의 커밋 준비 |
| 사용자 Documents/Codex의 2026-09-10 PPT 작업 `work/` | 8 | 특정 PPT 편집·디자인·검사·렌더링 |

데모 전용 8개와 PPT 검사·렌더링 6개는 재사용 여부를 확인한 뒤 사용자의 삭제 선택에 따라 정리했다.

## 유지한 도구

- `CardArchive/Library/VideoAnalysis/full_draft_v13/docs/contact.py`: 입력 영상과 시간 범위를 인자로 받는 미리보기 모음 생성기. 출력 위치는 기존 폴더로 고정되어 있다.
- `CardArchive/Library/VideoAnalysis/full_draft_v13/docs/static_attack_revision/CodexEnemyExport.cs`: 적군 보드 카드 일괄 출력 도구. 기존 Unity 도구 씬에서 사용한다.
- `CardArchive/docs/_gen_csv.py`: 조건·효과 문서 생성기.
- `CardArchive/Tools/ResolveQueueTests/`: 회귀 테스트.
- Assets의 카드 출력·효과 테스트 도구, API·서버 코드, 설치된 플러그인과 외부 패키지.

## 보존 및 검증

현재 영상 프로젝트는 `Video/CardArchive/`에 있으며 연결 미디어 139개의 존재를 확인했다.
`Video/` 전체는 Git에서 제외한다.
기존 영상·이미지·문서·PPT 결과물과 Logs의 과거 코드 백업은 제작 스크립트 삭제 대상에서 제외했다.
따라서 Library/VideoAnalysis의 대용량 중간 영상은 이번 정리로 삭제되지 않았다.
게임 실행 코드는 바꾸지 않았으며 Unity 실행 테스트는 하지 않았다.
