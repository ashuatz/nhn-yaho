# Segment/ - 구간 생성과 진행

네임스페이스: Scavenger.Segment

## 파일 목차

- SegmentDefinition.cs: ScriptableObject (길이, 복도 반폭, 벽 높이).
  라운드 템포(2-3분)는 길이 x 전진 속도로 결정 - 템포 튜닝은 여기서
- SegmentSpawner.cs: 생성/제거 단일 경계 (풀링 교체 대비). 요소 4분리 (M4-1)로
  partial 3파일: 본 파일 = 코어 조율 (BuildSegment/BuildInitialChain/DespawnAll/
  DespawnBehind, 공유 유틸, 시야 클리어런스)
- SegmentSpawner.Path.cs: 길 카테고리. BuildShell(바닥/배경 위임), 사전 배치 복구
  (RegisterPreplacedStrips), 단차(PopulateLedges/BuildLedge), 발밑 부착
  (AttachToSupportingStrip), currentStrips/currentLedges
- SegmentSpawner.Features.cs: 기능 카테고리. 루트(tier 가중치)/폭탄(z 간격 검증)/
  땅꺼짐·밀기 트랩/낙하물 존(RockfallZone, 초입·선택지 앞·단차 제외, z 간격 9m)/
  바닥 장판(PopulateHazardFloors, 한쪽만 깔아 우회 여지)/폭격 구역(PopulateStrikeZones,
  z 간격 12m)/굴림블록 스포너(PopulateRollingBlocks, z 간격 16m) - 뒤 3종 ADR-0008
  웹 이식/ChoiceNode/거리 신호 배치 + 트랩 튜닝 필드. 루트 조각 산포/낙하물 착탄/
  폭격 셀/굴림 스폰 시드는 배치 시 RunManager.Rng에서 배정 (게임 결과 난수 = 재현성 대상).
  전진 콜백에서 다음 구간 생성과 뒤쪽 정리 수행 (동시 생존 최대 2구간)
- SegmentPath.cs: 길 공용 지오메트리 (정적). BuildWalkFloorStrips - 런타임과
  사전 배치 윈도우 공용 (SegmentEnvironment에서 이동)
- DepthCurve.cs: 깊이 스케일링 단일 소스. tier 가중치 / 폭탄 수 / 기폭 시간 / 폭발 반경 /
  땅꺼짐·밀기 트랩 수 / 낙하물 존 수 (EvaluateRockfallCount) / 바닥 장판 수
  (EvaluateHazardFloorCount) / 폭격 구역 수 (EvaluateStrikeZoneCount) / 굴림블록
  스포너 수 (EvaluateRollingBlockCount) - 뒤 3종은 ADR-0008 웹 이식.
  모든 값 클램프 - 통과 불가 배치 방지 (blastMaxRadius x 2 < 복도 폭 유지 필수)
- ChoiceNode.cs: 구간 끝 선택지. W = 전진, E = 탈출. static Active = HUD 프롬프트 참조.
  (탈출 잠금 규칙은 ADR-0006에서 제거 - IsExtractionLocked는 기본 false)
- FloorStrip.cs: 붕괴 단위 바닥 스트립 (2m). Sink = 자식 포함 콜라이더 off + 가라앉음 -> 낙사.
  표면 렌더러가 있으면 SinkDebris로 그리드 셀 조각 낙하 연출 (판정은 일괄 유지).
- SinkDebris.cs: 붕괴 연출 전용 (순수 비주얼). 표면을 1m 셀로 쪼개 조각별
  지연/낙하 속도/월드 틸트 회전. 상한 80조각, 공유 런타임 머티리얼, 자체 파괴.
  단차(Ledge) 같은 복합 요소 루트에도 부착 가능.
  preserveOnSink(사전 배치용) = 파괴 대신 비활성 보존, Restore = 런 재시작 복구
  (미복구 시 재시작 낙사 루프 - 교차 검토 P1). 코리도 루트/폭탄은 지지 스트립의
  자식으로 부착되어 함께 침몰 (사전 배치 커버 구간 제외 - 복구 시 루트 부활 방지)
- 단차 (M1-1, SegmentSpawner.PopulateLedges): 구간당 1-2개, 측면 상판(0.7-1.1m) +
  진입 경사로(-z, 약 21도). 상판에 tier 2-3 고가치 루트. 시야 클리어런스 적용,
  바닥 루트/폭탄은 단차 영역 제외, 붕괴 전선에 등록되어 함께 가라앉음
- DangerGrid.cs: 위험 범위 셀 표시 (M3-1, 큐비트 방식). ShowCircle/ShowRect -> 핸들,
  Hide(핸들). 셀 풀링 + 공통 점멸 (런타임 머티리얼). 폭탄 기폭 시작 시 폭발 반경 표시,
  땅 꺼짐 등 후속 위협 공용. 셀 포함은 보수적 (일부라도 겹치면 표시 - 최근접점/
  실면적 판정, 교차 검토 반영). CellsInCircle/CellsInRect = 순수 함수 (EditMode
  테스트). cellSize/색/점멸 = 프리팹 튜닝 지점. static Instance
