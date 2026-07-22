# 작업 워크로그 (세션 인계용)

다른 PC/세션에서 이어받기 위한 진행 기록. 최신 항목이 위.
시작 가이드는 scavenger-landing.md 참조.

---

## 2026-07-22 (8) - 적/기둥/체크포인트/체력·스테미나/가방 HUD/길폭 1.5배

사용자 지시 배치 2탄 (플레이테스트 병행 - 씬 재구성은 플레이 종료 대기).

### 완료

- 조각 비주얼 수정 (사용자 지적: 메쉬 깨짐): 루트 회전 -> 비주얼 자식만
  자기 중심 회전 + 비행 중 tier색 TrailRenderer (착지 시 페이드)
- 체력(PlayerHealth): 3히트, 피격 무적 0.8s. 폭탄/낙하물/기둥 = 2,
  돌진 적 = 1, 낙사 = 즉사 유지. GameFlow 폴백 부착 + 런 시작 리셋
- 스테미나(PlayerStamina): 과적/초과적 이동 중 소모, 그 외 회복.
  0 = 탈진 -> Motor.StaminaScale 감속 (SpeedScale과 곱 합성),
  recoverThreshold(30) 회복 시 해제. 스프린트 도입 시 소모 소스 추가 지점
- FloorBreaker 추출: 분할 침몰 + 안전 레인 공용 유틸 (3종째 규칙).
  RockfallZone 리팩토링 + ToppleColumn 공유
- 돌진 적(ChargingEnemy): 접근 -> 레인 예고(셀 점멸) -> 앞(포그 너머 16m)에서
  뒤로 질주. 접촉 = 피해 1 + 밀려남. DepthCurve.EvaluateChargerCount
- 기둥 붕괴(ToppleColumn): 가장자리 기둥이 예고(흔들림+발자국 점멸) 후
  복도를 가로질러 쓰러짐. 깔림 = 피해 2 + 발자국 바닥 분할 침몰.
  카메라 반대편 고정 배치 (세워진 기둥의 발판 가림 방지)
- 체크포인트 안전지대(CheckpointZone, 사용자 지시): 구간 끝 9m + 좌우 확장
  슬랩 (복도보다 넓음 - 이동 클램프 확장). 머무는 동안 위협 발동 보류
  (static PlayerInside) + 붕괴 전선 정지 (CollapseFront.RequestHold).
  앞으로 벗어나면 플랫폼 통째 분해 낙하 + 붕괴 재개. 모든 위협 z 배치에서
  체크포인트 범위 제외
- 길 폭 1.5배 (사용자 지시): corridorHalfWidth 3.5 -> 5.25 (코드 기본값 단일 소스)
- HUD 확장 (사용자 지시): 가방 패널 (가치/무게·적재 단계/보유 아이템 목록),
  체력 바 + 스테미나 바 (좌상단, 탈진 표기). HudCanvas 프리팹 재생성

### Codex 교차 검토 2차 (gpt-5.5 xhigh, P1 6 / P2 6 / P3 5)

반영 12건:
- P1: 기존 위협(폭탄 무장/땅꺼짐 발동/밀기 발동)에 체크포인트 안전지대 게이트
  누락 -> CheckpointZone.PlayerInside 확인 추가 (신규 발동만 보류, 진행 중은 유지)
- P1: FloorBreaker가 좁은 조각(기존 안전 레인)을 통째 침몰 -> 전폭 봉쇄.
  분할 불가 조각은 파괴하지 않는 것으로 변경 (마지막 우회로 보존)
- P1: 위협 존(낙하물/돌진/기둥) 타입 교차 z 간격 없음 -> 공용 예약 리스트
  (CrossHazardMinZGap 7m). 폭탄/트랩까지 포함한 완전 통합 예약은 백로그
- P1: 확장 클램프로 선택 노드 옆 우회 가능 -> ChoiceNode 트리거 폭을
  체크포인트 전체 폭으로 확장
- P2: 체크포인트 내부 판정 z 단독 -> x 범위 + 접지 높이 병행 (옆 낙하 오탐 방지)
- P2: RequestHold 실행 순서 1프레임 누수 -> CollapseFront가 PlayerInside 직접
  조회 (요청 프로토콜 제거)
