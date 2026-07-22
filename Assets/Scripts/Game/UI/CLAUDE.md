# UI/ - 플레이어 HUD (uGUI, M5-1)

네임스페이스: Scavenger.UI

IMGUI HUD(HudOverlay)는 M5-1에서 폐기. 전부 uGUI - HudCanvas 프리팹
(Assets/Prefabs/HudCanvas.prefab) 루트에 컴포넌트 3개가 상주하고,
패널/리그 참조는 에디터 템플릿(Editor/HudCanvasTemplate.cs)이 1회 생성 시 배선한다.
EventSystem 불사용 - 조작 컴포넌트가 Input System 포인터를 직접 판독.

## 파일 목차

- HudController.cs: HUD 패널 갱신 단일 지점. 가치 합계 + 무게/적재 단계(M2-1),
  상호작용 프롬프트(우하단 - 루팅 시작/조각 줍기/루팅 중 안내), 루팅 게이지,
  선택지 프롬프트, 신호 배너, 붕괴 근접 경고(12m), 런 결과 패널.
  상호작용 프롬프트 박스 = 모바일 홀드 버튼 겸용: 포인터가 박스 위에서 눌린 동안
  PlayerController.SetExternalInteractHeld(true) 공급 (E 키와 OR 합성).
  루팅 중에도 박스를 유지한다 - 사라지면 모바일 홀드가 끊겨 즉시 취소되므로
- VirtualJoystick.cs: 좌하단 가상 조이스틱 (ADR-0007). uGUI RectTransform 리그
  (baseRect/knobRect)를 프리팹이 배선, 원형 스프라이트는 런타임 생성 (에셋 저장 없음).
  베이스 원 안 드래그 = 2D 벡터 -> PlayerController.SetExternalMoveInput.
  마우스/터치 공용, 데드존(반경 비율). Running 상태에서만 표시/입력.
  실행 순서 -200 (PlayerController -100보다 먼저 - 1프레임 지연 방지)
- LootLabelLayer.cs: 아이템 머리 위 간략 설명 라벨 (사용자 지시: 뭐가 뭔지 표시).
  LootSpot.All / LootPickup.All(착지 조각)을 순회해 플레이어 반경 maxDistance(14m) 내
  대상을 화면 투영, 가까운 순으로 최대 maxLabels(8)개. 라벨 풀은 프리팹의
  비활성 LabelTemplate을 Awake에서 복제. 내용: 이름 +가치 (무게) / 한 줄 설명
  (LootDefinition.shortDescription)

## 규칙

- 숨김 정보(타이머 실수치, 실거리 수치) 노출 절대 금지. 그건 Diagnostics 대시보드 전용
- HUD가 읽는 정적 참조: LootSpot.Active/PromptTarget/All, LootPickup.PromptTarget/All,
  ChoiceNode.Active, SignalEmitter.LastMessage, CollapseFront.Instance
- HudCanvas 프리팹이 씬에 없으면 GameFlow가 경고 로그만 낸다 (런타임 생성 금지 규약).
  Scavenger > Setup Greybox Scene으로 재구성
- 폰트는 내장 LegacyRuntime.ttf (동적 폰트 - 한글은 OS 폰트 폴백). TMP 미사용
  (기본 SDF 폰트에 한글 글리프 없음)
