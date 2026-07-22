# Player/ - 플레이어 상태와 이동

네임스페이스: Scavenger.Player

## 파일 목차

- PlayerState.cs: 상태 enum (Advancing / Looting / AtChoice / Dead)
- PlayerController.cs: 상태 소유자 + 입력. 외부 진입점:
  TryBeginLoot / EndLoot, EnterChoice / ExitChoice, Kill, ResetForNewRun,
  SetExternalMoveInput(조이스틱 벡터). 낙사 판정 (y < -4 -> Kill("fall"))
- PlayerMotor.cs: CharacterController 이동. 2D 벡터 홀드 (ADR-0007):
  SetMoveInput(Vector2) = 입력이 있는 동안 이동, 아날로그 크기 비례. moveSpeed = 튜닝 지점.
  SpeedScale = 외부 시스템(CarryLoad)이 설정하는 이동속도 배율 (1 = 정상).
  AddImpulse(Vector3) = 외부 충격 속도 (밀기 트랩, M3-3). impulseDamping으로 감쇠,
  입력과 합산 후 동일 클램프 적용. ResetVertical이 잔존 임펄스도 초기화.
  x는 복도 반폭, z는 MinZ(붕괴 전선)로 이동 전 사전 클램프. 누적 중력 = 낙사 지원
- CarryLoad.cs: 무게 -> 이동속도 배율 (M2-1). RunInventory.TotalWeight를 읽어
  3단계(일반/과적/초과적) 판정 후 Motor.SpeedScale 반영. 임계(overloadedAt/
  severelyOverloadedAt)와 배율은 Player 프리팹 튜닝 지점.
  EvaluateStage/ResolveSpeedScale/StageLabel = 정적 순수 함수 (EditMode 테스트 대상)
- FollowCamera.cs: 대각 쿼터뷰~사이드뷰 로우앵글 (ADR-0003) + 벨트스크롤
  (ADR-0007 5항): z 추적은 전진 전용 래칫 - 플레이어가 후퇴해도 물러나지 않고,
  BackLimitZ(래칫 - backLimitMargin)를 Motor.CameraMinZ로 공급해 가시 영역 밖
  이탈을 막는다. 리그 구성 순서:
  lookAtOffset(앵커 기준 월드축, 바라보는 지점) -> positionOffsetWorld(룩앳 기준
  월드축, 카메라 위치) -> positionOffsetLocal(시선 로컬축, 회전 확정 후 구도 시프트 -
  시선 방향 불변). followSmoothTime = 지연 추적(SmoothDamp, 0이면 즉시).
  포즈 매 프레임 재계산 - 플레이 중 튜닝 즉시 반영. 확정값은 코드 기본값에 반영 예정
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