- P2: FloorBreaker RaycastAll 순서 의존 -> 최근접 히트 선택
- P3: 스테미나 OnValidate(회복 임계 클램프)/OnDisable(감속 복구),
  HUD 폭 음수 방어, 가방 목록 6종 + 외 N종 요약, 트레일 머티리얼
  셰이더 폴백 + 투명 서페이스 설정 (끝 페이드 동작)

기각 3건:
- 확장 슬랩 0.5m 틈: 본선 바닥 폭이 halfWidth*2+1이라 실제로는 플러시 접합 (오독)
- scatterSeed 0 폴백 비결정성: 정상 경로에서 도달 불가 (수동 배치 전용 폴백)
- HudCanvas 프리팹 마이그레이션: 프리팹이 git으로 전파되므로 불필요

### 미결/주의

- 위협 통합 배치 예약(폭탄/트랩 포함 z + 안전 레인 방향)은 백로그 -
  현재는 발동형 존 3종만 교차 간격 보장

### 다음 작업

M2-3~6 잔여 (퀵슬롯/탈출 카드). 신규 위협/체크포인트 수치 플레이 튜닝.

---

## 2026-07-22 (7) - 후퇴 한계 / 배경 비대칭 / 파밍 드롭 / M5-1 uGUI / 낙하물

사용자 지시 5건 + 세션 중 추가 1건(낙하물).

### 완료

- 후퇴 한계 확장: BackLimitZ = 카메라 뷰포트 하단 에지(backEdgeViewportY 0.03)의
  지면 교점 - backLimitSlack(0.5). 화면에 아슬아슬하게 걸릴 때까지 후퇴 가능,
  카메라 튜닝 자동 추종. Camera 부재 시 구 마진 방식 폴백
- 배경 좌우 비대칭 (발판 가림 금지): 카메라측(+x)은 계단식 하강 지형
  (3열, 상판 항상 y<0) + 상부 플랫폼 제외 + 중경/원경 침강 배치.
  시야측(-x)은 럽블 능선/상부층/솟는 중경·원경 유지 + 밀도 강화.
  SegmentEnvironment.CameraSide(clearance)가 부호 단일 소스
- 파밍 연출 (사용자 지시): 루팅 중 0.28s 주기 큐브 파편(LootBurst),
  완료 시 조각 3개가 포물선 낙하(LootPickup, 1회 바운스) -> E 홀드로 줍기
  (상호작용 키 공용). 가치/무게는 조각에 분배 - 합계 보존. 획득 시점 =
  줍는 순간 (방치 = 두고 간 가치). 조각은 지지 스트립 자식 - 함께 침몰.
  복도 밖 착지 클램프(scatterClampHalfWidth), 단차 위는 좁은 산포
- M5-1 uGUI 전환: IMGUI HudOverlay 삭제 -> HudCanvas 프리팹
  (HudController + LootLabelLayer + VirtualJoystick uGUI 재작성).
  아이템 머리 위 라벨 (이름/+가치/무게/한 줄 설명 - LootDefinition.shortDescription).
  상호작용 프롬프트 박스 = 모바일 홀드 버튼 겸용 (SetExternalInteractHeld).
  EventSystem 미사용 (포인터 직접 판독). 폰트 = LegacyRuntime (한글 OS 폴백, TMP 기각).
  asmdef에 UnityEngine.UI 참조 추가. 기존 GameFlow 프리팹의 구 컴포넌트는
  Ensure Prefabs가 자동 제거
- 낙하물 위협 (세션 중 추가 지시): RockfallZone - 접근 시 착탄점 예고
  (DangerGrid 셀 점멸 + 근접 트레머 0.95s) 후 돌 낙하. 직격 = 사망,
  착탄 지점 FloorStrip.Sink = 발판 파괴(낙사 구멍). 착탄점은 플레이어 전방
  0.9~2.4m + 좌우 산포 (즉사 저격 금지). DepthCurve.EvaluateRockfallCount
  (기본 d1=1, +0.5/깊이, 최대 3). 초입/선택지 앞/단차 z 제외, 존 간격 9m
