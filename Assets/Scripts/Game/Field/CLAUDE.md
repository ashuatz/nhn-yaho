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
  IsLastZone. UpdateRemoval(기준선, 흔들림 시간) = 기준선 뒤 행을 Pending으로 전환.
  AttachToRow = 요소를 발밑 행의 자식으로 부착 (바닥과 함께 낙하).
  IsFullyRemoved = 남은 행 없음 (스포너가 존 오브젝트 정리).
- FieldSpawner.cs: 필드 생성/제거의 단일 경계 (문서 3.1). static Instance.
  StartStage(startZ) = 존 개수 확정 + 현재/다음 존 생성.
  Update = 제거 기준선 갱신 -> 존 체인 유지(최대 3개: 직전/현재/다음) -> 행 제거 전이.
  제거 기준선 = 플레이어 z - 제거 시작 거리, 전진 전용 래칫.
  기준선을 PlayerMotor.MinZ로 공급해 되돌아가기를 막는다 (문서 3.2 (1)).
  RemoveLineDistanceToPlayer = 근접 피드백 조회용 (CameraShake/HUD).
  StageProgress01 = 스테이지 진행도 (HUD 마일스톤 안내).
  BuildSightClearance = 배경 카메라측 가림 방지 기준 (에디터 윈도우 공용).
- FieldSpawner.Drops.cs: 배치 카테고리.
  PopulateDrops = 존 예산(아이템 가치 값 합) 안에서 바닥에 드랍 배치.
  가치가 높은 아이템일수록 예산을 많이 써서 적게 나온다 (드랍 문서 5.2 (3)).
  생성 간격 거리 준수, 배치 후 AttachToRow로 바닥에 부착.
  BuildStageExit = 마지막 존 끝에 탈출 지점(Extract) / 다음 스테이지(Advance)
  웨이포인트 (웹 이식 룩 - 발광 패드 + 빛 기둥 + 포인트라이트).

## 규칙

- 배치 난수는 반드시 RunManager.Rng (시드 재현성). 존 개수/예산/좌표 모두 해당
- 존/행 생성과 제거는 FieldSpawner 경계 안에서만. 외부에서 Instantiate/Destroy 금지
- 바닥은 존이 소유한다. 배경 생성기(Segment/SegmentEnvironment)는 장식 블록만 만든다
- 기믹(장애물)은 현재 전부 제거된 상태 - 기믹 문서 확정 후 별도 폴더에서 재도입
- 파밍 포인트는 2단계 작업 (포인트 소켓/오브젝트 스팟). 존 옆에 붙는 구역이며
  소켓 좌표가 제거 기준선에 들어가면 통째 낙하 (파밍 문서 2.7)

## 미구현 (문서 대비)

- 파밍 포인트 배치/제거, 아이템 오브젝트 상호작용 (파밍 문서 2~3장)
- 가방 슬롯 한도 / 버리기 / 가방 정리(무게 압축) (가방 문서 2.3 / 6장)
- 스테이지별 컬럼 분리 (지금은 ZoneDefinition 하나가 전 스테이지 공통값)
