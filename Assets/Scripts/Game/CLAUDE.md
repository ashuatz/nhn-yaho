# Scavenger.Game 어셈블리

폐지줍기 익스트랙션 그레이박스 (구현계획 v0.0.2, 수직 슬라이스 S1..S7).
참조: Unity.InputSystem. 루트 네임스페이스 Scavenger.

## 폴더 맵

| 폴더 | 네임스페이스 | 역할 |
|------|--------------|------|
| Run/ | Scavenger.Run | 런 상태 머신, 숨김 타이머, 시드/깊이, 정산 |
| Debug/ | Scavenger.Diagnostics | 디버그 대시보드 (Debug 네임스페이스는 UnityEngine.Debug와 충돌하여 회피) |
| Player/ | Scavenger.Player | (S2) 상태 기반 이동 |
| Loot/ | Scavenger.Loot | (S3) 루팅, 인벤토리, 스태시 |
| Segment/ | Scavenger.Segment | (S2/S5) 구간 생성, 선택지, 거리 신호 |
| Obstacle/ | Scavenger.Obstacle | (S4) 폭탄, 밀기 트랩, 낙하물 존(RockfallZone) |
| UI/ | Scavenger.UI | (S3+) 플레이어 HUD - M5-1부터 uGUI (HudCanvas 프리팹) |

## 루트 파일

- GameFlow.cs: 런 흐름 배선만 담당 (오브젝트 생성 금지 - 런타임 부트스트랩 회피 규약).
  씬 참조는 직렬화 필드 + 자동 탐색 폴백.
  라운드는 심리스 (ADR-0003): 종료 후 클릭 = 현재 위치에서 다음 라운드.
  텔레포트 없음, 월드는 플레이어 앞으로 재생성.

## 씬 구성 (프리팹 기반)

- 시스템은 전부 Assets/Prefabs/ 프리팹으로 관리: Main Camera / Player / RunSystems /
  SegmentSpawner / GameFlow / HudCanvas (M5-1 uGUI). 사용자가 프리팹을 직접 수정해 튜닝한다.
- Scavenger > Setup Greybox Scene = 프리팹 인스턴스 배치 + 참조 배선 + 라이트/포그.
  프리팹이 없으면 기본 템플릿으로 1회 생성, 있으면 절대 덮어쓰지 않음
  (Scavenger > Ensure Prefabs로 프리팹만 생성 가능).
- 카메라 프리팹 경로 고정: Assets/Prefabs/Main Camera.prefab (사용자 지정).
- 카메라 시야 클리어런스: 카메라-복도 시선 밴드와 겹치는 배경 블록은 생성 자체가
  거부된다 (SightClearance, 런타임/사전배치 동일 규칙).

## 전역 규칙

- 숨김 타이머 실수치는 Diagnostics 대시보드에만 노출. 플레이어 HUD 노출 금지.
- 난수는 반드시 RunManager.Rng (시드 기반) 사용. UnityEngine.Random 금지 (재현성).
  - 게임 결과에 닿는 지연 산포(조각 착지, 착탄점)는 배치 시 RunManager.Rng에서
    시드를 배정받아 로컬 System.Random 사용 (런타임 직접 소비 금지 - 스트림 오염)
  - 순수 비주얼(파편, 붕괴 조각)은 new System.Random(GetInstanceID()) 허용
- 이모지/유니코드 특수문자 금지 (프로젝트 규약).
