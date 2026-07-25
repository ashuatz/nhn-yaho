# 작업 워크로그 (세션 인계용)

다른 PC/세션에서 이어받기 위한 진행 기록. 최신 항목이 위.
시작 가이드는 scavenger-landing.md 참조.

---

## 2026-07-25 (6) - 바닥 겹침 수정 + HUD를 UI Toolkit으로 전환

사용자 보고: "바닥 겹침 뭐야? 높이 처리 안했어?" + "UI도 UIToolkit 기반으로 바꿔서 셋업해봐".

### 바닥 겹침 (높이 문제가 아니었다)

원인 3개를 Unity MCP로 씬을 직접 조회해 특정했다.

1. **구버전 프리팹이 회전 없이 붙어 복도를 침범** (주 원인).
   FarmingPoint_Top/_Bottom이 아직 ver01 평면 규격(둘 다 +x로 뻗음)인데,
   새 코드가 "상단은 -x 규격"이라고 타입으로 가정해 회전을 생략했다 ->
   상단 플랫폼이 복도 위로 5x5 슬래브처럼 겹쳤다.
   -> 프리팹이 방향을 선언하게 했다 (FarmingPoint.authoredSideSign, 기본 +1 = ver01).
      소켓 1개인 구버전은 세션당 1회 경고로 재생성을 안내
2. **SampleScene에 이전 세션 생성물(Zone_00~02)이 저장**되어 있었고
   루트(SegmentSpawner)가 z -26.9로 밀려 있었다. 리스트 기반 정리로는 지워지지 않는다.
   -> DespawnAll에 DestroyLeftoverSegments(자식 순회) 추가 + ValidateRootTransform으로
      루트를 원점/무회전/스케일 1로 되돌린다 (경고 후 복구)
3. 실제 플레이 씬은 Assets/Scenes/Greybox.unity였다 (SampleScene에는 RunSystems/
   GameFlow/HUD가 없어 런이 시작되지 않는다). 두 씬을 혼동하지 말 것

자체 수정 중 만든 함정도 되돌렸다: 생성물에 HideFlags.DontSaveInEditor를 붙였더니
FindObjectsByType이 그 오브젝트를 제외해 조회가 0개가 됐다 (Unity 동작).

검증 (플레이 모드 + 스크린샷): 복도 침범 렌더러 0 / 세그먼트 gap 0.000 /
상단 +1.0 · 하단 -1.0 동시 생성 / 램프 경사 -26.6도 / 계단이 눈으로 확인됨.
존당 개수 min을 2로 올렸다 - 배분 규칙상 상단+하단이 함께 나와 3층이 화면에 읽힌다.

### HUD -> UI Toolkit

- 자산: Assets/UI/Hud.uxml + Hud.uss + UnityDefaultRuntimeTheme.tss +
  HudPanelSettings.asset, 씬 배치는 Prefabs/HudDocument.prefab
- 런타임 뷰 3개: HudView(패널) / HudJoystickView(조이스틱) / HudLootLabelView(월드 라벨)
- 에디터 메뉴 Scavenger > Ensure HUD Document (UI Toolkit), Setup Greybox Scene에 포함.
  씬에서 uGUI HudCanvas를 빼고 HudDocument로 교체 (프리팹 파일은 롤백용으로 남김)
- **함정: 폰트를 USS `resource("LegacyRuntime.ttf")`로 지정하면 런타임 패널에서
  해석되지 않아 글자가 아예 안 그려진다** (박스만 보였다). 기본 테마 폰트는 한글
  글리프가 없어서, 내장 동적 폰트를 코드로 루트에 넣는다 (HudView.ApplyFont)
- 바인딩은 Update에서 지연 처리 - UIDocument가 트리를 만드는 OnEnable보다
  뷰의 실행 순서가 빨라 rootVisualElement가 null이다
- 미이식: 획득 플라이어 연출, 토스트 큐 (연출 문서 확정 후)

### 검증 상태

- Unity 컴파일 0 에러, EditMode 55건 통과 유지
- 플레이 모드 스크린샷으로 HUD 렌더 확인 (타이머/경고/수집 요약/월드 라벨/조이스틱/가방 8칸)

---

## 2026-07-25 (5) - 기준 문서 v0.0.2 개정 + 데이터 컬럼 블로커 해소

사용자 지시: "어긋남 있는 문서는 v0.0.2로 개선 / 실질 블로커 우선 작업".

