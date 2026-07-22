# Run/ - 런 수명 주기

네임스페이스: Scavenger.Run

## 파일 목차

- RunState.cs: 상태 enum (Ready / Running / Extracted / Dead)
- RunStateMachine.cs: 전이 검증 + StateChanged 이벤트. 전이 표는 IsValidTransition 참조
- RunTimer.cs: 경과 스톱워치 (템포 계측/로그용). 제한 시간/탈출 잠금은
  바닥 붕괴(Segment/CollapseFront)로 대체됨 (ADR-0006)
- RunSettings.cs: ScriptableObject (seedOverride). CreateDefault 폴백
- RunManager.cs: 얇은 코디네이터. 시드 확정, 깊이, 인벤토리 보유,
  StartRun/AdvanceDepth/CompleteExtraction/KillRun
- RunSettlement.cs: 정산. Extracted -> 인벤토리를 PlayerStash에 확정 저장,
  Dead -> 아무것도 반영 안 함. 중복 정산은 상태 머신 전이 규칙이 차단

## 핵심 규칙

- 시간 압박 = 바닥 붕괴 (CollapseFront). 타이머 기반 잠금 규칙 없음 (ADR-0006)
- RunManager는 조립만. 규칙 로직을 여기에 추가하지 말 것 (비대화 방지)

## 이벤트 흐름

- RunManager.RunStarted -> 구간 생성(S5), HUD 리셋(S3) 구독 예정
- RunManager.DepthChanged(int) -> 깊이 스케일링(S5) 구독 예정
- RunStateMachine.StateChanged -> 대시보드, 정산(S7) 구독 예정
