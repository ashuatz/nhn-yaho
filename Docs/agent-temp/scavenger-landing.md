# 랜딩 - 다음 세션 시작 가이드

새 세션(어느 PC든)은 이 문서부터 읽는다. 마지막 갱신: 2026-07-22.

## 한눈 요약

- 프로젝트: 폐지줍기 익스트랙션 (탈콥라이크), Unity 6000.3 / URP 그레이박스
- 브랜치: design/scavenger-roguelike-concept
- 진행: M1, M2-1, M3-1~3, M4-1~2, M5-1(uGUI) 완료 + 낙하물/파밍 드롭 추가.
  다음 = M2-2~6
- 조작 (ADR-0007): WASD/화살표 홀드 = 2D 벡터 이동 (뷰 좌표계, 대각 허용),
  좌하단 가상 조이스틱(uGUI, 드래그 = 아날로그), E 홀드 = 루팅/조각 줍기 공용
  (우하단 프롬프트 박스 = 모바일 홀드 버튼 겸용), W/E 선택지, 클릭=라운드 재개,
  F1 대시보드
- 벨트스크롤: 카메라는 전진 전용, 후퇴 한계 = 화면 하단 에지 지면 교점
  (아슬아슬하게 걸릴 때까지, FollowCamera가 자동 산출)
- 파밍: 루팅 완료 = 조각 3개 포물선 낙하 -> 주워야 획득. 아이템 머리 위
  uGUI 라벨 (이름/가치/무게/설명)
- 압박: 바닥 붕괴(CollapseFront) + 땅꺼짐(우회로 보존)/밀기 트랩 + 폭탄 +
  낙하물(돌 낙하, 발판 파괴) + 돌진 적(앞에서 뒤로) + 기둥 붕괴(쓰러지며 발판 파괴).
  위험 범위 = 바닥 셀 점멸(DangerGrid), 붕괴 = 셀 조각 틸트 낙하(SinkDebris).
  카메라 쉐이크 = 피격/근접 피드백. 낙사 있음. 점프 없음
- 생존: 체력 3히트(폭탄/기둥/낙하물 2, 적 1, 낙사 즉사) + 스테미나
  (과적 이동 시 소모, 탈진 = 감속). HUD = 가방(가치/무게/목록) + 체력/스테미나 바
- 체크포인트: 구간 끝 9m 안전지대 (넓은 플랫폼, 공격/붕괴 면제).
  벗어나면 통째로 분해 낙하 + 붕괴 재개. 길 폭 = 반폭 5.25 (1.5배)
- 배경: 좌우 비대칭 - 카메라측은 계단식 하강(발판 가림 금지),
  시야측은 럽블 능선 + 중경/원경 스카이라인

## 세션 시작 체크리스트

1. git pull (다른 PC에서 푸시했을 수 있음)
2. 푸시 안 된 로컬 커밋 확인: git log origin/design/scavenger-roguelike-concept..HEAD
   - 이 PC가 GitHub 미인증이면 터미널에서 git push 1회 (브라우저 인증)
3. Unity 열고 컴파일 에러 없는지 확인
4. 캐릭터/배경이 마젠타면: Scavenger > Repair Greybox Materials 실행,
   사전 배치 배경은 Environment Authoring 윈도우에서 재생성
5. 씬 구성이 구버전이면: Scavenger > Setup Greybox Scene 재실행
6. Test Runner (EditMode) 실행 - 43개 통과 여부 (2026-07-22 43/43 실기 확인)

## 다음 작업 (착수 지시 예: "M2-2 진행")

- M2-2 획득 피드: 조각 줍기가 사실상 획득 피드 역할을 하므로 기획 재확인 후
  간소화 여부 결정
- 이후 순서: M2-3~6 (인터랙션 키/HP/퀵슬롯/탈출 카드) (전체 표는 구현 계획 v0.0.3)
- uGUI 배치/폰트 크기 튜닝: HudCanvas 프리팹에서 직접 (플레이테스트 피드백 대기)

## 사용자 결정 대기

- 스테미나 도입 여부 (M2-1 완료 - 플레이에서 무게 체감 확인 후 판단)
- 세계관 (현대/판타지) - 리소스 착수 전 필요
- 튜닝 확인: 과적 임계 8/16, 배율 0.7/0.45 (Player 프리팹 CarryLoad) /
  트랩 수치 (SegmentSpawner 프리팹) / 쉐이크 (Main Camera 프리팹 CameraShake) /
  조도 (SegmentSpawner 프리팹 DepthLighting)

## 문서 맵

- 기획: Docs/agent-temp/scavenger-extraction_v0.0.2_plan.md
- 구현 계획(마일스톤): Docs/agent-temp/scavenger-impl_v0.0.3_plan.md
- 진행 기록: Docs/agent-temp/scavenger-worklog.md (세션마다 항목 추가할 것)
- 결정: Docs/adr/ADR-0001~0007 (0007 = 2D 벡터 홀드 이동, 0006 이동부 대체)
- 코드 맵: Assets/Scripts/Game/CLAUDE.md + 폴더별 CLAUDE.md

## 환경 제약 (2026-07-22 기준)

- Codex 한도 해소됨. M1 + M2-1 교차 검토 완료 (지적 전부 반영/보류 처리 -
  워크로그 2026-07-22 (4) 참조)
- Unity MCP: .mcp.json에 UnityMCP 등록됨. 세션 시작 시 신뢰 승인 +
  Unity 에디터에서 MCP 브릿지 Running이면 씬 조작/테스트를 에이전트가 직접 가능
- 튜닝값은 프리팹에 있음: CollapseFront(SegmentSpawner 프리팹), PlayerMotor(Player),
  FollowCamera(Main Camera), TiltShiftProfile.asset, EnvironmentRenderer(출렁임/임펄스)