### 문서 (어긋남 해소)

- 필드_규칙_및_절차 **v0.0.2** 신설: 구간(8블록) 단위 추가, 필드 구성을
  "존 N개 + 구간 1개 반복"으로, 미리 생성을 전방 패딩 2존으로, 3.3 스테이지 인계와
  탈출 지점 신설(구간 역할 / 탈출과 진행 / 런 종료 3조건), 스테이지 시트에
  구간 길이·전방 패딩 컬럼 추가
- 파밍_아이템_및_포인트_시스템 **v0.0.2** 신설: 크기 깊이 7 x 길이 9, 소켓 2개
  (입구 z- / 출구 z+), 단차 확정 수치, 진입을 통과형으로, 제거 판정 분리,
  합성 N의 관리 위치(전역 기본 + 아이템 덮어쓰기) 명시
- 구버전 2종은 히스토리 상태를 "아카이브"로 바꾸고 현행판 링크를 넣었다.
  살아 있는 문서/코드 CLAUDE.md의 참조 링크는 전부 v0.0.2로 교체
  (ADR-0009는 당시 결정 기록이라 손대지 않음)

### 코드 (실질 블로커 = 데이터 컬럼)

- LootDefinition: 정렬 순서 / 1·2회 압축 무게 / 압축 시간 / 합성 N(0=전역) /
  등장 존 컬럼 추가. CanSpawnInZone·CompressedWeight·MaxCompressStep 헬퍼
- **등급 축 통일**: tier가 곧 아이템 등급(1~4)이고 합성의 시작 등급이다.
  TierColor -> GradeColor(문서 색상 4종)로 교체, GradeName 추가.
  이전에는 tier(종류색 1~3)와 Entry.Grade(합성 레벨 0~2)가 별개 축이라
  영웅/전설에 도달할 경로가 없었다 (문서는 4등급)
- RunInventory: 합성 상한을 전설(4)로, N을 아이템 컬럼 -> 전역 기본값 순으로 해석.
  Configure(슬롯 한도, 전역 N) / HasSlotFor / UsedSlots 추가
- BagDefinition 신설 (가방 문서 7.1 컬럼): 최대 무게 / 슬롯 기본·최대 / 전역 N.
  GameFlow가 CarryLoad(무게)와 RunInventory(슬롯·N)에 배선 - 두 판정이 같은 값을 본다
- LootSpot: 획득 제한을 무게 + **슬롯** 2조건으로 (가방 5장 / 드랍 6.3)
- 아이템 획득 거리를 스포너 상수에서 PlayerController.itemCollectDistance로 이전
  (드랍 9.3 플레이어 옵션 컬럼)
- 드랍 배치에 등장 존 필터 적용 (판정 기준 = 스테이지 내 존 순번)

### 검증

- **EditMode 55건 전부 통과** (Unity MCP로 실행. 기존 39 + 신규 16).
  신규: 등급 시작/전설 도달/전설 정지/N 덮어쓰기/전역 N/슬롯 3종 + 컬럼 헬퍼 8종
- dotnet 컴파일 0 에러 (Game / Editor / EditModeTests)

### 남은 것

- 자동 획득 on/off 버튼 (HUD 기능 작업), 가방 정리 압축 루프, 아이템 버리기 조작
- 컬럼 값은 시스템 검증용 임시값 - 밸런스 값은 데이터 시트에서 확정 필요
- 깊이(스테이지)별 컬럼 분리는 여전히 미작성

---

## 2026-07-25 (4) - 무한 진행 + 구간 웨이포인트 + 파밍 포인트 3층/입구·출구

사용자 지시 4건 + 3층 구조 계획서 착수.

### 완료

- 필드를 무한 세그먼트 체인으로 재작성 (FieldSpawner). 존 N개 + 구간(8블록) 반복.
  세그먼트 리스트(StartZ/EndZ 보유)로 교체 - 길이가 다른 구간이 끼어들어
  `stageStartZ + index * length` 산술로는 위치를 되짚을 수 없다
- 웨이포인트는 Extract 1개만. 구간 중앙 화면 위쪽(-x)에 배치해 직진 동선을 비운다.
  Advance 패드 제거 - 구간을 걸어서 통과하면 깊이 +1 (스포너가 감지, 월드 재생성 없음).
  ExtractionWaypoint.Kind 제거
