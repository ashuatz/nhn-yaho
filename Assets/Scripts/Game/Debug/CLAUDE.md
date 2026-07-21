# Debug/ - 진단 도구

네임스페이스: Scavenger.Diagnostics
(폴더명은 Debug지만 네임스페이스에 Debug를 쓰면 UnityEngine.Debug 참조가 깨져서 Diagnostics 사용)

## 파일 목차

- RunDebugDashboard.cs: IMGUI 온스크린 대시보드. F1 토글.
  상태/시드/깊이/숨김 타이머 실수치 표시. UNITY_EDITOR || DEVELOPMENT_BUILD 전용.
  이후 단계 확장은 DrawExtraSections에 추가.

## 규칙

- 숨김 정보(타이머 실수치, 실거리)는 여기에만 노출한다
- 그레이박스 단계라 IMGUI. uGUI 전환은 트랙 C에서 검토
