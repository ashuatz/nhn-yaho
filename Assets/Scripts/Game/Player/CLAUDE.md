# Player/ - 플레이어 상태와 이동

네임스페이스: Scavenger.Player

## 파일 목차

- PlayerState.cs: 상태 enum (Advancing / Looting / AtChoice / Dead)
- PlayerController.cs: 상태 소유자 + 입력. 외부 진입점:
  TryBeginLoot / EndLoot, EnterChoice / ExitChoice, Kill, ResetForNewRun,
  SetExternalMoveInput(조이스틱 벡터), SetExternalInteractHeld(모바일 홀드 버튼,
  M5-1 - 키보드 E와 OR 합성). 낙사 판정 (y < -4 -> Kill("fall"))
- PlayerMotor.cs: CharacterController 이동. 2D 벡터 홀드 (ADR-0007):
  SetMoveInput(Vector2) = 입력이 있는 동안 이동, 아날로그 크기 비례. moveSpeed = 튜닝 지점.
  관성 (ADR-0008 이식): 지수 보간 가속 (k = 1 - exp(-acceleration*dt*load)),
  무게 실릴수록(SpeedScale 낮을수록 loadedAccelFloor까지) 반응 저하. smoothedMove가
  관성 속도 - 임펄스는 별도라 보간 제외(타격감 유지). acceleration/loadedAccelFloor 튜닝.
  SpeedScale = 외부 시스템(CarryLoad)이 설정하는 이동속도 배율 (1 = 정상).
  AddImpulse(Vector3) = 외부 충격 속도 (밀기 트랩, M3-3). impulseDamping으로 감쇠,
  입력과 합산 후 동일 클램프 적용. ResetVertical이 잔존 임펄스/관성도 초기화.
  x는 복도 반폭, z는 MinZ(바닥 제거 기준선)로 이동 전 사전 클램프. 누적 중력 = 낙사 지원.
  SetBoundsOverride(minX, maxX, minZ, maxZ) / ClearBoundsOverride = 이동 경계 교체
  (파밍 포인트 진입 - 복도 밖으로 나가되 구역 안에서 떨어지지 않게).
  후퇴 한계(MinZ)는 override보다 항상 우선 - 사라진 바닥으로는 걸어갈 수 없다
- PlayerHealth.cs: 체력 (HP, ADR-0008 A안 - 회복 없음). Damage(amount, cause) 진입점,
  지속 피해는 dps*dt로 매 프레임 호출. 0 이하면 PlayerController.Kill로 사망 위임
  (경로 단일화). HealthChanged/Damaged 이벤트(HUD 바/피격 플래시). maxHealth = 프리팹
  튜닝. ApplyDamage = 정적 순수 함수 (EditMode 테스트 대상). GameFlow가 폴백 보강.
- CarryLoad.cs: 무게 -> 이동속도 배율 (M2-1, ADR-0008 비율 재작성). RunInventory.
  TotalWeight를 maxCarryWeight 대비 비율로 환산해 5단계 판정(가벼움/보통/무거움/
  매우무거움/과적, 경계 25/50/75/90%) 후 Motor.SpeedScale 반영. LoadRatio(0..1) 노출.
  배율(heavy/veryHeavy/severe/overloaded)/maxCarryWeight = 프리팹 튜닝, 경계는 코드 상수.
  EvaluateStage(비율)/ResolveSpeedScale/StageLabel = 정적 순수 함수 (EditMode 테스트 대상)