- SinkTrap.cs: 땅 꺼짐 트랩 (M3-2). FloorStrip과 동일 오브젝트. 플레이어 z 근접 시
  예고 (DangerGrid 사각 점멸 + 근접 비례 CameraShake 트레머) 후 FloorStrip.Sink.
  붕괴 전선이 먼저 침몰시키면 무효. 수치는 SegmentSpawner 필드가 주입.
  배치는 PopulateSinkTraps (초입 12m/선택지 앞 8m/단차 z구간/기부착 스트립 제외,
  DepthCurve 수량). 배치 시 SplitStripForSinkTrap이 스트립을 분할해 침몰편에만
  트랩을 달고 반대쪽에 안전 레인(우회로, sinkTrapSafeLaneWidth) 보존 - 전폭
  함몰 봉쇄 금지 (사용자 지시)
- CollapseFront.cs: 시간 압박의 단일 소스 (ADR-0006). 붕괴 전선이 뒤에서 전진하며
  지나간 스트립을 가라앉히고 플레이어 z를 전선 앞으로 클램프 (후퇴 불가 겸용).
  startDelay/baseSpeed/speedPerDepth/maxSpeed = 프리팹 튜닝 지점. static Instance
- SegmentEnvironment.cs: 공간감 PCG 데이터 생성기 (ADR-0003/0004/0005).
  GenerateBlocks = 인스턴스 블록(행렬+팔레트 8색+웨이브/위상) 생성.
  좌우 비대칭 (사용자 지시: 카메라측 지형이 발판을 가리면 안 됨):
  CameraSide(clearance) = 카메라 쪽 부호 (+x 기본). 카메라측 = 계단식 하강 지형
  (AddTerracedDescent, 상판 항상 y<0, 3열이 복도에서 멀수록 낮아짐) + 상부
  플랫폼 제외 + 중경/원경도 침강 배치 (상판 y<0, 저지대 잔해 인상).
  시야 반대측 = 기존 럽블 능선 + 상부층 + 위로 솟는 중경/원경 스카이라인.
  레이어: 럽블(wave 0.35) + 상부층(정적) + 데브리(정적) + 중경(팔레트 6, wave 0.6)
  + 원경(팔레트 7, wave 1.0). FarPaletteStart(6)부터 렌더러가 그림자 생략.
  브릿지는 카메라 반대측에서 복도 중앙까지만 (CameraSide 미러).
  난수는 주입된 rng만 사용. SegmentDefinition.wallHeight는 현재 미사용(구 셸 잔재)
- EnvironmentRenderer.cs: Graphics.RenderMeshInstanced 드로우 (ADR-0005, 웹 호환 -
  BRG는 WebGL2 미지원이라 기각). 구간 청크 등록/해제(AddChunk/RemoveChunk),
  팔레트별 머티리얼, 콜당 1023 인스턴스 분할, 청크 바운드 컬링.
  셀 출렁임 (brg-shooter 차용): Wave>0 블록만 동적 그룹으로 분리해 매 프레임
  사인파 y 오프셋으로 행렬 갱신. animate/waveSpeed/waveAmplitude = 인스펙터 튜닝 지점.
  동적 블록 수천 개 이상으로 늘리면 Job/버텍스 셰이더 전환 검토
- DepthLighting.cs: 깊이별 조도 변화 (M4-2, 라이팅 카테고리). RunStarted/DepthChanged
  구독, 씬 베이스 라이팅 캡처 후 배율만 적용 (앰비언트 3색 + 주광 강도).
  minAmbientFactor/minLightFactor = 시인성 가드 하한. depthForDarkest/전환 시간 =
  프리팹 튜닝 지점. 위험/신호 이펙트 표시는 DangerGrid로 일원화됨
- EnvironmentAuthoring.cs: 사전 배치 배경 마커. 커버 z 범위 내 런타임 배경 생성 스킵.
  생성은 에디터 윈도우 Scavenger > Environment Authoring - 이 경로만 GameObject 백엔드
  (손 편집 가능해야 하므로 인스턴싱 비대상)

## 룩어헤드 체인 (ADR-0004)

- 다음 스테이지는 항상 미리 생성되어 확정 노출된다.
  BuildInitialChain(depth, z) = 현재+다음. 전진 선택 시 depth+2를 선생성.
  동시 생존 구간 최대 3개 (직전/현재/다음). TailEndZ가 체인 끝 기준점

## 스케일링 레퍼런스

- 배경 복셀 밀도를 레퍼런스 이미지 수준(수천 큐브)으로 올릴 때:
  https://github.com/Unity-Technologies/brg-shooter (BatchRendererGroup + Burst/Jobs
  인스턴싱, 저사양 모바일 대상 Unity 공식 데모). SegmentEnvironment를 데이터 생성기로
  유지하고 렌더만 BRG로 교체하는 경로 - GameObject 큐브는 그레이박스까지만

## 예정 (구현계획 v0.0.2)

- S6: SignalEmitter (거리 신호), ChoiceNode 탈출 잠금 연결

## 규칙

- 배치 난수는 RunManager.Rng만 사용 (시드 재현성)
- 생성/제거는 SegmentSpawner 내부에만. 외부에서 Instantiate/Destroy 금지