- 전방 패딩 2존 (zoneLookAheadCount). 존 생성 장면이 화면에 잘려 보이지 않게
- SegmentEnvironment.GenerateBlocks에 segmentLength 인자 추가 - 구간(8블록)에
  존 길이(25) 배경을 만들면 다음 세그먼트 배경과 겹친다
- 파밍 포인트 3층 구조 (계획서 8장 1~5번): 상단 +1.0 / 하단 -1.0, 계단 4단
  (riser 0.25 = Step Offset 0.3 - 여유). 시각은 단 큐브, 충돌은 램프 콜라이더 1개
- 입구(z-)/출구(z+) 분리: 게이트 각 2블록, 사이는 차단 매스로 막아 계단이 유일한 동선.
  구역 길이 9블록으로 연장, 존당 개수 상한 3 -> 2 (간격 10을 지키면 2개까지)
- 제거 기준 분리: 흔들림 = 입구 소켓 / 낙하 = 출구 소켓. 입구 기준으로 떨어뜨리면
  출구 쪽 플랫폼에 서 있는 동안 발밑이 무너진다
- PointSocketRole(Entry/Exit) 신설. 프리팹 소켓은 월드 z로 판별 (y축 180도 회전이
  z까지 뒤집으므로 역할 필드를 신뢰할 수 없다) - 그래서 상단/하단을 각 방향으로 제작
- 프리팹 템플릿 3층 개정 + Scavenger > Rebuild Farming Point Prefabs (강제 재생성).
  Ensure 계열은 있는 프리팹을 덮어쓰지 않으므로 규격 개정에는 별도 경로가 필요

### 자체 검토에서 잡은 것

- 스테이지 지표 역행: 구간 경계에서 앞뒤로 걸으면 깊이가 여러 번 올라갔다
  (후퇴가 6칸까지 허용되므로 실제 발생) -> StageIndex를 전진 전용 래칫으로
- 계단 단 상판 높이를 riser*(step+1) -> riser*(step+0.5)로. 걷는 면은 램프이므로
  (step+1)이면 발이 단 안으로 riser만큼 파묻혀 보인다

### Codex 교차 검토 (gpt-5.5 high) 반영

- 차단 매스가 양방향으로 넘어갈 수 있음 (하단은 본선 -> 상판 -> 플랫폼으로 뛰어내림,
  상단은 플랫폼 -> 본선). 하단은 카메라측이라 매스를 본선 위로 올릴 수 없으므로
  (발판 가림) 보이지 않는 차단 콜라이더(EdgeBlocker, 높이 1.8)를 시각 매스와 분리해 추가
- 필드 루트 스케일이 1이 아니면 지형 치수가 어긋남 (월드 좌표 + localScale 조합).
  FieldSpawner.OnEnable에 ValidateRootScale 경고 + 로컬 스케일 복구 추가
- 램프 수학은 +x/-x, 오름/내림 모두 정확 확인. 세그먼트 기록/프론티어/퍼지에 지적 없음
- 파밍 포인트 z 범위 안에서만 반대쪽 복도 한계가 존 반폭(3.5)으로 넓어지던 것도
  함께 수정 (모터 클램프 폭 3.15 사용) - 평소 클램프와 어긋나던 부분

### 검증 상태

- dotnet 컴파일 0 에러 (Scavenger.Game / .Editor / .EditModeTests)
- **플레이 검증 미실시**. 프리팹 재생성(위 메뉴) 후 계획서 6장 체크리스트 진행 필요

### 문서

- 파밍포인트_3층구조 계획 ver02 (9장 구현 결과), 아트_협업_스케줄 ver02
  (피벗/소켓 2개/계단 콜라이더/확정 수치), 7월_마일스톤 ver02 (구조 반영 + 5장 미비 사항)

---

## 2026-07-25 (3) - 프리팹화 + 머티리얼 통일 + 타일 노이즈 바닥

사용자 지시 3건.

### 완료

- 머티리얼 통일: GreyboxPalette 신설. 런타임 생성물이 모두
  Assets/Materials/Greybox/Common.mat 기반으로 동작한다 (FieldSpawner 직렬화 주입).
  색당 1장만 만들어 공유 - 이전에는 오브젝트마다 renderer.material로 인스턴스를 떠서
  존 하나에 수십 장이 생기고 SRP 배칭도 색마다 끊겼다.
  발광(탈출 지점 랜드마크)도 같은 캐시를 쓴다
