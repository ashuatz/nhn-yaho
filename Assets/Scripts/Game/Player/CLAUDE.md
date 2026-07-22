# Player/ - 플레이어 상태와 이동

네임스페이스: Scavenger.Player

## 파일 목차

- PlayerState.cs: 상태 enum (Advancing / Looting / AtChoice / Dead)
- PlayerController.cs: 상태 소유자 + 입력. 외부 진입점:
  TryBeginLoot / EndLoot, EnterChoice / ExitChoice, Kill, ResetForNewRun.
  낙사 판정 (y < -4 -> Kill("fall"))
- PlayerMotor.cs: CharacterController 이동. 4방향 토글 (ADR-0006):
  ToggleDirection = 같은 방향 재입력 시 정지, 다른 방향 시 전환. moveSpeed = 튜닝 지점.
  x는 복도 반폭, z는 MinZ(붕괴 전선)로 이동 전 사전 클램프. 누적 중력 = 낙사 지원
- FollowCamera.cs: 대각 쿼터뷰~사이드뷰 로우앵글 (ADR-0003). 리그 구성 순서:
  lookAtOffset(앵커 기준 월드축, 바라보는 지점) -> positionOffsetWorld(룩앳 기준
  월드축, 카메라 위치) -> positionOffsetLocal(시선 로컬축, 회전 확정 후 구도 시프트 -
  시선 방향 불변). followSmoothTime = 지연 추적(SmoothDamp, 0이면 즉시).
  포즈 매 프레임 재계산 - 플레이 중 튜닝 즉시 반영. 확정값은 코드 기본값에 반영 예정
- PlayerStepAnimator.cs: 연속 이동용 워크 밥 (ADR-0006 개편). 이동 중 |sin| 홉 반복 +
  공중 스트레치, 정지 시 착지 스쿼시. 낙하 중 연출 정지. Visual 자식만 조작.
  bobHeight/bobFrequency/airStretch/landSquash = 인스펙터 튜닝 지점

플레이어 리그(CC + 큐브 2개 비주얼)는 프리팹 (Assets/Prefabs/Player.prefab).

## 조작 (그레이박스, ADR-0006)

- WASD/화살표: 4방향 토글 이동 (1회 = 그 방향 연속 이동, 같은 키 = 정지, 다른 키 = 전환)
- E 홀드: 루팅 (LootSpot이 InteractHeld를 읽음)
- 점프 없음. 낙사 있음 (바닥 없으면 추락, y < -4 사망)
- 좌클릭/스페이스: 라운드 종료 후 계속 (GameFlow)

## 규칙 (ADR-0001 + ADR-0006)

- 루팅 중 이동 완전 정지, 토글 입력 무시. 좌우 입력은 루팅 취소 신호 (판정은 LootSpot 소유)
- 후퇴는 붕괴 전선(Motor.MinZ 클램프)까지만 가능
- 상태 전이는 PlayerController.Transition만 사용. 외부에서 State 직접 변경 불가
- CharacterController 텔레포트는 반드시 비활성화 후 위치 설정 (GameFlow.RecoverPlayerIfFallen)
