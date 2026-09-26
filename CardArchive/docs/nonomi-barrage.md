# 노노미, 총을 쏴 — 스프라이트 시트 이펙트

`Nonomi_Fire`와 `Nonomi_Fire_Tutorial`의 공용 능력
`spell_damage1_all_enemy.board_fx`가 `NonomiBarrageFX.prefab`을 사용한다.
기존 적 전체 1피해, 개별 피격 효과와 피격음은 그대로다.

## 에셋 구성

- `Assets/TcgEngine/Prefabs/FX/Nonomi/NonomiBarrageFX.prefab`
- `Assets/TcgEngine/Prefabs/FX/Nonomi/NonomiBarrageSheet.png`

기존 금빛 부채꼴 탄막을 8열 × 6행, 48프레임 시트에 구웠다.
프레임 순서는 왼쪽 위부터 오른쪽으로, 다음 행으로 내려간다.
프레임당 512 × 288픽셀, 전체 4096 × 1728픽셀이다.
Unity 기본 Particle System의 Texture Sheet Animation이 60fps로 0.8초 동안
한 번 재생한다. 파티클은 정지한 사각형 한 개이며, 탄환·총구 섬광·불꽃은
모두 이미지에 포함된다. Stop Action은 Destroy다.

머티리얼은 프리팹 내부에 포함하며 Unity 기본 URP 파티클 셰이더를 사용한다.
노노미 전용 C# 스크립트, 커스텀 셰이더, Animator, 별도 머티리얼 파일은 없다.
전용 미리보기와 일회성 검증·변환 스크립트도 프로젝트에 남기지 않는다.
미리보기는 Unity의 프리팹 모드에서 기본 파티클 재생 기능을 사용한다.

기준 배치는 게임의 기본 직교 카메라(크기 5.4), 월드 원점이다.
시트의 화면 크기는 19.2 × 10.8 월드 단위이며 발사점은 (0, -5.022)다.
아군·상대 모두 하단에서 위로 발사한다. 카메라를 따라 위치나 크기를
재계산하는 코드는 없으므로 카메라 크기를 바꾸면 프리팹 크기도 조절해야 한다.
연사 횟수나 탄환 궤적은 이미지에 구워져 있으므로 시트를 편집해 변경한다.