- 배경 인스턴싱 기본 비활성 (사용자 지시: 파밍 포인트와 겹침).
  FieldSpawner.buildBackgroundBlocks = false. 켜면 즉시 복귀되는 토글
- 존/파밍 포인트 프리팹화 (아트 다듬기 대상). Scavenger > Ensure Field Prefabs:
  - Assets/Prefabs/Field/Tiles/FloorTile_A~C (1블록 타일 3종)
  - Assets/Settings/FieldTileSet.asset (타일 목록/가중치/노이즈/기울기)
  - Assets/Prefabs/Field/FloorRow.prefab (단일 메시 행 - 타일셋 대안)
  - Assets/Prefabs/Field/FarmingPoint_Top / _Bottom (소켓 원점 + 스팟 마커)
  기존 프리팹은 절대 덮어쓰지 않는다. 스포너 프리팹에 참조 자동 배선(Repair Prefabs)
- 타일 노이즈 바닥 (사용자 지시): 타일 종류는 월드 좌표 펄린 노이즈로 가중치 선택
  (같은 타일이 뭉쳐 패치가 생긴다), 타일마다 1도 미만 roll/pitch를 좌표 해시로 부여해
  격자감을 깬다 (yaw는 유지). 노이즈 오프셋은 스테이지 진입 시 런 시드에서 1회 -
  같은 시드면 같은 바닥이 재현된다
- 타일 행의 콜라이더는 행에 BoxCollider 하나로 대표 (타일 7장마다 콜라이더를 두면
  물리 비용만 늘어난다). 타일 프리팹은 콜라이더 없이 만든다

### 설계 메모

- 파밍 포인트 프리팹 규격: 소켓이 원점(0,0,0)이고 +x로 뻗는다. 반대편은 코드가
  y축 180도 회전으로 붙인다 - 음수 스케일은 콜라이더/노멀이 뒤집혀 금지
- 프리팹 크기가 규격과 다르면 FarmingPoint.authoredSizeBlocks에 선언한다.
  선언이 없으면 ZoneDefinition 값을 안전지대 범위로 쓴다 (범위와 실제 플랫폼이
  어긋나면 끝 전에 막히거나 허공을 걷는다)
- 바닥 구성 우선순위: 타일셋 -> 행 프리팹 -> 코드 큐브 폴백.
  프리팹 미배선 상태에서도 플레이가 되도록 폴백을 남겼다

### 미결

- Unity에서 Setup Greybox Scene 재실행 필요 (필드 프리팹 생성 + 참조 배선 포함)
- 타일 3종은 색만 다른 그레이박스다. 형태 배리에이션은 아트 작업

---

## 2026-07-25 (2) - 스테이지 인계 버그 수정 + 파밍 포인트 1차 (2단계)

### 버그 수정: 탈출/진행 지점에서 발밑 바닥이 사라짐

사용자 리포트: "탈출지점에 오면 텔레포트했다가 갑자기 땅으로 떨어짐"

- 원인: 다음 스테이지 진입 시 StartStage(웨이포인트 z)로 새 존 0을 웨이포인트
  자리에서 시작했다. 웨이포인트 트리거는 z 두께 2m라 플레이어는 웨이포인트보다
  최대 1.35m 뒤에서 밟게 되고, 그 지점은 새 존 0의 첫 행보다 뒤라 바닥이 없다.
  기존 존은 DespawnAll로 같은 프레임에 사라지므로 즉시 낙하.
  "텔레포트"는 존·배경이 그 자리에서 통째로 재생성되며 보이는 현상
- 수정: StartStage에 인계 가드 추가 - startZ를 플레이어 z - 3m 이하로 강제.
  호출자와 무관하게 항상 발밑 바닥이 확보된다 (런 재시작 경로도 동일하게 보호)

### 완료 (2단계 파밍 포인트 1차)

- 아트 프리팹 계약 마커: PointSocket(입구 + 제거 판정 기준, 포인트당 1개) /
  ObjectSpot(아이템 오브젝트 자리, 복수). 규격 정본은 아트 협업 스케줄 문서 3.2
- PlayerMotor.SetBoundsOverride / ClearBoundsOverride: 이동 경계 교체 API.
  복도 밖(파밍 포인트)으로 나가되 그 구역 안에서는 떨어지지 않게 x/z를 함께 클램프.
  후퇴 한계(MinZ)는 override보다 우선 - 사라진 바닥으로는 못 걸어간다
