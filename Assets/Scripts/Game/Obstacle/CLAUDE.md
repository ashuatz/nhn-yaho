# Obstacle/ - 장애물과 위협

네임스페이스: Scavenger.Obstacle

## 파일 목차

- Bomb.cs: 시한폭발형. Initialize(감지 반경, 기폭 시간, 폭발 반경).
  감지 반경 진입 시 기폭 시작(배치 시점 아님), 점멸 가속, 폭발 반경 내 플레이어
  피해 2 (PlayerHealth 없으면 즉사 폴백). 기폭 시작 시 DangerGrid로 폭발 반경 셀
  표시 (M3-1), 폭발 시 근접 비례 CameraShake.
- PushTrap.cs: 밀기 트랩 (M3-3). 감지 반경 진입 -> 예고 점멸 -> 붕괴 쪽(-z)으로
  PlayerMotor.AddImpulse. 쿨다운 후 재장전 (머무르면 반복). Initialize(반경, 예고,
  밀기 속도, 쿨다운) - 수치는 SegmentSpawner 필드가 주입.
- RockfallZone.cs: 낙하물 존 (1회 발동). 접근 -> 착탄점 예고 (플레이어 전방
  0.9~2.4m + 좌우 산포, DangerGrid 원 점멸 + 근접 트레머) -> 돌 낙하.
  직격 = 피해 2, 착탄 바닥은 FloorBreaker.SinkWithSafeLane (분할 침몰 + 우회로).
  scatterSeed = 배치 시 RunManager.Rng 배정 (재현성). rng 지연 초기화
  (AddComponent 직후 Awake가 시드 주입보다 먼저 돌기 때문).
- ChargingEnemy.cs: 돌진 적 존 (1회 발동). 접근 -> 레인 예고 (DangerGrid 사각
  점멸, 발동 시점 플레이어 x 부근) -> 텔레그래프 후 앞(포그 너머 spawnAhead)에서
  뒤(-z)로 질주. 접촉 = 피해 1 + 밀려남 (Motor.AddImpulse, 1회만).
  플레이어 뒤 12m 통과 시 자멸. scatterSeed 규칙 동일.
- ToppleColumn.cs: 기둥 붕괴 존 (1회 발동). 복도 가장자리에 서 있는 기둥
  (길이 = 복도 폭 - 안전 레인). 접근 -> 예고 (흔들림 + 발자국 사각 점멸 + 트레머)
  -> 밑동 피벗으로 복도를 가로질러 쓰러짐. 깔림 = 피해 2, 발자국 바닥은
  FloorBreaker로 분할 침몰 (반대쪽 안전 레인). 카메라측 배치 금지 - 세워진
  기둥이 발판을 가리므로 항상 카메라 반대편에서 카메라 쪽으로 쓰러진다.

## 규칙

- 공용 계약: 발동형 존은 Armed -> (예고) -> 실행 페이즈 구조, DangerGrid 예고 필수,
  run 비활성 시 Cancel, OnDisable에서 위험 표시 해제. 바닥 파괴는 FloorBreaker
  (Segment/) 경유 - 전폭 봉쇄 금지 (안전 레인)
- 피해는 PlayerHealth.Damage 경유 (없으면 Kill 폴백). 즉사는 낙사만
- 체크포인트 안전지대(CheckpointZone.PlayerInside)에서는 발동 보류
- 기폭/발동은 반드시 근접 트리거로 시작. 배치 시점 카운트다운 금지
- 배치 시 전 레인 봉쇄 금지 검증은 SegmentSpawner가 소유 (최소 z 간격)
- 발동 시점 산포 시드는 배치 시 RunManager.Rng에서 배정 (게임 결과 난수 = 재현성)
- 깊이 스케일링(수량/기폭 시간)은 DepthCurve에서
