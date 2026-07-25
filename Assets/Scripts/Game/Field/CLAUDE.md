# Field/ - 필드 규격과 절차 생성

네임스페이스: Scavenger.Field

기준 문서: Docs/agent-temp/필드_규칙_및_절차_v0.0.1.md
블록(1x1) - 존(25 x 7) - 필드(= 스테이지) 계층을 코드 용어까지 일치시킨 폴더.
구 Segment/SegmentSpawner + CollapseFront 스택을 대체한다 (ADR-0009).

## 파일 목차

- ZoneDefinition.cs: 필드 규격 ScriptableObject (문서 2.1 / 4.2 컬럼).
  블록 크기 / 존 길이·너비(블록 수) / 스테이지 존 개수 min-max /
  바닥 제거 시작 거리(칸) / 흔들림 시간 / 존 아이템 가치 예산 min-max /
  드랍 생성 간격 거리. lengthMeters·corridorHalfWidth는 블록 수에서 유도되는
  읽기 전용 프로퍼티 (배경 생성기 공용 입력).
  RollZoneCount / RollDropBudget = 시드 기반 확정 (재현성).
- FloorRow.cs: 바닥 제거 단위 (문서 3.2 (3)). 존 너비 전체 x 1블록 두께.
  상태 3종 FloorRowState: Normal -> Pending(흔들림, 흔들림 시간) -> Falling(낙하).
  Pending은 아직 밟을 수 있는 마지막 경고 구간, Falling 시작과 동시에
  자식 포함 콜라이더 off (위에 있던 것은 낙사). 낙하 후 자체 파괴.
- Zone.cs: 존 하나 (문서 2.1 (2)). 바닥 행 목록 소유 + 존 범위(StartZ/EndZ) +
  IsLastZone. Build(BuildContext) 바닥 구성 우선순위:
  타일셋(노이즈 조합) -> 행 프리팹(단일 메시) -> 코드 큐브 폴백.
  타일 행은 빈 컨테이너 + 너비만큼 타일이고, 콜라이더는 타일에서 걷어내 행에
  BoxCollider 하나만 둔다 (타일 수만큼 물리 비용을 늘리지 않는다).
  UpdateRemoval(기준선, 흔들림 시간) = 기준선 뒤 행을 Pending으로 전환.
  AttachToRow = 요소를 발밑 행의 자식으로 부착 (바닥과 함께 낙하).
  IsFullyRemoved = 남은 행 없음 (스포너가 존 오브젝트 정리).
- GreyboxPalette.cs: 런타임 생성물의 머티리얼 공급기 (사용자 지시).
  기준 = Assets/Materials/Greybox/Common.mat, FieldSpawner가 직렬화 참조로 주입.
  색당 1장만 만들어 돌려쓴다 - 이전에는 오브젝트마다 인스턴스를 떠서 존 하나에
  수십 장이 생기고 SRP 배칭도 색마다 끊겼다. Apply / ApplyGlow(발광).
  기준 머티리얼 미주입 시 프리미티브 기본 머티리얼 복제로 폴백
- FieldTileSet.cs: 존 바닥 타일 구성 SO (사용자 지시: 타일 프리팹 리스트 +
  노이즈 기반 배치). 타일 목록(가중치) / noiseScale / maxTiltDegrees(1도 미만) /
  tileSurfaceOffsetY / tileThickness.
  PickTile = 월드 좌표 펄린 노이즈로 가중치 선택 (같은 타일이 뭉쳐 패치가 생긴다),
  SampleTilt = 좌표 해시로 타일마다 다른 roll/pitch (yaw는 건드리지 않는다).
  노이즈 오프셋은 스테이지 진입 시 런 시드에서 1회 뽑는다 - 시드 재현성 유지
