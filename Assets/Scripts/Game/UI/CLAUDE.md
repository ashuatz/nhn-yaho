# UI/ - 플레이어 HUD

네임스페이스: Scavenger.UI

**현행은 UI Toolkit** (사용자 지시 2026-07-25). uGUI 경로(HudController / VirtualJoystick /
LootLabelLayer + HudCanvas 프리팹)는 롤백용으로 파일만 남아 있고 씬에는 배치되지 않는다.
둘을 함께 두면 같은 정보가 두 번 그려지므로 씬에는 하나만 둔다.

## 현행 (UI Toolkit)

자산: Assets/UI/Hud.uxml (레이아웃) / Hud.uss (스타일) /
UnityDefaultRuntimeTheme.tss (런타임 테마) / HudPanelSettings.asset (패널 설정).
씬 배치는 Assets/Prefabs/HudDocument.prefab (UIDocument + 아래 뷰 3종).
생성/배선은 에디터 메뉴 **Scavenger > Ensure HUD Document (UI Toolkit)**
(Setup Greybox Scene에 포함). 있으면 덮어쓰지 않는다.

- HudView.cs: 패널 갱신 단일 지점. 실행 순서 -150 (입력 공급이 PlayerController -100보다 먼저).
  타이머(RunTimer.Remaining) / 체력 바 + 피격 플래시(PlayerHealth.Damaged) /
  무게 바 + 5단계 라벨(CarryLoad) / 수집 요약(종류·가치) / 가방 슬롯 /
  붕괴 근접 경고(RemoveLineDistanceToPlayer) / 진행 마일스톤 안내(StageProgress01) /
  런 결과 패널 / 상호작용 홀드 버튼(LootPickup.PromptTarget 있을 때만).
  가방 슬롯은 RunInventory.SlotCapacity만큼 만들고 재사용한다 - 빈 칸이 곧 슬롯 한도 표시.
  외곽선 = 현재 등급(합성 반영), 아이콘 = 아이템 시작 등급, 좌상단 점 = 압축 가능 여부
- HudJoystickView.cs: 가상 조이스틱 (ADR-0007). 실행 순서 -200.
  UI Toolkit 포인터 이벤트 + CapturePointer를 쓴다 - 포인터 캡처가 멀티터치 추적을
  대신 처리하므로 uGUI 버전의 touchId 고정 로직이 필요 없다.
  패널 좌표는 y가 아래로 증가하므로 화면 기준(위가 +)으로 뒤집어서 넘긴다.
  PointerCaptureOut에서도 입력을 놓는다 (놓지 않으면 계속 걷는다)
- HudLootLabelView.cs: 아이템 머리 위 라벨. LootSpot.All / LootPickup.All을 순회해
  플레이어 반경(maxDistance) 내 가까운 순으로 maxLabels개.
  월드 -> 패널 좌표는 RuntimePanelUtils.CameraTransformWorldToPanel.
  카메라 뒤쪽(viewport z <= 0)은 그리지 않는다 - 뒤집힌 좌표로 화면에 튄다.
  라벨은 풀로 재사용 (매 프레임 생성하면 GC가 튄다)

## 함정 (실제로 겪은 것)

- **폰트는 코드가 넣는다** (HudView.ApplyFont). USS의
  `-unity-font-definition: resource("LegacyRuntime.ttf")`는 런타임 패널에서 해석되지
  않아 폰트가 null이 되고 **글자가 아예 그려지지 않는다** (박스만 보인다).
  기본 런타임 테마 폰트에는 한글 글리프가 없으므로, 내장 동적 폰트
  LegacyRuntime.ttf(OS 폰트 폴백)를 루트에 넣고 자식이 상속받게 한다
- 바인딩은 지연 처리한다. UIDocument는 자기 OnEnable에서 트리를 만들고 이 뷰들은
  실행 순서가 더 빠르므로, OnEnable 시점에는 rootVisualElement가 null이다
  (EnsureBound를 Update에서 호출)
- 루트는 picking-mode="Ignore". 화면 전체를 덮으므로 픽킹을 켜두면 클릭이 게임에
  도달하지 않는다. 조작 요소(조이스틱/버튼)만 픽킹을 받는다

## 구버전 (uGUI, 롤백 경로)

- HudController.cs / VirtualJoystick.cs / LootLabelLayer.cs + Editor/HudCanvasTemplate.cs
- 획득 플라이어 연출과 토스트 큐는 uGUI 버전에만 있다 - UI Toolkit으로 옮기지 않았다
  (연출 문서 확정 후 진행)
- EventSystem 불사용, 포인터 직접 판독 방식

## 규칙

- 숨김 정보(타이머 실수치, 실거리 수치) 노출 금지. 그건 Diagnostics 대시보드 전용
  (F1 IMGUI 대시보드는 개발 빌드 전용이며 화면 좌상단 HUD와 겹친다 - 의도된 상태)
- 레이아웃/색은 UXML/USS가 소유한다. 코드는 수치와 표시 여부만 넣는다
- HudDocument 프리팹이 씬에 없으면 GameFlow가 경고 로그만 낸다 (런타임 생성 금지 규약)