- FarmingPoint: 타입(상단/하단) + 등급 위상 3종.
  static PlayerInside / IsPlayerInSafeZone = 안전지대 판정 (3~4단계 게이트로 사용).
  진입은 소켓 z 범위에서만, 구역 위에서는 낙사 없음 (문서 2.6 (3)).
  소켓 z가 제거 기준선에 닿으면 흔들림 -> 통째 낙하 (문서 2.7)
- 배치: 존당 개수 min-max(1~3), 최소 간격 7블록,
  상단 1번째 고정 / 하단 2번째 고정 / 3번째부터 50:50 (문서 2.5).
  상단 = 카메라 반대편(화면 위), 하단 = 카메라측 - 배경과 CameraSide 판정 공유
- ZoneDefinition에 파밍 포인트 컬럼 추가 (개수 min-max / 크기 4~7 / 간격 /
  흔들림 시작 거리)
- F1 대시보드에 Safe zone 표시 (타입 / 등급)

### 검토에서 잡은 것 (자체)

- 파밍 포인트 소유권을 양보하면 앞 포인트 해제와 뒤 포인트 획득 사이에 1프레임
  클램프 공백이 생기고, 그 프레임에 복도 클램프가 플레이어를 옆으로 끌어당긴다
  -> 조건을 만족한 쪽이 즉시 가져가는 방식으로 변경
- 낙하 시 클램프를 해제하면 구역 위 플레이어가 복도로 순간이동한다
  -> 구역 위에 있으면 클램프를 유지한 채 함께 낙하 (문서 2.7 (3) 사망 가능)

### 미결

- 단차(상단 높게 / 하단 낮게)는 아트 R&D 항목이라 그레이박스는 flush로 생성
- 그레이박스 파밍 포인트가 배경 럽블과 시각적으로 겹칠 수 있다 (콜라이더 없어
  플레이 영향 없음). 아트 프리팹 도입 시 배경 생성 제외 처리를 함께
- 3단계 아이템 오브젝트: 스팟 마커까지 생성되어 있어 오브젝트만 얹으면 된다

---

## 2026-07-25 - 기획 문서 기준 재정비: 존 단위 필드 + 기믹 전면 제거 (ADR-0009)

사용자 지시: 문서(필드 규칙 및 절차 -> 파밍 아이템 및 포인트) 기준으로 진행,
현재 있는 기믹(바닥 무너짐 등)은 빼도 됨.

### 착수 전 발견 (브랜치 상태 문제)

- 머지 충돌 잔재 4개 파일이 Assets에 남아 있었다
  (SegmentSpawner.Features_BACKUP/BASE/LOCAL/REMOTE_824.cs).
  BACKUP에 충돌 마커(`<<<<<<<`)가 그대로 있어 Unity 컴파일이 깨진 상태였음. 삭제
- PlayerStamina도 머지 후 API 불일치(PlayerMotor.StaminaScale /
  CarryLoadStage.SeverelyOverloaded 부재)로 컴파일 불가 + 배선/표시 없는 고아 상태

### 완료 (0단계: 정리)

- 기믹 9종 삭제: 폭탄 / 밀기 트랩 / 땅 꺼짐 / 낙하물 / 돌진 적 / 기둥 붕괴 /
  바닥 장판 / 미사일 폭격 / 굴러오는 블록 (Obstacle/ 폴더 제거)
- 함께 삭제: DangerGrid(기믹 예고 전용), FloorBreaker, CheckpointZone,
  ChoiceNode, SignalEmitter, DepthCurve, CollapseFront,
  SegmentSpawner 3분할 + SegmentPath + SegmentDefinition + FloorStrip + SinkDebris
- PlayerStamina 삭제 (마일스톤 문서 4.2에서 "별도 문서 예정"으로 유보된 항목)
- 삭제 시스템의 테스트 2종 제거 (DangerGridTests / DepthCurveTests)

### 완료 (1단계: 필드)

