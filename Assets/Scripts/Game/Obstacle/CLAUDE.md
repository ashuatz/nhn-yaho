# Obstacle/ - 장애물

네임스페이스: Scavenger.Obstacle

## 파일 목차

- Bomb.cs: 시한폭발형. Initialize(감지 반경, 기폭 시간, 폭발 반경).
  감지 반경 진입 시 기폭 시작(배치 시점 아님), 점멸 가속, 폭발 반경 내 플레이어 즉사.
  기폭 시작 시 DangerGrid로 폭발 반경 셀 표시 (M3-1), 폭발 시 근접 비례 CameraShake.
- PushTrap.cs: 밀기 트랩 (M3-3). 감지 반경 진입 -> 예고 점멸 -> 붕괴 쪽(-z)으로
  PlayerMotor.AddImpulse. 쿨다운 후 재장전 (머무르면 반복). Initialize(반경, 예고,
  밀기 속도, 쿨다운) - 수치는 SegmentSpawner 필드가 주입.
- RockfallZone.cs: 낙하물 존 (1회성). 접근 시 착탄 예고 -> 돌 낙하 -> 직격 즉사
  (Kill) + 착탄 발판 파괴(우회로 남김). 착탄 산포는 RunManager.Rng 시드.

## 웹 프로토타입 이식 장애물 (ADR-0008, 전부 HP 데미지형)

- HazardFloor.cs: 바닥 장판 (지속 피해). 사각 영역 위에 서 있는 동안
  PlayerHealth.Damage(dps*dt, "hazard"). DangerGrid.ShowRect로 상시 표시
  (예고 아닌 지속 위험). 발판 위(공중)/사망 시 면제. halfWidthX/Z, damagePerSecond
  = SegmentSpawner 주입. Health 없으면 무시 (즉사 아님).
- StrikeZone.cs: 미사일 폭격 구역 (반복). 플레이어가 activateDistance 안이면
  재장전 반복 - 3~5셀 예고(telegraphSeconds) -> 폭발 PlayerHealth.Damage(40, "strike").
  회피 가능. 셀 산포는 배치 시 RunManager.Rng 시드(scatterSeed). NextRange 지연 초기화.
- RollingBlock.cs: 굴러오는 개별 블록. -z로 굴러오며 접촉 시 Damage(35, "rolling", 1회).
  뒤로 지나가거나 CollapseFront.FrontZ 도달 시 자멸. 이동/충돌만 담당.
- RollingBlockSpawner.cs: 굴림블록 스포너 (구역 기반). 근처면 interval마다
  플레이어 정면(spawnAheadDistance 앞)에 RollingBlock 스폰. 스폰 x 산포는
  RunManager.Rng 시드. 웹의 전역 타이머를 구역 기반으로 이식.

## 규칙

- 추상화 유예: Bomb/PushTrap이 아직 구조를 공유하지 않아 공통 계약 미추출.
  3종째 또는 구조 중복 발생 시 추출 검토
- 기폭은 반드시 근접 트리거로 시작. 배치 시점 카운트다운 금지
- 배치 시 전 레인 봉쇄 금지 검증은 SegmentSpawner가 소유 (최소 z 간격)
- 깊이 스케일링(기폭 시간 감소, 밀도 증가)은 S5 DepthCurve에서
