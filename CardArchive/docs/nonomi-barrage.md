# 노노미, 총을 쏴 — 부채꼴 탄막

2026-09-19, Unity 6000.0.37f1 / CardArchive.

## 연결과 사용

`Nonomi_Fire`와 `Nonomi_Fire_Tutorial`이 사용하는 `spell_damage1_all_enemy`의 `board_fx`에
`Assets/TcgEngine/Prefabs/FX/Nonomi/NonomiBarrageFX.prefab`을 연결했다.
기존 `GameBoardFX.OnAbility`가 `onAbilityStart`를 받아 프리팹을 한 번 생성한다.
추가 씬 설정은 없다. 기존 적 대상 조건, 1 피해, 개별 피격 효과와 피격 음향은 유지한다.

화면 하단 중앙(뷰포트 0.5, 0.035)에서 112도 폭으로 21발씩 7회 연사한다.
간격은 0.052초, 탄환 수명은 약 0.38초, 전체 오브젝트 수명은 약 0.87초다.
총구 섬광과 작은 불꽃을 함께 재생하며 카메라 높이에 맞춰 크기를 계산한다.
파티클 시스템 3개를 사용하며 게임의 난수 상태를 소비하지 않는다.
시간 배율과 일시정지는 `Time.deltaTime` 및 Particle System 기본 시간에 따른다.
이 프리팹은 시각 연출만 담당한다. 탄환 수와 실제 피해 횟수는 무관하다.
사용자의 요청에 따라 상대의 시전도 화면 하단 중앙에서 재생한다.

## 미리보기 및 조절

Unity 메뉴 **Tools > Card Archive > FX > Nonomi Barrage Preview**.
Play Mode 없이 독립 미리보기 카메라에서 반복 재생하거나 Time 슬라이더로 확인한다.
실제 게임 씬에는 미리보기 오브젝트를 저장하지 않는다.

프리팹 루트의 `NonomiBarrageFX`에서 연사 횟수, 탄환 수, 부채꼴 각도,
연사 간격, 탄환 수명, 발사 높이, 탄환 색을 조절한다.
프리팹을 수정한 뒤 미리보기 창을 다시 열면 변경된 설정을 사용한다.
`NonomiTracer.shader`는 외부 텍스처 없이 밝은 탄두와 가늘어지는 잔광을 그린다.

## 검증

- Unity 컴파일 성공, 신규 Console 오류 0개.
- 실제 Game 카메라에서 0.08 / 0.20 / 0.36 / 0.70 / 0.95초 렌더링 확인.
- Play Mode 검증 21개 통과: 4:3·16:9·21:9 발사 위치, 파티클 상한,
  파티클 종료, 난수 보존, 두 카드의 프리팹 연결, 전체 필드의 적 대상 중복 없음,
  적마다 1 HP 감소, 아군·손패·플레이어 HP 유지, 적이 없는 경우,
  실제 클라이언트 이벤트를 통한 3회 발동과 오브젝트 자동 정리.
- 피해 검증은 별도의 메모리 Game 상태에서 기존 AbilityData/GetCardTargets/DoEffects를 사용했다.
  실제 네트워크 대전이나 손패 드래그를 통한 종단 검증 및 Player 빌드는 실행하지 않았다.
- 기존 카드 리소스 로딩의 `The referenced script (Unknown) ... is missing!` 경고는
  작업 전 에디터 로그에도 있었으며 이번 이펙트와 무관하다.

재검증: Game 씬의 로컬 Solo Play Mode에서 다음을 실행한다.

```powershell
unity command run_script --file Tools/NonomiBarrage/ValidateNonomiBarrage.cs --entry ValidateNonomiBarrage.Run --project-path "C:\Users\rlafu\Downloads\PPJ\GAME\CardArchive\CardArchive" --caller plugin --skill unity-cli --format json
```

## 연결 패키지 및 에디터 저장 변화

사용자 승인으로 `com.unity.pipeline` 0.7.0-exp.1을 설치했다.
Unity의 의존성 해결로 Mono.Cecil은 1.11.6, Newtonsoft.Json은 3.2.1이 되었다.
명령은 항상 위 CardArchive 프로젝트 경로를 명시한다. 별도의 NOVA 프로젝트가 동시에 열려 있으며,
기본 MCP 도구가 그 프로젝트를 가리킬 수 있으므로 대상을 추정하지 않는다.

에디터의 에셋 저장 과정에서 이미 로딩된 URP의 직렬화 갱신도 저장되었다.
`Renderer.asset`에는 현재 URP의 probe/depth 리소스 필드가 반영됐고,
`URP-HIGH/LOW/MEDIUM.asset`의 이전 버전 표기가 11에서 12로 갱신됐다.
게임 씬 파일은 변경하지 않았다. 기존 Live2D 및 ProjectSettings 변경은 보존했다.