- Field/ 신설 - 문서 용어(블록/존/필드)와 코드 용어 일치
  - ZoneDefinition: 블록 1x1 / 존 25 x 7 / 스테이지 존 개수 min-max(6~9) /
    바닥 제거 시작 거리 6칸 / 흔들림 시간 1.2s / 존 가치 예산 60~110 /
    드랍 생성 간격 2. lengthMeters·corridorHalfWidth는 블록 수에서 유도
  - FloorRow: 존 너비 x 1블록 = 제거 단위. 정상 -> 제거 예정(흔들림) -> 제거(낙하).
    Pending은 아직 밟히는 마지막 경고, Falling 시작에 콜라이더 off
  - Zone: 행 목록 소유, UpdateRemoval로 기준선 뒤 행 전이, AttachToRow로 요소 부착
  - FieldSpawner: 스테이지 존 개수 확정 + 존 체인 3개 유지(직전/현재/다음) +
    제거 기준선(플레이어 뒤 6칸, 전진 전용 래칫) -> PlayerMotor.MinZ 공급
  - FieldSpawner.Drops: 존 예산 안에서 드랍 배치 (가치 값 = 예산 소비량,
    생성 간격 준수), 마지막 존 끝에 탈출/다음 스테이지 웨이포인트
- 재배선: GameFlow(FieldSpawner/ZoneDefinition), GreyboxSceneSetup 프리팹 템플릿,
  HudController(진행도 = StageProgress01, 붕괴 경고 = 제거 기준선 근접),
  CameraShake(기준선 근접 트레머), RunDebugDashboard(기준선/존 표시),
  SegmentEnvironment + EnvironmentAuthoringWindow(ZoneDefinition으로 시그니처 변경)
- 검증: 런타임/에디터/EditMode 테스트 3개 어셈블리 컴파일 0 에러
  (csproj가 stale해서 실제 파일 목록으로 보정 후 빌드 - Unity가 재생성하면 원복됨)

### 미결 (사용자 조치 필요)

- Unity에서 Scavenger > Setup Greybox Scene 1회 재실행 필요:
  스포너 프리팹의 SegmentSpawner 컴포넌트가 FieldSpawner로 교체됨.
  재실행 전에는 씬의 스포너 참조가 끊긴 상태 (missing script)
- EditMode 테스트 실행 미확인 (Unity MCP 연결 끊김 - 이번 세션 실행 불가)

### 다음 작업 (마일스톤 문서 순서)

- 2단계 파밍 포인트: 포인트 소켓/오브젝트 스팟 프리팹 규격, 상단1·하단2·3+랜덤,
  안전지대(기믹 면제 + 낙사 없음), 소켓 좌표 기준 제거 + 흔들림 시작 거리
- 3단계 아이템 오브젝트: 종류(상자/항아리) x 등급, 파밍 포인트 등급별 생성 확률,
  상호작용(버튼 1회 시작 / 이동·낙하 시 중단), 아이템 그룹 합산 확률 추첨 1개
- 4단계 가방 보강: 슬롯 한도, 버리기(압축 안 된 것 우선), 가방 정리(압축 2단계,
  파밍 포인트에서만, 이동/피해 시 중단)

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
## 2026-07-23 (9) - 웹 이식 2차: 카메라/자동수집/인터페이스/시각 (ADR-0008 연장)

사용자 지시로 웹 프로토타입을 시각/조작 전반까지 이식. 조작 방식(입력)은 유지.

### 완료

- 카메라 아이소: FollowCamera 직교(Orthographic) + pitch30/yaw-45(2:1 픽셀
  아이소) + size12. 전진(+z)=화면 오른쪽 위(웹 진행 방향, 좌우반전 수정).
  CameraOffsetXY로 시야 클리어런스 유도, CameraSide 부호 yaw에 연동.
- 길바닥 아이템 자동수집: LootSpot E홀드 루팅 -> 밟으면 자동 획득(무게 여유 시).
  조각낙하(LootPickup) 미사용. Collected 이벤트로 HUD 연출 훅.
- 아이템 발광: LootVisual emissive 큐브 + bob/회전 + 포인트라이트. Bloom 추가
  (TiltShiftProfile). 45도 기울여 마름모 룩.
- 아이템 아이콘: 이모지 금지 규약 준수 - LootIconMaker가 종류별 절차적 도형
  스프라이트 생성(Resources/LootIcons), LootIcons 런타임 로드. 가방슬롯/flyer 적용.
- 인터페이스 웹 이식: HP바/무게바/탐색목표/가방 5열 그리드(등급 외곽선+아이콘)/
  획득 토스트/진행 강조. 실시간 모션: flyToBag(메인+파티클4개 포물선 -> 가방
  꽂힘 + pop), 바 보간, 토스트 슬라이드인, 강조 팝, 피격 붉은 플래시.
