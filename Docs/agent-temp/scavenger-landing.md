# 랜딩 - 다음 세션 시작 가이드

새 세션(어느 PC든)은 이 문서부터 읽는다. 마지막 갱신: 2026-07-22.

## 한눈 요약

- 프로젝트: 폐지줍기 익스트랙션 (탈콥라이크), Unity 6000.3 / URP 그레이박스
- 브랜치: design/scavenger-roguelike-concept
- 진행: M1, M2-1, M3-1~3, M4-1~2 완료. 다음 = M5-1 (uGUI 전환)
  (순서 재편: M3 -> M4 -> M5(uGUI) -> M2-2~6, 사용자 지시)
- 조작 (ADR-0007): WASD/화살표 홀드 = 2D 벡터 이동 (뷰 좌표계, 대각 허용),
  좌하단 가상 조이스틱(드래그 = 아날로그), E 홀드 루팅 (가능 시 우하단 프롬프트),
  W/E 선택지, 클릭=라운드 재개, F1 대시보드
- 벨트스크롤: 카메라는 전진 전용, 플레이어는 카메라 영역 뒤로 이탈 불가
- 압박: 바닥 붕괴(CollapseFront) + 땅꺼짐(우회로 보존)/밀기 트랩 + 폭탄.
  위험 범위 = 바닥 셀 점멸(DangerGrid), 붕괴 = 셀 조각 틸트 낙하(SinkDebris).
  카메라 쉐이크 = 피격/근접 피드백. 낙사 있음. 점프 없음

## 세션 시작 체크리스트

1. git pull (다른 PC에서 푸시했을 수 있음)
2. 푸시 안 된 로컬 커밋 확인: git log origin/design/scavenger-roguelike-concept..HEAD
   - 이 PC가 GitHub 미인증이면 터미널에서 git push 1회 (브라우저 인증)
3. Unity 열고 컴파일 에러 없는지 확인
4. 캐릭터/배경이 마젠타면: Scavenger > Repair Greybox Materials 실행,
   사전 배치 배경은 Environment Authoring 윈도우에서 재생성
5. 씬 구성이 구버전이면: Scavenger > Setup Greybox Scene 재실행
6. Test Runner (EditMode) 실행 - 43개 통과 여부 (2026-07-22 43/43 실기 확인)

## 다음 작업 (착수 지시 예: "M5-1 진행")

- M5-1 uGUI 전환 (사용자 지시 확정): 가상 조이스틱 + HUD(가치/무게, 이후
  퀵슬롯/획득 피드 자리)를 uGUI로 재구성. 완료 기준: 기획 UI 항목 전부 화면에
  존재 + uGUI 기반
- 이후 순서: M2-2 획득 피드 -> M2-3~6 (인터랙션 키/HP/퀵슬롯/탈출 카드)
  (전체 표는 구현 계획 v0.0.3)
- 플레이 종료 후 EditMode Test Runner 1회 실행 (이번 세션은 플레이 모드라 미실행)

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
