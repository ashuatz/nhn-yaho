# UI/ - 플레이어 HUD

네임스페이스: Scavenger.UI

## 파일 목차

- HudOverlay.cs: IMGUI HUD. 가치 합계 + 무게/적재 단계(M2-1, D-패드 오른쪽 하단),
  루팅 게이지, 선택지 프롬프트, 신호 배너, 붕괴 근접 경고(12m), 런 결과 패널(클릭 재개).
- VirtualJoystick.cs: 좌하단 가상 조이스틱 (ADR-0007, 토글 D-패드 대체).
  베이스 원 안 드래그 = 2D 벡터 -> PlayerController.SetExternalMoveInput.
  마우스/터치 공용, 데드존(반경 비율). Running 상태에서만 표시/입력.
  baseRadius/knobRadius/margin/deadZone = 인스펙터 튜닝 지점. uGUI 전환은 M5-1.

## 규칙

- 숨김 정보(타이머 실수치, 실거리 수치) 노출 절대 금지. 그건 Diagnostics 대시보드 전용
- 그레이박스 단계라 IMGUI. uGUI 전환은 트랙 C
