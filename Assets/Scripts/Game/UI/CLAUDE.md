# UI/ - 플레이어 HUD

네임스페이스: Scavenger.UI

## 파일 목차

- HudOverlay.cs: IMGUI HUD. 가치 합계 + 무게/적재 단계(M2-1, D-패드 오른쪽 하단),
  루팅 게이지, 선택지 프롬프트, 신호 배너, 붕괴 근접 경고(12m), 런 결과 패널(클릭 재개).
- VirtualDPad.cs: 좌하단 가상 D-패드 (기획: 왼쪽 아래 조작계). 클릭 = 4방향 토글,
  키보드와 동일 규칙(PlayerController.RequestToggle). 활성 방향 하이라이트.
  Running 상태에서만 표시. buttonSize/margin = 인스펙터 튜닝 지점.

## 규칙

- 숨김 정보(타이머 실수치, 실거리 수치) 노출 절대 금지. 그건 Diagnostics 대시보드 전용
- 그레이박스 단계라 IMGUI. uGUI 전환은 트랙 C
