# Unity 6 migration

검증일: 2026-09-18

## 대상

- 브랜치: `codex/unity-6-migration`
- 이전: Unity `2022.3.32f1`
- 이후: Unity `6000.0.37f1 (090b7797214c)` — 로컬에 설치된 Unity 6 버전
- 기존 미커밋 작업을 유지한 상태에서 진행했으며, 마이그레이션 변경은 게임 로직·데모 작업과 분리해 커밋한다.

## 변경

Unity 에디터의 프로젝트 업그레이드 및 API Updater를 사용했다.

| 패키지 | 이전 | 이후 |
| --- | --- | --- |
| URP / Core / Shader Graph | 14.0.11 | 17.0.3 |
| Addressables | 1.25.1 | 2.2.2 |
| Netcode for GameObjects | 1.9.1 | 1.12.0 |
| Recorder | 4.0.3 | 5.1.2 |
| uGUI | 1.0.0 | 2.0.0 |
| Timeline | 1.7.6 | 1.8.7 |
| Test Framework | 1.1.33 | 1.4.5 |

전체 패키지 버전은 `Packages/manifest.json` 및 `Packages/packages-lock.json`에 기록돼 있다.
별도 TextMeshPro 패키지 의존성은 Unity 업그레이드가 제거했으며 uGUI 2.0의 TMP를 사용한다.

후속 정리: deprecated 알림의 원인인 `com.unity.ide.vscode` 1.2.5를 제거했다. 프로젝트 코드에서 직접 참조하지 않는 구형 에디터 연동 패키지이며, 기존 `com.unity.ide.visualstudio` 2.0.22는 유지한다. 이후 경고 정리와 재검증 결과는 [Unity 6 경고 분석](unity6-warnings.md)에 기록한다.

- `GameTool.IsURP()`: API Updater가 `GraphicsSettings.renderPipelineAsset`를 `GraphicsSettings.defaultRenderPipeline`으로 변경했다.
- TMP 예제 `TMP_TextSelector_B.cs`, `VertexZoom.cs`: UV0가 `Vector4[]`로 바뀌어 발생한 컴파일 오류를 수정했다. 배열은 `var`로 받고 메시에는 `SetUVs(0, ...)`를 사용해 네 성분을 보존한다.
- ProjectSettings, URP 전역 설정, 사용된 URP 에셋과 머티리얼은 에디터가 새 형식으로 변환했다.
- 최초 마이그레이션은 호환 모드로 검증했다. 이후 경고 정리에서 **Render Graph Compatibility Mode를 해제**했으며 실제 씬 재생 및 4개 품질 설정의 외곽선 렌더링을 검증했다.

## 검증 결과

| 검증 | 결과 | 기록 |
| --- | --- | --- |
| 에셋 임포트 및 에디터 컴파일 | 성공, 종료 코드 0 | `Logs/Unity6Migration/validation.log` |
| `ReplayValidation.Run` | PASS — 상태 직렬화, 참조 복원, 델타, 난수, 압축, 재시작, 최종 해시 및 실제 경기 시뮬레이션 | `Library/ReplayValidation/replay-validation.txt` |
| `ReplayPlaybackValidation.Run` | PASS — 실제 씬에서 양쪽 플레이어 시점 재생, 오류 0 | `Library/ReplayValidation/replay-playback-validation.txt` 및 `Logs/Unity6Migration/playback.log` |
| Windows x64 플레이어 빌드 | `Build Finished, Result: Success`, 종료 코드 0 | `Logs/Unity6Migration/build-windows.log` |

빌드 출력: `Builds/Unity6Migration/CardArchive.exe` 및 같은 폴더의 데이터/런타임 파일.
실제 씬 재생 검증은 NVIDIA RTX 4070 Ti / Direct3D 11 에디터 배치 모드에서 실행했다.
빌드된 실행 파일을 직접 실행한 수동 플레이 검증은 하지 않았다.

## 재현

프로젝트 루트에서 PowerShell로 실행한다. 같은 프로젝트가 다른 Unity 프로세스에서 열려 있으면 먼저 닫는다.

```powershell
$unityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.0.37f1/Editor/Unity.exe'
$projectPath = (Get-Location).Path
& $unityEditor -batchmode -nographics -quit -projectPath $projectPath -executeMethod ReplayValidation.Run -logFile "$projectPath/Logs/Unity6Migration/validation.log"
# 위 에디터가 종료된 뒤 실행. 재생 검증은 자체적으로 에디터를 종료하므로 -quit를 붙이지 않는다.
& $unityEditor -batchmode -projectPath $projectPath -executeMethod ReplayPlaybackValidation.Run -logFile "$projectPath/Logs/Unity6Migration/playback.log"
# 재생 검증 에디터가 종료된 뒤 실행.
& $unityEditor -batchmode -quit -projectPath $projectPath -buildWindows64Player "$projectPath/Builds/Unity6Migration/CardArchive.exe" -logFile "$projectPath/Logs/Unity6Migration/build-windows.log"
```

## 남은 확인

- Windows 실행 파일의 직접 플레이, 화면 비교, Live2D/Spine 애니메이션 및 카드 효과의 시각적 품질.
- 서버에 연결한 온라인 대전 및 기존 저장 데이터 호환성. Netcode/Transport 패키지가 변경됐으므로 실제 클라이언트/서버 조합으로 확인해야 한다.
- Android 및 Linux 서버 빌드는 검증하지 않았다.
- `FindObjectOfType`, TMP 일부 속성, `Resolution.refreshRate` 등의 사용 중단 예정 API 경고는 후속 경고 정리에서 수정했다. 세부 내용은 [Unity 6 경고 분석](unity6-warnings.md)을 참고한다.
- 초기 `-nographics` 임포트에서는 VFX/UI 셰이더의 Unsupported 경고가 나왔다. 이후 GPU 사용 씬 재생과 Windows 빌드는 성공했지만 전체 효과의 육안 비교를 대체하지는 않는다.

## 기존 작업 보존

`Logs/Unity6Migration/before/`에 작업 시작 시점의 diff, 상태 목록, 미커밋 파일, Packages 및 ProjectSettings를 백업했다. 이 폴더는 Git에서 제외되는 로컬 백업이며 전체 프로젝트 복사본은 아니다.
기존 변경 사항은 다른 작업과 섞여 있으므로 일괄 reset/checkout으로 되돌리지 않는다.
브랜치 생성만으로 미커밋 변경이 격리되는 것은 아니므로 다른 브랜치로 전환하기 전에 작업 내용을 별도로 보존해야 한다.
