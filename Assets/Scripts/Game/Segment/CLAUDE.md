# Segment/ - 배경 장식과 조도

네임스페이스: Scavenger.Segment

필드 생성(존/바닥/절차 생성)은 Field/로 이전됨 (ADR-0009).
이 폴더는 걸을 수 없는 장식(배경 블록), 조도, 스테이지 끝 웨이포인트만 담당한다.

## 파일 목차

- SegmentEnvironment.cs: 배경 PCG 데이터 생성기 (ADR-0003/0004/0005).
  GenerateBlocks(ZoneDefinition, rng, clearance[, segmentLength]) = 인스턴스 블록
  (행렬 + 팔레트 8색 + 웨이브/위상) 생성. GameObject를 만들지 않는다 (웹 호환 인스턴싱).
  segmentLength를 넘기지 않으면 존 길이 - 존보다 짧은 세그먼트(존 사이 구간)는
  반드시 실제 길이를 넘겨야 한다 (안 넘기면 다음 세그먼트 배경과 겹쳐 그려진다).
  좌우 비대칭: CameraSide(clearance) 쪽은 계단식 하강 지형 + 침강 중경/원경
  (발판 가림 금지), 반대쪽은 럽블 능선 + 상부층 + 솟는 스카이라인.
  BuildGameObjects = 사전 배치(손 편집) 전용 GO 백엔드 - 바닥은 만들지 않는다.
  SightClearance 구조체 = 카메라 시선 밴드 겹침 판정 (여기 정의).
- EnvironmentRenderer.cs: Graphics.RenderMeshInstanced 드로우 (ADR-0005, 웹 호환).
  존 청크 등록/해제(AddChunk/RemoveChunk), 팔레트별 머티리얼, 콜당 1023 분할,
  청크 바운드 컬링, Wave>0 블록만 동적 그룹으로 사인파 갱신. static Active
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
