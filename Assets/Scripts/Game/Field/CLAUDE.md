# Field/ - 필드 규격과 절차 생성

네임스페이스: Scavenger.Field

기준 문서: Docs/agent-temp/필드_규칙_및_절차_v0.0.2.md
블록(1x1) - 존(25 x 7) - 필드(= 스테이지) 계층을 코드 용어까지 일치시킨 폴더.
구 Segment/SegmentSpawner + CollapseFront 스택을 대체한다 (ADR-0009).

## 필드 구조 (사용자 지시, 2026-07-25)

- 필드는 세그먼트가 끝없이 이어지는 체인이다. 스테이지 인계에 월드 재생성이 없다
- 세그먼트 2종: **존**(25블록, 드랍/파밍 포인트) / **구간**(junctionLengthBlocks,
  존과 존 사이. 탈출 웨이포인트만 놓인다)
- 스테이지 = 존 N개 + 뒤에 붙는 구간 1개. 구간을 걸어서 통과하면 다음 스테이지(깊이 +1)
- 웨이포인트는 Extract 하나뿐. 진행(Advance)은 오브젝트가 아니다 - 그냥 지나가면 된다
- 전방 패딩: 플레이어 앞으로 zoneLookAheadCount(기본 2) 존 거리를 미리 만든다.
  존이 생겨나는 장면이 화면에 잘려 보이지 않게 하는 유일한 장치

## 파일 목차

- ZoneDefinition.cs: 필드 규격 ScriptableObject (문서 2.1 / 4.2 컬럼).
  블록 크기 / 존 길이·너비(블록 수) / 스테이지 존 개수 min-max /
  구간 길이(junctionLengthBlocks) / 전방 패딩 존 개수(zoneLookAheadCount) /
  바닥 제거 시작 거리(칸) / 흔들림 시간 / 존 아이템 가치 예산 min-max /
  드랍 생성 간격 거리 / 파밍 포인트 컬럼(깊이·길이·게이트·단차·계단 길이).
  lengthMeters·corridorHalfWidth·JunctionLengthMeters·LookAheadDistance·
  FarmingPoint*는 블록 수에서 유도되는 읽기 전용 프로퍼티.
  RollZoneCount / RollDropBudget = 시드 기반 확정 (재현성).
- FloorRow.cs: 바닥 제거 단위 (문서 3.2 (3)). 존 너비 전체 x 1블록 두께.
  상태 3종 FloorRowState: Normal -> Pending(흔들림, 흔들림 시간) -> Falling(낙하).
  Pending은 아직 밟을 수 있는 마지막 경고 구간, Falling 시작과 동시에
  자식 포함 콜라이더 off (위에 있던 것은 낙사). 낙하 후 자체 파괴.
- Zone.cs: 세그먼트 하나 (문서 2.1 (2)). 바닥 행 목록 소유 + 범위(StartZ/EndZ) +
  IsJunction(구간 여부). 길이는 BuildContext.LengthBlocks가 정한다 -
  존과 구간이 같은 클래스를 쓰고 길이만 다르다.
  Build(BuildContext) 바닥 구성 우선순위:
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
  FieldSegmentKind(Zone/Junction) 정의. 세그먼트 리스트(FieldSegment)로 관리 -
  길이가 다른 구간이 끼어들어 인덱스 산술로는 위치를 되짚을 수 없다.
  StartStage(startZ) = 런 시작 전용 리셋 + 전방 패딩까지 초기 생성.
  Update = 제거 기준선 -> EnsureSegmentsAhead(전방 패딩) ->
  UpdatePlayerSegment(구간 통과 감지 = 깊이 +1) -> 세그먼트 제거.
  BuildNextSegment = 스테이지 존 개수를 채우면 구간을 넣고 BeginNextStage로 새 스테이지.
  제거 기준선 = 플레이어 z - 제거 시작 거리, 전진 전용 래칫.
  기준선을 PlayerMotor.MinZ로 공급해 되돌아가기를 막는다 (문서 3.2 (1)).
  세그먼트 정리 = 바닥이 전부 사라졌거나 기준선 한 존 뒤로 밀려났을 때.
  RemoveLineDistanceToPlayer = 근접 피드백 조회용 (CameraShake/HUD).
  StageIndex / StageZoneCount / CurrentZoneIndex(구간이면 -1) / StageProgress01 = 표시용.
  BuildSightClearance = 배경 카메라측 가림 방지 기준 (에디터 윈도우 공용).