- FieldSpawner.cs: 필드 생성/제거의 단일 경계 (문서 3.1). static Instance.
  StartStage(startZ) = 존 개수 확정 + 현재/다음 존 생성.
  Update = 제거 기준선 갱신 -> 존 체인 유지(최대 3개: 직전/현재/다음) -> 행 제거 전이.
  제거 기준선 = 플레이어 z - 제거 시작 거리, 전진 전용 래칫.
  기준선을 PlayerMotor.MinZ로 공급해 되돌아가기를 막는다 (문서 3.2 (1)).
  RemoveLineDistanceToPlayer = 근접 피드백 조회용 (CameraShake/HUD).
  StageProgress01 = 스테이지 진행도 (HUD 마일스톤 안내).
  BuildSightClearance = 배경 카메라측 가림 방지 기준 (에디터 윈도우 공용).
- PointSocket.cs / ObjectSpot.cs: 아트 프리팹 계약 마커 (파밍 문서 2.2).
  빈 GameObject에 컴포넌트만 붙이고 위치만 잡는다 - 코드가 위치를 읽는다.
  소켓은 파밍 포인트당 1개(입구 + 제거 판정 기준), 스팟은 복수(아이템 오브젝트 자리).
  규격 정본은 Docs/agent-temp/아트_협업_스케줄_v0.0.1.md 3.2
- FarmingPoint.cs: 파밍 포인트 (파밍 문서 2장). 타입(Top/Bottom) + 등급 위상 3종.
  static PlayerInside / IsPlayerInSafeZone = 안전지대 판정 (위협 보류/가방 정리 게이트).
  진입: 소켓 z 범위 안에서 복도-구역 x 범위를 합쳐 PlayerMotor.SetBoundsOverride,
  구역 위에서는 x/z를 구역으로 클램프해 낙사를 막는다 (2.6 (3)).
  제거: 소켓 z가 제거 기준선에 닿으면 흔들림(흔들림 시작 거리) -> 통째 낙하 (2.7).
  구역 위에 있으면 클램프를 유지한 채 함께 낙하 (해제하면 옆으로 순간이동).
  소유권은 조건을 만족한 쪽이 즉시 가져간다 - 양보하면 1프레임 클램프 공백이 생긴다
- FieldSpawner.FarmingPoints.cs: 파밍 포인트 배치 (파밍 문서 2.4 / 2.5).
  존당 개수 min-max, 최소 간격, 상단 1번째 고정 / 하단 2번째 고정 / 3번째부터 50:50.
  상단 = 카메라 반대편(화면 위), 하단 = 카메라측 - 배경과 같은 CameraSide 판정 공유.
  그레이박스 지형을 코드 생성한다. 아트 프리팹이 들어오면 BuildGreyboxPoint를
  프리팹 인스턴스화로 교체하고 소켓/스팟은 마커에서 읽는다 (계약 동일)
- FieldSpawner.Drops.cs: 배치 카테고리.
  PopulateDrops = 존 예산(아이템 가치 값 합) 안에서 바닥에 드랍 배치.
  가치가 높은 아이템일수록 예산을 많이 써서 적게 나온다 (드랍 문서 5.2 (3)).
  생성 간격 거리 준수, 배치 후 AttachToRow로 바닥에 부착.
  BuildStageExit = 마지막 존 끝에 탈출 지점(Extract) / 다음 스테이지(Advance)
  웨이포인트 (웹 이식 룩 - 발광 패드 + 빛 기둥 + 포인트라이트).

## 프리팹 / 데이터 자산 (아트 다듬기 대상)

에디터 메뉴 Scavenger > Ensure Field Prefabs (Setup Greybox Scene에 포함).
있으면 절대 덮어쓰지 않는다 - 아트가 다듬은 결과가 메뉴 재실행으로 사라지지 않게.

