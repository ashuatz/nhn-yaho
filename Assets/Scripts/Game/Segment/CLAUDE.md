# Segment/ - 구간 생성과 진행

네임스페이스: Scavenger.Segment

## 파일 목차

- SegmentDefinition.cs: ScriptableObject (길이, 복도 반폭, 벽 높이).
  라운드 템포(2-3분)는 길이 x 전진 속도로 결정 - 템포 튜닝은 여기서
- SegmentSpawner.cs: 생성/제거 단일 경계 (풀링 교체 대비).
  BuildSegment(depth, startZ) / DespawnAll. S2는 셸(바닥+벽)만

## 예정 (구현계획 v0.0.2)

- S3: 루트 배치 (시드 난수, RunManager.Rng)
- S4: 폭탄 배치 + 전 레인 봉쇄 금지 검증
- S5: ChoiceNode, 구간 체인, DepthCurve 스케일링
- S6: SignalEmitter (거리 신호)

## 규칙

- 배치 난수는 RunManager.Rng만 사용 (시드 재현성)
- 생성/제거는 SegmentSpawner 내부에만. 외부에서 Instantiate/Destroy 금지
