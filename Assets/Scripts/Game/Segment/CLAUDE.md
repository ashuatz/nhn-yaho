# Segment/ - 배경 장식과 조도

네임스페이스: Scavenger.Segment

필드 생성(존/바닥/절차 생성)은 Field/로 이전됨 (ADR-0009).
이 폴더는 걸을 수 없는 장식(배경 블록), 조도, 스테이지 끝 웨이포인트만 담당한다.

## 파일 목차

- SegmentEnvironment.cs: 배경 PCG 데이터 생성기 (ADR-0003/0004/0005).
  Palette는 **기준색**(색상 + 채도)이고 밝기는 Field/GreyboxTheme이 정한다
  (창: Scavenger > Greybox Brightness). 여기 값을 직접 올리면 조절값을 덮어쓴다.
  PaletteNames / PaletteName(i) = 팔레트 역할 이름 (RubbleDark 등).
  사전 배치 머티리얼 에셋 이름이 여기서 나온다 (Env_00_RubbleDark.mat -
  색상 해시 이름은 어디에 쓰이는지 알 수 없다, 사용자 지시 2026-07-26).
  GenerateBlocks(ZoneDefinition, rng, clearance[, segmentLength]) = 인스턴스 블록
  (행렬 + 팔레트 8색 + 웨이브/위상) 생성. GameObject를 만들지 않는다 (웹 호환 인스턴싱).
  segmentLength를 넘기지 않으면 존 길이 - 존보다 짧은 세그먼트(존 사이 구간)는
  반드시 실제 길이를 넘겨야 한다 (안 넘기면 다음 세그먼트 배경과 겹쳐 그려진다).
  좌우 비대칭: CameraSide(clearance) 쪽은 계단식 하강 지형 + 침강 중경/원경
  (발판 가림 금지), 반대쪽은 럽블 능선 + 상부층 + 솟는 스카이라인.
  BuildGameObjects = 사전 배치(손 편집) 전용 GO 백엔드 - 바닥은 만들지 않는다.
  SightClearance 구조체 = 카메라 시선 밴드 겹침 판정 (여기 정의).
- EnvironmentRenderer.cs: Graphics.RenderMeshInstanced 드로우 (ADR-0005, 웹 호환).
  존 청크 등록/해제(AddChunk/RemoveChunk), 콜당 1023 분할, 청크 바운드 컬링,
  Wave>0 블록만 동적 배치로 사인파 갱신. static Active.
  **배치 키는 (메시, 팔레트)** - 트림시트 블록 세트가 배선되면 크기별로 구운 메시를
  쓰기 때문이다 (셀 2m 기준 청크당 16~21 배치. 측정값이며 팔레트 8색만 쓰던
  이전과 큰 차이가 없다). 세트가 없거나 인스턴싱이 꺼진 머티리얼이면
  경고 1회 후 큐브 폴백 - RenderMeshInstanced는 인스턴싱이 꺼져 있으면 예외를 던지고
  배경이 통째로 사라진다 (실제 발생)
- EnvironmentBlockSet.cs: 배경 블록 렌더 자산 묶음 (런타임 SO, 사용자 지적 2026-07-26:
  인스턴싱 배경만 트림시트를 안 쓰고 있었다). 셀 크기(cellSpan, 기본 2m) /
  축당 셀 상한 / 크기별 구운 메시 / 팔레트 머티리얼.
  ResolveEntry = 크기를 셀로 스냅해 항목 조회 (없으면 가장 가까운 크기).
  AlignPosition = 스냅으로 커진 만큼 위치 보정 - **윗면과 복도쪽 면을 유지**한다
  (위로 자라면 카메라측 지형이 발판을 가리고, 안쪽으로 자라면 통로를 침범한다).
  굽기: 메뉴 Scavenger > Field Trim Sheet > Bake Background Blocks
  (Editor/EnvironmentBlockSetBaker - 실제 생성기를 표본 삼아 나오는 크기만 굽는다).
  cellSpan이 곧 룩/비용 손잡이다: 작을수록 원형에 가깝고 배치 수가 늘어난다
- EnvironmentAuthoring.cs: 사전 배치 배경 마커. 커버 z 범위 표시용.
  생성 입구 2개 (둘 다 Editor/BackgroundBlockBuilder를 호출 - 로직은 그쪽에만 있다):
  - FieldSpawner 인스펙터의 "실제 객체로 생성" 버튼 (buildBackgroundBlocks 플래그 옆)
  - 에디터 윈도우 Scavenger > Environment Authoring
  주의: FieldSpawner.BuildBackground는 이 마커의 커버 범위를 확인하지 않는다.
  사전 배치를 둔 채 플레이하면 인스턴싱 배경이 위에 겹쳐 그려진다 -
  사전 배치만 쓰려면 buildBackgroundBlocks를 꺼야 한다 (양쪽 UI가 경고로 안내).
- DepthLighting.cs: 깊이별 조도 (M4-2). RunStarted/DepthChanged 구독,
  씬 베이스 라이팅 캡처 후 배율만 적용. minAmbientFactor/minLightFactor = 시인성 가드
- ExtractionWaypoint.cs: 탈출 웨이포인트 (ADR-0008). 존과 존 사이 구간에 1개.
  밟으면 1회 자동 탈출(정산) - 선택 UI 없음.
  진행(Advance)은 웨이포인트가 아니다 (사용자 지시 2026-07-25): 밟지 않고 걸어서
  구간을 통과하는 것이 곧 다음 스테이지이며, 감지와 깊이 증가는 Field/FieldSpawner가
  담당한다. 배치와 시각은 Field/FieldSpawner.Drops.cs의 BuildJunctionWaypoint가 소유

## 규칙

- 난수는 주입된 rng(RunManager.Rng)만 사용 (시드 재현성)
- 배경은 비주얼 전용 - 콜라이더 없음. 좌우 이동 제한은 PlayerMotor 사전 클램프
- 카메라측 가림 금지: 시야 밴드와 겹치는 블록은 생성 자체를 거부 (SightClearance)

## 스케일링 레퍼런스

- 배경 복셀 밀도를 수천 큐브로 올릴 때:
  https://github.com/Unity-Technologies/brg-shooter (BRG + Burst/Jobs).
  SegmentEnvironment를 데이터 생성기로 유지하고 렌더만 교체하는 경로
