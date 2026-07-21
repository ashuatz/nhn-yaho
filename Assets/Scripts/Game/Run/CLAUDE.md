# Run/ - 런 수명 주기

네임스페이스: Scavenger.Run

## 파일 목차

- RunState.cs: 상태 enum (Ready / Running / Extracted / Dead)
- RunStateMachine.cs: 전이 검증 + StateChanged 이벤트. 전이 표는 IsValidTransition 참조
- RunTimer.cs: 숨김 제한 시간. Begin/Stop, IsExpired = 탈출 잠금 (사망 아님)
- RunSettings.cs: ScriptableObject (타이머 랜덤 범위, seedOverride). CreateDefault 폴백
- RunManager.cs: 얇은 코디네이터. 시드 확정, 깊이, StartRun/AdvanceDepth/CompleteExtraction/KillRun

## 핵심 규칙

- 타이머는 루팅/선택지 중에도 진행 (구현계획 v0.0.2 섹션 0.2)
- 시간 초과 = IsExpired = 탈출 노드 잠금. 즉사 아님
- 타이머 한계값도 시드 난수에서 산출 - 같은 시드는 같은 런을 재현
- RunManager는 조립만. 규칙 로직을 여기에 추가하지 말 것 (비대화 방지)

## 이벤트 흐름

- RunManager.RunStarted -> 구간 생성(S5), HUD 리셋(S3) 구독 예정
- RunManager.DepthChanged(int) -> 깊이 스케일링(S5) 구독 예정
- RunStateMachine.StateChanged -> 대시보드, 정산(S7) 구독 예정
