# UI/ - 플레이어 HUD

네임스페이스: Scavenger.UI

## 파일 목차

- HudOverlay.cs: IMGUI HUD. 가치 합계(좌하단), 루팅 게이지(중앙), 런 결과 패널.
  S5에서 선택지 프롬프트, S6에서 신호 배너 추가 예정.

## 규칙

- 숨김 정보(타이머 실수치, 실거리 수치) 노출 절대 금지. 그건 Diagnostics 대시보드 전용
- 그레이박스 단계라 IMGUI. uGUI 전환은 트랙 C