- 검증: dotnet 3 프로젝트 0 에러, EditMode 43/43, Ensure Prefabs +
  Setup Greybox Scene 재실행 (HudCanvas 포함 씬 커밋)

### 규약 메모

- 순수 비주얼 난수는 new System.Random(GetInstanceID()) - SinkDebris 선례.
  단, 게임 결과에 닿는 산포(조각 착지/착탄점)는 배치 시 RunManager.Rng에서
  시드를 배정받아 로컬 스트림 사용 (아래 교차 검토 반영).
  RunManager.Rng를 런타임 이펙트가 직접 소비하면 맵 재현성이 깨지므로 금지

### Codex 교차 검토 (gpt-5.5 xhigh, P1 4 / P2 5 / P3 3)

반영 10건:
- P1: 조각 산포/착탄점 시드를 GetInstanceID -> 배치 시 RunManager.Rng 배정
  (게임 결과 난수는 재현성 규약 대상). AddComponent 직후 Awake가 시드 주입보다
  먼저 돌므로 rng는 지연 초기화
- P1: 낙하물 착탄이 전폭 스트립을 통째로 Sink -> 점프 없는 플레이어에게
  우회 불가 봉쇄. 땅 꺼짐과 동일하게 분할 침몰 + 안전 레인 보존
  (부착물은 소속 조각으로 재부착 - 리스케일 왜곡 방지)
- P1: 선택지가 키보드 전용 -> 터치 소프트락. ChoiceNode.ChooseAdvance/Extract
  공개 + HUD 선택 패널에 전진/탈출 터치 버튼 (프리팹 재생성)
- P2: 조이스틱/홀드 버튼이 primaryTouch만 읽음 -> 전체 터치 순회 + 조이스틱은
  시작 touchId 고정 추적 (멀티터치)
- P2: 후퇴 한계를 하단 중앙 한 점으로만 계산 -> 플레이어 뷰포트 x에서 샘플
- P2: 조각마다 런타임 LootDefinition SO 생성 (같은 id 다른 값 = 불변식 파괴)
  -> 정의는 공유 참조, RunInventory.Add(definition, value, weight) 오버로드로
  지분만 반영. LootPickup.PieceValue/PieceWeight
- P2: 돌/조각 인스턴스 머티리얼 미해제 -> OnDestroy에서 Destroy
- P3: HudController 실행 순서 -150 (홀드 1프레임 지연 제거),
  LoadPrefabContents try/finally, (RaycastAll은 자체 발견 선반영)

기각/보류 2건:
- 반경 내 조각 일괄 수거 + Looting 중 줍기 허용: 의도된 UX (더미 줍기 편의)
- LootBurst CreatePrimitive churn: 그레이박스 허용, 정식 VFX 교체 시 풀링

### 다음 작업

M2-2 획득 피드 (조각 줍기 피드로 대체 가능성 검토) -> M2-3~6.
uGUI 배치/폰트 크기는 플레이테스트 후 프리팹에서 튜닝.

---

## 2026-07-22 (6) - 뷰좌표 입력 / 벨트스크롤 / 붕괴 연출 / 프롬프트 / 교차 검토

사용자 피드백 연속 반영 세션 (플레이테스트 병행).

### 완료

- 입력 축 = 뷰 좌표계 (화면 위 = 카메라 전방 지면 투영). ADR-0007 개정
- 벨트스크롤 규칙: 후퇴 이동은 허용, 카메라는 전진 전용 래칫 + 플레이어는
  카메라 가시 영역 뒤(BackLimitZ = 래칫 - backLimitMargin) 이탈 불가.
  붕괴 클램프와 max 합성 (Motor.CameraMinZ)
- 땅 꺼짐 우회로: SinkTrap이 스트립을 분할해 침몰편에만 트랩, 반대쪽 안전
  레인(sinkTrapSafeLaneWidth) 보존 - 전폭 함몰 봉쇄 제거.
  낙하물(위에서 떨어지는 위협) 안은 백로그
