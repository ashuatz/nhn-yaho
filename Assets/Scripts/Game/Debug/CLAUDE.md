# Debug/ - 진단 도구

네임스페이스: Scavenger.Diagnostics
(폴더명은 Debug지만 네임스페이스에 Debug를 쓰면 UnityEngine.Debug 참조가 깨져서 Diagnostics 사용)

## 파일 목차

- RunDashboardView.cs: 온스크린 진단 대시보드 (UI Toolkit. 사용자 지시 2026-07-26로
  구 IMGUI RunDebugDashboard를 대체). UNITY_EDITOR || DEVELOPMENT_BUILD 전용.
  - 배치: HUD와 **같은 UIDocument**(Assets/Prefabs/HudDocument.prefab)에 얹는다.
    패널 설정/스케일/폰트를 HUD와 공유하려는 것 - 별도 문서를 두면 폰트를 또 넣어야 한다
  - 레이아웃/스타일: Assets/UI/Dashboard.uxml + Dashboard.uss (코드는 수치만 넣는다).
    프리팹 배선은 Scavenger > Ensure HUD Document (UI Toolkit)
  - 표시: **기본은 접힌 상태**(헤더 한 줄 = 깊이/상태/붕괴 거리 요약).
    헤더 클릭 = 접기/펼치기, F1 = 패널 표시/숨김
  - 섹션 4종: RUN(상태·시드·깊이·경과·스테이지 경과) /
    FIELD(스테이지·존·진행도 막대·제거선 z·제거선 거리) /
    PLAYER(상태·위치·안전지대·상호작용 대상) / LOOT(종류·슬롯·가치·무게·필드 아이템 수)
  - 갱신은 refreshInterval(기본 0.1초) 간격 - 매 프레임 문자열을 만들지 않는다
  - 실행 순서 -140: HudView(-150)가 루트에 폰트를 넣은 뒤에 트리를 붙인다

## 규칙

- 숨김 정보(타이머 실수치, 제거선 실거리)는 여기에만 노출한다.
  플레이어 HUD에는 근접 경고 등 정성 신호만 (UI/CLAUDE.md 규칙)
- 패널 루트는 picking-mode Ignore, 헤더 버튼만 픽킹을 받는다.
  본문까지 픽킹을 켜면 화면 좌측 클릭이 게임에 도달하지 않는다
- 값 갱신만 코드가 한다. 색/여백/배치는 Dashboard.uss 소유
