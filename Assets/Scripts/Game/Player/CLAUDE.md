# Player/ - 플레이어 상태와 이동

네임스페이스: Scavenger.Player

## 파일 목차

- PlayerState.cs: 상태 enum (Advancing / Looting / AtChoice / Dead)
- PlayerController.cs: 상태 소유자 + 입력. 외부 진입점:
  TryBeginLoot / EndLoot, EnterChoice / ExitChoice, Kill, ResetForNewRun
- PlayerMotor.cs: CharacterController 이동. 클릭 스텝 전진 (ADR-0002):
  RequestStep = 고정 거리 트윈, 스텝 중 1회 버퍼. stepDistance/stepDuration = 이동느낌 튜닝 지점.
  복도 반폭은 이동 전 사전 클램프
- PlayerFactory.cs: 리그 생성. 로직 루트 + Visual 자식 (큐브 2개: 몸/머리, 콜라이더 제거)
- FollowCamera.cs: 대각 쿼터뷰~사이드뷰 로우앵글 (ADR-0003). x 고정, z만 추적.
  전방 주시점(lookAheadMeters)으로 원경 확보. offset/주시점은 에셋 생성 게이트 동결 대상

## 조작 (그레이박스, ADR-0002)

- 좌클릭 또는 스페이스: 한 스텝 전진 (기본은 정지)
- A/D, 좌우 화살표: 좌우 이동
- E 홀드: 루팅 (LootSpot이 InteractHeld를 읽음)

## 규칙 (ADR-0001 + ADR-0002)

- 기본 정지. 클릭 연타 속도 = 전진 속도. 자동 전진 없음
- 루팅 중 이동 완전 정지, 클릭(전진) 무시. 좌우 입력은 루팅 취소 신호 (판정은 LootSpot 소유)
- 상태 전이는 PlayerController.Transition만 사용. 외부에서 State 직접 변경 불가
- CharacterController 텔레포트는 반드시 비활성화 후 위치 설정 (GameBootstrap.TeleportPlayer)