- 붕괴 그리드 연출: FloorStrip.Sink 시 표면을 1m 셀 조각으로 분해, 조각별
  지연/틸트 낙하 (SinkDebris, 판정은 일괄 유지)
- 상호작용 키 프롬프트: 루팅 가능 시 우하단 "E 꾹" 안내 (LootSpot.PromptTarget)
- Codex 교차 검토 (M3/M4 배치, xhigh): 9건 중 8건 반영 -
  조이스틱 실행 순서(-200, 1프레임 지연), DangerGrid 보수적 셀 포함(경계
  거짓 안전 제거), 푸시 트랩 preplaced 제외/침몰 무장해제/머티리얼 누수(+Bomb),
  DepthLighting 구독 경합/Skybox 앰비언트 모드(ambientIntensity 병행)/비활성 복원.
  기각 1건: BuildWalkFloorStrips 공개 API 제거 - 외부 소비자 없음
- EditMode 테스트 43/43 통과 (플레이 종료 후 실기 확인)

### 다음 작업

M5-1 uGUI 전환. 모바일용 상호작용 버튼(E 대체)도 M5-1에서 uGUI로.

---

## 2026-07-22 (5) - 입력 개편(ADR-0007) + 카메라 쉐이크 + M3 + M4

사용자 지시: 토글 폐기(2D 벡터 입력), 진행 순서 M3 -> M4 -> M5(uGUI) -> M2,
땅꺼짐 근접/폭발 피격 카메라 쉐이크.

### 완료

- ADR-0007 입력 개편: 2D 벡터 홀드 이동 (WASD 홀드 8방향 + 가상 조이스틱
  아날로그 합산, 크기 1 클램프). VirtualDPad 삭제 -> VirtualJoystick (IMGUI,
  데드존, uGUI 전환은 M5-1). 루팅은 좌우 입력 유지 중 시작 불가 (즉시 취소 방지)
- CameraShake (Main Camera 프리팹): 트라우마 임펄스 (폭탄 근접 비례 / 사망 0.8)
  + 지속 트레머 (붕괴 전선 근접 내장, RequestTremor로 외부 위협 합류)
- M3-1 DangerGrid: 위험 범위를 월드 정렬 바닥 셀 점멸로 표시 (풀링, 핸들 API).
  폭탄 기폭 시작 시 폭발 반경 표시. 셀 계산 순수 함수 + EditMode 테스트
- M3-2 SinkTrap: 코리도 스트립에 부착, z 근접 -> 예고 (셀 점멸 + 근접 비례
  트레머) -> FloorStrip.Sink. 배치 제외 규칙 (초입/선택지 앞/단차 구간)
- M3-3 PushTrap: 감지 -> 예고 점멸 -> 붕괴 쪽(-z) 밀기 (PlayerMotor.AddImpulse,
  감쇠 + 기존 클램프 그대로). 쿨다운 재장전. depth 1은 기본 미등장 (DepthCurve)
- M4-1 요소 4분리: SegmentSpawner partial 3분할 (코어 / Path=길 / Features=기능)
  + SegmentPath (길 공용, BuildWalkFloorStrips 이동). 동작 동일 리팩토링
- M4-2 DepthLighting: 깊이별 앰비언트/주광 배율 (베이스 캡처 + 배율 적용,
  하한 = 시인성 가드, 전환 보간)

### 미결/주의

- EditMode 테스트 실기 실행 못 함 - 세션 내내 에디터가 플레이 모드 (사용자
  플레이테스트 중으로 판단, 강제 종료 회피). dotnet 컴파일은 전 슬라이스 통과.
  플레이 종료 후 Test Runner 1회 실행 필요 (신규: DangerGrid 5 + DepthCurve 2)
- 플레이 모드 중 리컴파일이 수차례 발생 (csproj 재생성 필요 때문). 플레이 재시작
  후 신규 컴포넌트(조이스틱/쉐이크/트랩)가 반영된다
- 구 VirtualDPad missing script 경고는 플레이 세션 메모리 잔재 - 디스크
  씬/프리팹은 정리 완료 (재생 시 사라짐)