- FollowCamera.cs: 아이소 쿼터뷰 (ADR-0008, pitch 30 / yaw -45) + 벨트스크롤
  (ADR-0007 5항): z 추적은 전진 전용 래칫 - 플레이어가 후퇴해도 물러나지 않고,
  BackLimitZ를 Motor.CameraMinZ로 공급해 가시 영역 밖 이탈을 막는다.
  BackLimitZ = 카메라 뷰포트 하단 에지(backEdgeViewportY)의 시선-지면(y=0) 교점 z
  - backLimitSlack (사용자 지시: 화면에 아슬아슬하게 걸릴 때까지 후퇴 허용,
  카메라 튜닝에 자동 추종). Camera 컴포넌트 부재 시 구 방식(래칫 - backLimitMargin) 폴백.
  리그 구성 순서:
  lookAtOffset(앵커 기준 월드축, 바라보는 지점) -> positionOffsetWorld(룩앳 기준
  월드축, 카메라 위치) -> positionOffsetLocal(시선 로컬축, 회전 확정 후 구도 시프트 -
  시선 방향 불변). followSmoothTime = 지연 추적(SmoothDamp, 0이면 즉시).
  포즈 매 프레임 재계산 - 플레이 중 튜닝 즉시 반영. 확정값은 코드 기본값에 반영 예정.
  투영 (사용자 지시): 기본은 **초망원 원근** (orthographic = false, fieldOfView 18).
  거리는 MatchedPerspectiveDistance = orthographicSize / tan(fov/2)로 자동 계산되어
  오쏘와 화면 크기가 같다 (fov 18 / size 12 -> 거리 75.77). cameraDistance는 오쏘 전용.
  클립 평면도 FollowCamera가 소유 - orthoNearClip/orthoFarClip(0.1 / 90)을 후퇴량만큼
  밀어 오쏘가 보던 월드 깊이 구간을 유지한다 (fov 18 -> 51.87 / 141.77).
  CameraOffsetXY(배경 가림 판정)도 실제 후퇴 거리를 쓴다 - 카메라가 멀어지면
  시선 밴드가 pitch 각도에 더 가까워지므로 판정도 함께 따라가야 한다.
  **fov를 바꾸면 후퇴량이 바뀌므로 카메라 기준 후처리 값도 함께 밀어야 한다**:
  LUT 포그 distanceRange, DOF focusDistance, (활성화 시) URP m_ShadowDistance.
  현재 값은 fov 18 기준 후퇴량 51.7656으로 맞춰져 있다
- PlayerStepAnimator.cs: 연속 이동용 워크 밥 (ADR-0006 개편). 이동 중 |sin| 홉 반복 +
  공중 스트레치, 정지 시 착지 스쿼시. 낙하 중 연출 정지. Visual 자식만 조작.
  bobHeight/bobFrequency/airStretch/landSquash = 인스펙터 튜닝 지점
- CameraShake.cs: 카메라 쉐이크 (Main Camera 프리팹, FollowCamera 뒤 실행 순서 +100).
  AddImpulse = 순간 충격 (폭발 피격/사망, 트라우마 제곱 커브 + 시간 감쇠),
  RequestTremor = 지속 위협 (매 프레임 요청, 땅 꺼짐 예고 등),
  붕괴 전선 근접 트레머 내장 (collapseTremorDistance 안에서 거리 비례).
  maxPositionOffset/maxRollDegrees/frequency/감쇠 = 프리팹 튜닝 지점

플레이어 리그(CC + 큐브 2개 비주얼)는 프리팹 (Assets/Prefabs/Player.prefab).

## 조작 (그레이박스, ADR-0007)

- WASD/화살표 홀드: 2D 벡터 이동 (누르는 동안만, 대각 허용, 크기 1 클램프)
- 좌하단 가상 조이스틱: 드래그 = 아날로그 벡터 (키보드와 합산)
- 입력 축 = 뷰 좌표계 (화면 위 = 카메라 전방 지면 투영). 변환은
  PlayerController.ScreenToWorldMove. 루팅 취소 좌우 판정은 화면 축 원본
- E 홀드: 루팅 (LootSpot이 InteractHeld를 읽음). 좌우 입력 유지 중엔 시작 불가
- 점프 없음. 낙사 있음 (바닥 없으면 추락, y < -4 사망)
- 좌클릭/스페이스: 라운드 종료 후 계속 (GameFlow)

## 규칙 (ADR-0001 + ADR-0006)

- 루팅 중 이동 완전 정지. 좌우 입력은 루팅 취소 신호 (판정은 LootSpot 소유)
- 후퇴는 붕괴 전선(Motor.MinZ)과 카메라 후방 한계(Motor.CameraMinZ) 중
  앞선 것까지만 가능 (벨트스크롤)
- 상태 전이는 PlayerController.Transition만 사용. 외부에서 State 직접 변경 불가
- CharacterController 텔레포트는 반드시 비활성화 후 위치 설정 (GameFlow.RecoverPlayerIfFallen)