| 자산 | 경로 | 비고 |
|------|------|------|
| 바닥 타일 | Assets/Prefabs/Field/Tiles/FloorTile_A~C | 1블록, 콜라이더 없음 (행이 대표) |
| 타일 구성 | Assets/Settings/FieldTileSet.asset | 목록/가중치/노이즈/기울기 |
| 단일 메시 행 | Assets/Prefabs/Field/FloorRow.prefab | 타일셋 미사용 시 대안 |
| 파밍 포인트 | Assets/Prefabs/Field/FarmingPoint_Top / _Bottom | 소켓 원점 + 스팟 마커 |

프리팹 규격 정본은 Docs/agent-temp/아트_협업_스케줄_v0.0.1.md 3장.
파밍 포인트는 소켓이 원점(0,0,0)이고 +x로 뻗는 형태로 만든다 - 반대편은 코드가
y축 180도 회전으로 붙인다 (음수 스케일은 콜라이더/노멀이 뒤집혀 금지).
프리팹이 규격 크기와 다르면 FarmingPoint.authoredSizeBlocks에 선언한다.

## 규칙

- 배치 난수는 반드시 RunManager.Rng (시드 재현성). 존 개수/예산/좌표 모두 해당.
  바닥 타일 선택/기울기는 좌표 기반 노이즈 - 스테이지당 오프셋만 시드에서 뽑는다
- 런타임 생성물의 머티리얼은 GreyboxPalette 경유 (Common.mat 기반 공유 인스턴스).
  renderer.material 직접 접근은 개체마다 1장을 만들므로 지양
- 존/행 생성과 제거는 FieldSpawner 경계 안에서만. 외부에서 Instantiate/Destroy 금지
- 바닥은 존이 소유한다. 배경 생성기(Segment/SegmentEnvironment)는 장식 블록만 만든다
- 기믹(장애물)은 현재 전부 제거된 상태 - 기믹 문서 확정 후 별도 폴더에서 재도입
- 파밍 포인트는 2단계 작업 (포인트 소켓/오브젝트 스팟). 존 옆에 붙는 구역이며
  소켓 좌표가 제거 기준선에 들어가면 통째 낙하 (파밍 문서 2.7)

## 미구현 (문서 대비)

- 아이템 오브젝트: 종류·등급, 생성 확률, 상호작용, 아이템 그룹 추첨 (파밍 문서 3장).
  자리(ObjectSpot)와 스팟 마커는 이미 생성된다 - 오브젝트만 얹으면 된다
- 가방 슬롯 한도 / 버리기 / 가방 정리(무게 압축) (가방 문서 2.3 / 6장).
  가방 정리는 FarmingPoint.IsPlayerInSafeZone으로 게이트할 것
- 파밍 포인트 단차 (상단 높게 / 하단 낮게): 아트 프리팹 R&D 항목이라
  그레이박스는 보행면과 같은 높이(flush)로 만든다 (파밍 문서 2.4 (1))
- 스테이지별 컬럼 분리 (지금은 ZoneDefinition 하나가 전 스테이지 공통값)

## 배경 블록 (GPU 인스턴싱)

- FieldSpawner.buildBackgroundBlocks로 on/off (기본 on).
  드로우는 Segment/EnvironmentRenderer (Graphics.RenderMeshInstanced)
- 팔레트 머티리얼도 GreyboxPalette.GetTinted 경유 - 배경까지 Common.mat
  쉐이더를 공유한다 (사용자 지시). 팔레트 색당 1장이고 파괴 책임은 팔레트에 있다
  (EnvironmentRenderer가 Destroy하면 다른 생성물의 머티리얼까지 깨진다)
- 파밍 포인트 영역(RecordFootprint)과 겹치는 블록은 생성 후 필터로 버린다.
  생성 자체를 막지 않는 이유는 rng 소비 순서를 유지해 같은 시드에서 같은 배치가
  나오게 하기 위함. 영역 기록은 존마다 초기화되며, 조기 반환보다 앞에서 비운다
  (파밍 포인트가 없는 존에서 이전 존 기록이 남으면 엉뚱한 블록이 지워진다)