- M3-4 추격 적: 계획대로 보류 (붕괴와 압박 중복 - 재미 검증 후)

### 다음 작업

M5-1: uGUI 전환 (사용자 지시 확정) - 가상 조이스틱/HUD(무게, 이후 퀵슬롯/획득
피드 자리) uGUI 재구성. 이후 M2-2 획득 피드 -> M2-3~6

---

## 2026-07-22 (4) - M2-1 무게/가방 + EditMode 테스트 실기 첫 실행

### 완료

- M2-1 무게/가방:
  - LootDefinition.weight (폴백 카탈로그: 폐지 1 / 고철 4 / 금고 9)
  - RunInventory.TotalWeight 합산
  - CarryLoad (Player/ 신규): 3단계 판정 (일반 < 8 <= 과적 < 16 <= 초과적,
    임계·배율 프리팹 튜닝) -> PlayerMotor.SpeedScale (과적 0.7 / 초과적 0.45)
  - HUD: 가치 합계 박스에 무게/단계 줄 추가, D-패드 겹침 회피로 x 204로 이동
    (배치 정리는 M5-1)
  - 프리팹: 템플릿에 CarryLoad 포함 + GameFlow 런타임 폴백 + 기존 Player.prefab에
    MCP로 제자리 추가 완료
- EditMode 테스트 실기 첫 실행 (Unity MCP): RunLifecycleTests 6건이 전부
  NullReferenceException - EditMode는 AddComponent 시 Awake를 호출하지 않아
  StateMachine/Timer 미배선. EditModeLifecycle(리플렉션 Awake/OnDestroy 헬퍼)로
  수리. 현재 35/35 통과 (신규: 무게 합산 1 + CarryLoad 5)
- dotnet 컴파일 검증 통과 (Scavenger.Game / EditModeTests)

### Codex 교차 검토 (한도 해소 확인 - 이번 세션에 수행)

M2-1 검토 (gpt-5.6-sol high): 3건 -> 2건 반영 (임계/배율 OnValidate 가드,
런 재시작 시 CarryLoad.RefreshNow), HUD 반응형 배치는 M5-1 보류.

M1 배치 검토 (gpt-5.6-sol xhigh, 트리아지 기각 재확인 포함): 6건 확인, 전부 반영.
- P1: 사전 배치 바닥이 붕괴로 파괴된 뒤 복구 없음 -> 재시작 낙사 루프.
  FloorStrip.preserveOnSink + Restore, RegisterPreplacedStrips가 복구
- P2: 루팅 진행 중 재검증 없음 (단차 침몰 후 아래에서 획득 가능)
  -> IsLootingStillValid에 범위/최소 y 재검증
- P2: 시드에 따라 단차 0개 (전 시도 카메라쪽+클리어런스 거부)
  -> 거부 시 반대편 미러 재시도 (rng 추가 소비 없음)
- P2: 클리어런스 반대편 스킵이 중심 기준이라 x=0을 가로지르는 넓은 블록 통과
  -> 블록 전체가 반대편일 때만 스킵
- P2: 침몰한 바닥 위 루트/폭탄이 공중에 남아 인접 스트립에서 상호작용 가능
  (기존 트리아지에서 코스메틱으로 기각했던 항목 - 기각이 틀렸음)
  -> 지지 스트립의 자식으로 부착, Sink가 함께 처리. 사전 배치 커버 구간은
  제외 (복구형 스트립에 부착하면 획득한 루트가 부활) - 문서화된 한계
- P2: 커밋된 씬이 구식 (포그 18/65, 프리팹 미참조). 씬 + 프리팹 4종 + 그레이박스
  머티리얼 + TiltShiftProfile 일괄 커밋으로 해소. 나머지 기각 5건은 문제 없음 판정

### 메모

- 신규 .cs 추가 직후 dotnet 빌드는 실패한다 - Unity가 csproj를 재생성해야 함.
  Unity MCP refresh_unity(force) 후 재시도하면 해결
- .mcp.json 로컬 변경(HTTP 브릿지)은 머신 전용이라 커밋하지 않음
- 푸시 미해결: 이 환경은 비대화형이라 git 인증 프롬프트 불가. 사용자 터미널에서
  git push 1회 필요