- PointSocket.cs / ObjectSpot.cs: 아트 프리팹 계약 마커 (파밍 문서 2.2).
  빈 GameObject에 컴포넌트만 붙이고 위치만 잡는다 - 코드가 위치를 읽는다.
  소켓은 역할별 1개 (PointSocketRole: Entry = z- 입구 / Exit = z+ 출구),
  스팟은 복수(아이템 오브젝트 자리).
  규격 정본은 Docs/agent-temp/아트_협업_스케줄_v0.0.1.md 3.2
- FarmingPoint.cs: 파밍 포인트 (파밍 문서 2장 + 3층 구조 계획).
  타입(Top/Bottom) + 등급 위상 3종 + 플랫폼 높이(PlatformY: 상단 +, 하단 -).
  규격 주입은 Layout 구조체 (소켓 2개 / 깊이 / 길이 / 단차 / 흔들림 시작 거리).
  static PlayerInside / IsPlayerInSafeZone = 안전지대 판정 (위협 보류/가방 정리 게이트).
  진입: z 범위 안에서 복도-구역 x 범위를 합쳐 PlayerMotor.SetBoundsOverride,
  구역 위에서는 x/z를 구역으로 클램프해 낙사를 막는다 (2.6 (3)).
  계단 구간은 존 가장자리에서 시작하므로 이 x 범위에 자동으로 포함된다.
  제거: 흔들림은 **입구 소켓**(z-) 기준, 낙하는 **출구 소켓**(z+) 기준 -
  구역이 길어져 입구 기준으로 낙하시키면 출구 쪽에 서 있는 동안 발밑이 무너진다.
  구역 위에 있으면 클램프를 유지한 채 함께 낙하 (해제하면 옆으로 순간이동).
  소유권은 조건을 만족한 쪽이 즉시 가져간다 - 양보하면 1프레임 클램프 공백이 생긴다
- FieldSpawner.FarmingPoints.cs: 파밍 포인트 배치 (파밍 문서 2.4 / 2.5 + 3층 구조).
  존당 개수 min-max, 최소 간격, 상단 1번째 고정 / 하단 2번째 고정 / 3번째부터 50:50.
  상단 = 카메라 반대편(화면 위) +단차, 하단 = 카메라측(화면 아래) -단차 -
  배경과 같은 CameraSide 판정 공유 (카메라측에 솟는 지형은 발판을 가린다).
  PointMetrics = 치수 계산 한 곳 (게이트 z / 계단 x / 플랫폼 / 소켓 좌표).
  그레이박스 구성: 플랫폼 + 입구/출구 계단 + 게이트 사이 차단 매스(EdgeBarrier)
  + 보이지 않는 차단 콜라이더(EdgeBlocker). 시각 매스만 두면 위층에서 아래층으로
  뛰어내려 계단을 우회할 수 있고, 하단은 카메라측이라 매스를 본선 위로 올릴 수 없다
  (발판이 가려진다) - 그래서 시각과 차단을 분리한다 (Codex 검토 지적).
  계단은 시각(단 큐브) + 충돌(램프 콜라이더) 분리 - 단마다 CC가 튀지 않게.
  단 높이는 MaxStairRiser(0.25) 이하로 자동 분할 (Step Offset 0.3 통과 조건).
  아트 프리팹이 배선되면 프리팹을 인스턴스화하고 소켓은 마커의 월드 z로 판별한다
  (역할 필드가 아니라 z 기준 - 리그 반전 시 y축 180도 회전이 z까지 뒤집는다)
- FieldSpawner.Drops.cs: 배치 카테고리.
  PopulateDrops = 존 예산(아이템 가치 값 합) 안에서 바닥에 드랍 배치.
  가치가 높은 아이템일수록 예산을 많이 써서 적게 나온다 (드랍 문서 5.2 (3)).
  등장 존 제한 적용 (드랍 5.2 (4)) - 판정 기준은 스테이지 내 존 순번
  (buildStageZoneIndex. 배치가 생성 시점에 끝나므로 그 값이 유효하다).
  획득 거리는 PlayerController.itemCollectDistance를 주입 (드랍 9.3 플레이어 옵션).
  생성 간격 거리 준수, 배치 후 AttachToRow로 바닥에 부착.
  BuildJunctionWaypoint = 구간 중앙, 화면 위쪽(카메라 반대편)에 탈출 웨이포인트 1개
  (웹 이식 룩 - 발광 패드 + 빛 기둥 + 포인트라이트). 직진 동선을 비워 두는 이유는
  그냥 지나가려는 플레이어가 밟아서 강제 정산되지 않게 하기 위함

