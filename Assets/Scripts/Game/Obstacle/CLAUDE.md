# Obstacle/ - 장애물

네임스페이스: Scavenger.Obstacle

## 파일 목차

- Bomb.cs: 시한폭발형. Initialize(감지 반경, 기폭 시간, 폭발 반경).
  감지 반경 진입 시 기폭 시작(배치 시점 아님), 점멸 가속, 폭발 반경 내 플레이어 즉사.
  기폭 시작 시 DangerGrid로 폭발 반경 셀 표시 (M3-1), 폭발 시 근접 비례 CameraShake.
- PushTrap.cs: 밀기 트랩 (M3-3). 감지 반경 진입 -> 예고 점멸 -> 붕괴 쪽(-z)으로
  PlayerMotor.AddImpulse. 쿨다운 후 재장전 (머무르면 반복). Initialize(반경, 예고,
  밀기 속도, 쿨다운) - 수치는 SegmentSpawner 필드가 주입.

## 규칙

- 추상화 유예: Bomb/PushTrap이 아직 구조를 공유하지 않아 공통 계약 미추출.
  3종째 또는 구조 중복 발생 시 추출 검토
- 기폭은 반드시 근접 트리거로 시작. 배치 시점 카운트다운 금지
- 배치 시 전 레인 봉쇄 금지 검증은 SegmentSpawner가 소유 (최소 z 간격)
- 깊이 스케일링(기폭 시간 감소, 밀도 증가)은 S5 DepthCurve에서
