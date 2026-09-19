# Unity 6 경고 분석 및 수정

대상: `codex/unity-6-migration`, Unity `6000.0.37f1`, URP `17.0.3`.
작업일: 2026-09-18~19. 기존 미커밋 작업은 유지하며 마이그레이션 변경은 별도 커밋으로 관리한다.

## 분석 범위

마이그레이션의 임포트/컴파일/재생/빌드 로그와 사용자 에디터 로그를 모았다.
과거 로그의 중복을 제거하면 C# 경고는 27개 위치(CS0618 26개, CS0162 1개)였다.
이 중 `GameTool`의 렌더 파이프라인 조회 API는 최초 업그레이드에서 이미 수정됐고, 나머지는 이번 작업에서 수정했다.
기존 에디터 로그는 `Logs/Unity6Warnings/editor-before.log`, 초기 C# 목록은 `Logs/Unity6Migration/warnings-before.txt`에 보존했다.

## 원인과 해결

| 분류 | 원인 | 변경 |
| --- | --- | --- |
| 객체 검색 | `FindObjectOfType` / `FindObjectsOfType` 사용 중단 | `FindFirstObjectByType` / `FindObjectsByType(FindObjectsSortMode.InstanceID)`로 전환. 기존의 첫 객체 선택과 InstanceID 정렬 순서를 보존한다. 게임·리플레이·에디터 도구·Chart 예제에 적용했다. |
| TMP 줄바꿈 | `enableWordWrapping` 사용 중단 | `textWrappingMode`의 `Normal` / `NoWrap`으로 기존 true/false 동작을 보존한다. |
| TMP 커닝 | `enableKerning` 사용 중단 | `fontFeatures`에서 `OTL_FeatureTag.kern`만 제거하고 setter로 레이아웃 갱신을 요청한다. 다른 폰트 기능은 유지한다. |
| 화면 주사율 | 정수 `Resolution.refreshRate` 사용 중단 | `refreshRateRatio.value`를 사용하고 최대 소수점 두 자리로 표시한다. |
| Live2D | `return false` 다음의 실행 불가능한 `break` | 불필요한 `break`만 삭제했다. |
| 컴파일러 설정 | 구형 `Assets/mcs.rsp` | 동일한 `-unsafe` 설정이 `Assets/csc.rsp`에 이미 있어 구형 파일과 메타 파일만 제거했다. |
| URP 호환 모드 | Render Graph가 꺼져 있어 발생하는 경고 | 전역 설정의 Compatibility Mode를 해제했다. 현재 커스텀 Renderer Feature인 EPO에는 `RecordRenderGraph` 구현이 이미 포함돼 있다. |
| Deprecated 패키지 | 구형 `com.unity.ide.vscode` | 앞선 작업에서 의존성과 lock 항목을 제거했다. |
| VFX Shader Graph | `sh_simple_v3`의 Power 연산이 음수 입력을 받을 가능성. D3D11의 5개 렌더 패스에서 같은 원인을 보고 | 두 Power 노드의 밑 입력에 Absolute 노드를 추가했다. 0 이상 입력은 변하지 않으며, 음수 입력은 절댓값으로 처리한다. 상한을 자르거나 경고를 억제하지 않는다. |

패키지 캐시나 컴파일러 경고 억제 설정을 수정하지 않았다.
포함된 Chart/TMP/Live2D 소스에 대한 수정은 플러그인을 다시 가져올 때 덮어쓰지 않도록 확인해야 한다.

## 검증

- `ReplayValidation.Run`: 컴파일과 상태 직렬화/복원, 난수, 실제 경기 시뮬레이션 PASS.
- `ReplayPlaybackValidation.Run`: Render Graph 활성 상태에서 양쪽 플레이어 시점의 실제 씬 재생 PASS, 오류 0.
- `Unity6RenderingValidation.Run`: Low/Medium/High/Ultra에서 흰 물체와 녹색 외곽선을 실제 RenderTexture에 렌더링하고 픽셀을 검사했다. 각 품질에서 외곽선 688픽셀, 물체 7,056픽셀. 런타임 경고/오류 0, 종료 코드 0.
- 렌더링 검증 이미지와 결과: `Logs/Unity6Warnings/rendering/`.
- 컴파일, 재생, 외곽선 검증 로그: `compile-fixed.log`, `playback.log`, `rendering-fixed.log` (`Logs/Unity6Warnings/` 아래).
- 최종 Windows x64 빌드: `Build Finished, Result: Success`, 종료 코드 0. C# 및 셰이더 경고 0건. 로그: `Logs/Unity6Warnings/build-final.log`. 출력: `Builds/Unity6Warnings/CardArchive.exe` 및 동일 폴더의 런타임/데이터 파일.
- 최종 4개 검증 로그의 프로젝트 경고는 모두 0건이다. 요약: `Logs/Unity6Warnings/warning-summary.json`. 빌드 보고서가 나열하는 `*.deprecated.cs`/`Obsolete/` 파일명은 진단 메시지가 아니므로 경고 집계에서 제외했다.
- Shader Graph의 오브젝트 ID 중복 및 모든 연결의 노드/슬롯 참조가 유효한지 확인했다.

검증 도구는 배치 모드 전용이다. 품질을 원래 값으로 복원하며 프로젝트 씬을 저장하지 않는다.

```powershell
# 다른 Unity 프로세스에서 이 프로젝트를 닫은 상태로 실행한다.
& 'C:/Program Files/Unity/Hub/Editor/6000.0.37f1/Editor/Unity.exe' -batchmode -quit -projectPath 'C:/Works/CardArchive/CardArchive' -executeMethod Unity6RenderingValidation.Run -logFile 'C:/Works/CardArchive/CardArchive/Logs/Unity6Warnings/rendering-fixed.log'
```

## 환경 진단 및 검증 한계

- 최초 `-nographics` 임포트의 VFX/UI 및 Terrain Unsupported 메시지는 그래픽 장치 없는 실행에서 발생했다. GPU 사용 검증으로 확인하며, 패키지 셰이더를 임의 수정하지 않았다.
- 에디터 시작 시 Licensing Client의 서명/액세스 토큰 진단이 남아 있다. 프로젝트 코드 경고와 별개이며 Unity는 entitlement를 확인한 뒤 검증을 실행했다. 라이선스/계정 설정은 변경하지 않았다.
- 검증 환경은 Windows, NVIDIA RTX 4070 Ti, Direct3D 11이다. Android/Linux 빌드와 온라인 대전, 전체 VFX·Live2D·Spine의 육안 비교는 별도 확인이 필요하다. 특히 비정상적인 음수 텍스처 값을 사용하던 효과는 새 절댓값 처리로 표시가 달라질 수 있다.
- 이 결과는 실행한 경로에서 발생하는 경고에 대한 검증이다. 모든 플랫폼·모든 게임 상태에서 미래 경고까지 없다는 보장은 아니다.

## 참고

- [Unity: URP 17 업그레이드와 Render Graph](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/upgrade-guide-unity-6.html)
- [Unity: Object.FindObjectsByType](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Object.FindObjectsByType.html)
- TMP fontFeatures 및 Shader Graph Absolute/Power 동작은 설치된 uGUI 2.0.0 / Shader Graph 17.0.3 패키지 소스에서 확인했다.