- 바닥 그리드: GreyboxMaterials 그리드 텍스처/머티리얼 생성, SegmentPath가
  스트립에 적용(타일링 1m=1셀). 화살표 데칼 머티리얼 코드(배치는 후속).

### 검증

- 컴파일 에러 0건(전 단계). Play 실측: 자동수집 동작, flyer 생성(파티클 포함),
  아이콘 7종 로드, 바닥 그리드 78/82 스트립 적용, HUD 패널 활성 확인.

### 나중으로 (미결)

- 전진 방향 화살표 데칼 배치 (머티리얼 생성 코드만 있음, 스포너 배치 미구현).
- LootVisual/그리드 인스턴스 머티리얼 누수 검토(스트립 파괴 시 해제).
- 웹 5종 아이템(톱니/크리스탈/배터리/코일/희귀상자) 도입 여부(현재 tier 3종).
- 밸런스 세트 튜닝(무게압축 <-> 과적), 아이콘 모양/크기 눈 확인 후 조정.
- 보급 상자(2.5초 홀드 개봉) 웹 이식 여부.

---

## 2026-07-23 (8) - 웹 프로토타입 이식 (HP/속도감/장애물/등급) - ADR-0008

별도 웹 프로토타입(D:/NHN2/voxel-extraction-proto)을 정답지로 4영역 전면 이식.
사용자 결정: 퀵슬롯 제거 / HP 도입(A안 회복없음) / 전면 이식. 폴더 구조 불변.

### 완료

- HP 시스템 (A안): PlayerHealth 신설. Damage(amount, cause) 진입점, 0 이하 시
  기존 Kill로 위임(경로 단일화). 회복 없음. PlayerController.Health 게터 +
  리셋. GameFlow 폴백 보강. ApplyDamage 정적 순수 함수(테스트).
- 속도감(관성): PlayerMotor 지수 보간 가속(k=1-exp(-accel*dt*load)), 무게 실릴수록
  반응 저하. moveSpeed 4.2->5.0. smoothedMove 상태, 임펄스는 보간 제외.
- 무게 5단계: CarryLoad 절대임계 3단계 -> 비율 4경계 5단계(25/50/75/90% ->
  -7/-13/-23/-33%). maxCarryWeight 도입. enum/시그니처 변경 -> 테스트 재작성.
- 장애물 3종 (전부 HP 데미지형, DangerGrid 예고): HazardFloor(지속 26/s) /
  StrikeZone(반복 폭격, 예고 1.35s -> 40) / RollingBlock+Spawner(정면 스폰, 35).
  DepthCurve 수량 3종 + SegmentSpawner 배치 3종. 산포 시드 RunManager.Rng.
- 등급 합성 (A안): RunInventory 같은 id 5개 -> 상위 등급(무게 압축 + 가치 x6).
  Entry.Grade/Weight/Value. BankedCounts(합성 전 원본)로 스태시 저장 -> 창고 불변.
  RunSettlement.BankInventory 전환.
- HP HUD: HudController 체력 바 + 피격 붉은 플래시(Damaged 이벤트). HudCanvasTemplate 배선.

### 검증

- Unity 컴파일 에러 0건 (전 어셈블리). EditMode 테스트 52/52 통과
  (기존 46 + 신규 등급합성 5, 무게판정 재작성, 회계 회귀 1).
- 자체 검토에서 등급합성 회계 버그 1건 발견·수정: "평균 단가" 방식이 정수
  나눗셈 잔차로 TotalValue와 엔트리 합을 어긋나게 함 -> 제거 스택의 실제
  무게/가치 합을 정확히 회수하는 방식으로 재작성 + 회귀 테스트 추가.
- HudCanvas 프리팹 증분 배선 완료 (a안): 기존 프리팹 보존한 채 execute_code로
  HealthBar(Fill+Text)/DamageFlash 추가 + HudController 필드 배선. 기존 배선
  무손상 확인. HP 바/피격 플래시가 씬에 실제 노출됨.

### 나중으로 (미결)

- 밸런스 세트 튜닝: 등급 무게압축 <-> 과적 페널티는 플레이 확인 후 함께.
- /codex 검토: codex CLI 미설치 환경이라 자체 검토로 대체 수행. 설치 후
  정식 검토 재실행 여지.
- 회복 수단 도입 여부는 A안 밸런스 확인 후.

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
