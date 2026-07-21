# Player/ - 플레이어 상태와 이동

네임스페이스: Scavenger.Player

## 파일 목차

- PlayerState.cs: 상태 enum (Advancing / Stopped / Looting / AtChoice / Dead)
- PlayerController.cs: 상태 소유자 + 입력. 외부 진입점:
  TryBeginLoot / EndLoot (S3), EnterChoice / ExitChoice (S5), Kill (S4), ResetForNewRun
- PlayerMotor.cs: CharacterController 이동. 복도 반폭은 이동 전 사전 클램프
- PlayerFactory.cs: 리그 생성. 로직 루트 + Visual 자식 (큐브 2개: 몸/머리, 콜라이더 제거)
- FollowCamera.cs: 쿼터뷰 추적. x 고정, z만 추적. offset은 에셋 생성 게이트 동결 대상

## 조작 (그레이박스)

- A/D, 좌우 화살표: 좌우 이동
- S, 아래 화살표 홀드: 정지 (브레이크)
- E 홀드: 루팅 (S3에서 LootSpot이 InteractHeld를 읽음)

## 규칙 (ADR-0001)

- 루팅 중 이동 완전 정지. 좌우 입력은 루팅 취소 신호 (판정은 LootSpot 소유)
- 상태 전이는 PlayerController.Transition만 사용. 외부에서 State 직접 변경 불가
- CharacterController 텔레포트는 반드시 비활성화 후 위치 설정 (GameBootstrap.TeleportPlayer)