## 프리팹 / 데이터 자산 (아트 다듬기 대상)

에디터 메뉴 Scavenger > Ensure Field Prefabs (Setup Greybox Scene에 포함).
있으면 절대 덮어쓰지 않는다 - 아트가 다듬은 결과가 메뉴 재실행으로 사라지지 않게.

| 자산 | 경로 | 비고 |
|------|------|------|
| 바닥 타일 | Assets/Prefabs/Field/Tiles/FloorTile_A~C | 1블록, 콜라이더 없음 (행이 대표) |
| 타일 구성 | Assets/Settings/FieldTileSet.asset | 목록/가중치/노이즈/기울기 |
| 단일 메시 행 | Assets/Prefabs/Field/FloorRow.prefab | 타일셋 미사용 시 대안 |
| 파밍 포인트 | Assets/Prefabs/Field/FarmingPoint_Top / _Bottom | 계단 + 플랫폼 + 소켓 2개 + 스팟 |

프리팹 규격 정본은 Docs/agent-temp/아트_협업_스케줄_v0.0.1.md 3장.
파밍 포인트 원점은 **존 가장자리의 구역 중심(z 중앙)** 이고, 기본 아이소 리그
(카메라 +x) 기준으로 상단은 -x / 하단은 +x로 뻗으며 입구 소켓이 z- 쪽에 온다.
리그가 뒤집힌 경우에만 코드가 y축 180도로 돌려 붙인다 (음수 스케일은 콜라이더/노멀이
뒤집혀 금지) - 이때 z도 뒤집히므로 입구/출구는 마커의 월드 z로 다시 판별한다.
프리팹이 규격과 다르면 FarmingPoint.authoredDepthBlocks(x) /
authoredLengthBlocks(z)에 선언한다.

규격이 개정되어 기존 프리팹을 갱신해야 할 때는 메뉴
Scavenger > Rebuild Farming Point Prefabs (확인 대화 후 덮어쓴다. 경로가 같아
GUID가 유지되므로 스포너 프리팹 참조는 끊기지 않는다).

## 규칙

- 배치 난수는 반드시 RunManager.Rng (시드 재현성). 존 개수/예산/좌표 모두 해당.
  바닥 타일 선택/기울기는 좌표 기반 노이즈 - 스테이지당 오프셋만 시드에서 뽑는다
- 런타임 생성물의 머티리얼은 GreyboxPalette 경유 (Common.mat 기반 공유 인스턴스).
  renderer.material 직접 접근은 개체마다 1장을 만들므로 지양
- 존/행 생성과 제거는 FieldSpawner 경계 안에서만. 외부에서 Instantiate/Destroy 금지
- 바닥은 존이 소유한다. 배경 생성기(Segment/SegmentEnvironment)는 장식 블록만 만든다
- 기믹(장애물)은 현재 전부 제거된 상태 - 기믹 문서 확정 후 별도 폴더에서 재도입
- 파밍 포인트는 2단계 작업 (포인트 소켓/오브젝트 스팟). 존 옆에 붙는 구역이며
  출구 소켓 좌표가 제거 기준선에 들어가면 통째 낙하 (파밍 문서 2.7)
- 세그먼트 길이가 다르므로 위치 계산에 `stageStartZ + index * length` 산술을
  다시 도입하지 말 것. 위치는 FieldSegment의 StartZ/EndZ가 정본이다

## 미구현 (문서 대비)

- 아이템 오브젝트: 종류·등급, 생성 확률, 상호작용, 아이템 그룹 추첨 (파밍 문서 3장).
  자리(ObjectSpot)와 스팟 마커는 이미 생성된다 - 오브젝트만 얹으면 된다
- 가방 슬롯 한도 / 버리기 / 가방 정리(무게 압축) (가방 문서 2.3 / 6장).
  가방 정리는 FarmingPoint.IsPlayerInSafeZone으로 게이트할 것
- 구간(존 사이) 전용 룩: 지금은 존과 같은 바닥/배경이고 웨이포인트 빛기둥만 다르다.
  정산 구간으로 읽히는 지형/연출은 아트 항목
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
