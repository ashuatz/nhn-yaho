# Segment/ - 구간 생성과 진행

네임스페이스: Scavenger.Segment

## 파일 목차

- SegmentDefinition.cs: ScriptableObject (길이, 복도 반폭, 벽 높이).
  라운드 템포(2-3분)는 길이 x 전진 속도로 결정 - 템포 튜닝은 여기서
- SegmentSpawner.cs: 생성/제거 단일 경계 (풀링 교체 대비).
  BuildSegment(depth, startZ) / DespawnAll / DespawnBehind(z).
  셸 + 루트(tier 가중치) + 폭탄(z 간격 검증) + ChoiceNode 배치.
  전진 콜백에서 다음 구간 생성과 뒤쪽 정리 수행 (동시 생존 최대 2구간)
- DepthCurve.cs: 깊이 스케일링 단일 소스. tier 가중치 / 폭탄 수 / 기폭 시간 / 폭발 반경.
  모든 값 클램프 - 통과 불가 배치 방지 (blastMaxRadius x 2 < 복도 폭 유지 필수)
- ChoiceNode.cs: 구간 끝 선택지. W = 전진, E = 탈출. static Active = HUD 프롬프트 참조.
  IsExtractionLocked 델리게이트로 탈출 잠금 판정 주입 (S6)

## 예정 (구현계획 v0.0.2)

- S6: SignalEmitter (거리 신호), ChoiceNode 탈출 잠금 연결

## 규칙

- 배치 난수는 RunManager.Rng만 사용 (시드 재현성)
- 생성/제거는 SegmentSpawner 내부에만. 외부에서 Instantiate/Destroy 금지