### 다음 작업

즉시: M2-2 획득 피드 (최근 획득 HUD 피드, 파밍 가능 오브젝트 근접 하이라이트)
이후: M3-1 위험 그리드 -> M4-1 요소 4분리 -> M2-3~6 ...
보류 결정 대기: 스테미나 (이제 무게 체감 가능 - 플레이 확인 후 판단),
세계관, 이동 토글 해석 확정

---

## 2026-07-22 (3) - 마젠타 수정 / 가상 D-패드 / 랜딩 문서

- 마젠타 원인: 에디터에서 new Material(...)을 프리팹/씬에 저장 - 디스크 에셋이
  아니라 리로드 후 참조 깨짐. 수정:
  - GreyboxMaterials (에디터): Assets/Materials/Greybox/에 머티리얼 에셋 생성/재사용
  - 플레이어 템플릿/사전 배치 배경(에디트 모드)이 에셋 머티리얼 사용
  - Scavenger > Repair Greybox Materials: 기존 Player 프리팹 제자리 수리
    (다른 튜닝 값 보존). 사전 배치 배경은 윈도우에서 재생성
- 가상 D-패드 (VirtualDPad, 좌하단 십자): 클릭 = 4방향 토글 (키보드와 동일 규칙),
  활성 방향 하이라이트. GameFlow 프리팹 템플릿 포함 + 기존 프리팹은 런타임 폴백
- 랜딩 문서 scavenger-landing.md 작성 (세션 시작 체크리스트/다음 작업/결정 대기)
- 푸시 여전히 대기 (GitHub 인증). 로컬 커밋 5개

---

## 2026-07-22 (2) - M1 구현 + 리뷰 트리아지

### 완료

- M1-1 수직 요소: 단차(상판+21도 경사로, tier 2-3 보상 루트, 붕괴 연계) - SegmentSpawner.PopulateLedges
- M1-2 전방 시야 차단: 리니어 포그 10-42m (정보 차단용)
- 멀티렌즈 리뷰(3렌즈, 지적 21건) 후 직접 트리아지 - 아래 표 참조

### 리뷰 트리아지 결과

주의: 워크플로우 검증 단계가 조직 사용 한도 초과로 전멸해 반박 검증은 무효.
21건을 직접 판정했다.

수정함 (P1급):
1. 루팅/선택지 중 중력·붕괴클램프 정지 (붕괴 면역) -> 상태와 무관하게 motor.Tick 유지
2. MinZ 클램프가 정지 플레이어를 전선 속도로 밀어줌 (컨베이어 - 정지 압박 불성립)
   -> backwardLimit = min(MinZ, 현재z), 절대 앞으로 밀지 않음
3. 루팅 중 트리거 이탈/스팟 침몰 시 EndLoot 미호출 (Looting 소프트락)
   -> lootingPlayer 별도 추적, Release가 항상 EndLoot
4. 사전 배치(EnvironmentAuthoring) 구간 FloorStrip 미등록 (붕괴 무효)
   -> RegisterPreplacedStrips + CollapseFront 중복 등록 가드
5. 단차가 단일 FloorStrip(최대 10m)이라 붕괴 면역 구간 발생
   -> SinkThresholdZ = 시작 + min(깊이, 2m)
6. 시야 클리어런스가 카메라 반대편(-x) 낮은 지형까지 거부 (단차가 +x에만 스폰)
   -> 반대편 블록은 검사 제외 (NearPoint.x * pos.x < 0)
7. 단차 옆 바닥에서 상판 루트 도둑질 가능 (트리거 반경 겹침)
   -> LootSpot.requiredMinPlayerY (상판 높이 - 0.3)
8. 폭탄이 단차 z구간 옆 통로에 스폰 (통로 폭 < 폭발 지름 = 사실상 봉쇄)
   -> 단차 z구간 전체에서 폭탄 제외 (IsInLedgeZRange)
9. 잔존 탈출 잠금 데드코드 (ChoiceNode.IsExtractionLocked + HUD 분기) 제거
10. CollapseFront를 스포너 프리팹 템플릿에 추가 (런타임 폴백은 유지)
11. 단차 클리어런스 검사에 상판 루트 비주얼 포함

기각/보류 (문서화만):
- 코리도 루트/폭탄이 스트립 침몰에 동반 안 됨 - 전선 뒤라 도달 불가, 코스메틱 (P3 보류)
- PopulateLedges의 rng 소비 가변 - 같은 시드+같은 카메라 설정이면 결정적. 카메라
  튜닝 간 재현성은 스코프 밖으로 정의
- 사전 배치 유무에 따른 rng 스트림 분기 - 씬 구성이 다르면 다른 맵 (의도된 동작)
- BuildShell의 new Random(0) 폴백 - RunManager 부재 시(에디터 프리뷰)만, 의도됨
- 포그 42m vs 룩어헤드 확정 노출 - 기획 확정(전방 포그 가림)이 ADR-0004 연출 근거에
  우선. 룩어헤드는 포그 경계 팝인 방지 역할로 유지
- 클리어런스 카메라 지점이 positionOffsetLocal 미반영 - 근사 허용

### 푸시 대기 (이 PC GitHub 인증 필요)

로컬 커밋 (origin 대비 +3, 이번 트리아지 커밋 추가 예정):
- 8b92582 feat: replace click-step with four-way toggle movement and fall death
- 6d9076c feat: floor collapse pressure, tilt-shift view and background impulses
- 4277a18 docs: split remaining work into implementation plan v0.0.3

해결법: 아무 터미널에서 git push 1회 실행 -> Git Credential Manager 브라우저 로그인.

### 환경 메모

- 조직 월간 사용 한도 도달 - 서브에이전트 워크플로우/Codex 대량 호출 불가.
  교차 검증은 한도 리셋 또는 승급 후 재개 (/usage-credits)
- Unity MCP는 .mcp.json에 등록됨 (UnityMCP). 세션 시작 시 신뢰 승인 필요.
  Unity 에디터에서 Window > MCP For Unity 브릿지 Running 확인
- EditMode 테스트 (Test Runner) 실행은 아직 미확인 - Unity에서 1회 돌려볼 것

### 다음 작업 (구현 계획 v0.0.3)

즉시: M2-1 무게/가방 (LootDefinition.weight, 3단계 이속 배율, HUD 표시)
이후: M2-2 획득 피드 -> M3-1 위험 그리드 표시 -> M4-1 요소 4분리 -> M2-3~6 ...
보류 결정 대기: 스테미나 (무게 체감 후), 세계관, 이동 토글 해석 확정

---

## 2026-07-22 (1) - 기획 v0.0.2 반영 대개편

- 기획 메모 -> scavenger-extraction_v0.0.2_plan.md 정리 (확정 5 / 검토 6)
- ADR-0006: 4방향 토글 이동(가정: 1회=연속이동, 같은키=정지), 점프 제외, 낙사 도입,
  시간 압박 = 바닥 붕괴 (숨김 타이머 잠금 제거), 틸트 시프트 뷰
- 구현: 이동 재작성, FloorStrip/CollapseFront, RunTimer 스톱워치화, DoF 볼륨,
  brg-shooter 셀 바운스 임펄스 (폭발/붕괴 반응), 워크 밥
- 구현 계획 v0.0.3: M1 지형 / M2 파밍 / M3 위험 / M4 맵 4분리 / M5 UI 로 분할

## 2026-07-21 - v0.1 그레이박스 (요약)

- 기획 v0.0.1 -> 구현계획 v0.0.2 (Codex 교차검증 반영) -> S1~S7 수직 슬라이스 구현
- ADR-0001(루팅 정지/거리 신호) ADR-0002(클릭 스텝, 폐기됨) ADR-0003(심리스/카메라)
  ADR-0004(복층/사전배치/룩어헤드) ADR-0005(RenderMeshInstanced 배경)
- Codex 교차 검토 1회 반영 (P1 4건 등 9건)
- 프리팹 셋업 체계 (Assets/Prefabs, Main Camera.prefab 경로 고정)
