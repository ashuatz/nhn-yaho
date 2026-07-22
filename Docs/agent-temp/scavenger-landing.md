# 랜딩 - 다음 세션 시작 가이드

새 세션(어느 PC든)은 이 문서부터 읽는다. 마지막 갱신: 2026-07-22.

## 한눈 요약

- 프로젝트: 폐지줍기 익스트랙션 (탈콥라이크), Unity 6000.3 / URP 그레이박스
- 브랜치: design/scavenger-roguelike-concept
- 진행: 구현 계획 v0.0.3의 M1 완료. 다음 = M2-1 (무게/가방)
- 조작: WASD/화살표 4방향 토글(1회=연속이동, 같은키=정지), E 홀드 루팅,
  W/E 선택지, 클릭=라운드 재개, F1 대시보드, 좌하단 가상 D-패드
- 압박: 뒤에서 바닥 붕괴(CollapseFront). 타이머 잠금 없음. 낙사 있음. 점프 없음

## 세션 시작 체크리스트

1. git pull (다른 PC에서 푸시했을 수 있음)
2. 푸시 안 된 로컬 커밋 확인: git log origin/design/scavenger-roguelike-concept..HEAD
   - 이 PC가 GitHub 미인증이면 터미널에서 git push 1회 (브라우저 인증)
3. Unity 열고 컴파일 에러 없는지 확인
4. 캐릭터/배경이 마젠타면: Scavenger > Repair Greybox Materials 실행,
   사전 배치 배경은 Environment Authoring 윈도우에서 재생성
5. 씬 구성이 구버전이면: Scavenger > Setup Greybox Scene 재실행
6. Test Runner (EditMode) 실행 - 27개 통과 여부 (아직 실기 실행 미확인)

## 다음 작업 (착수 지시 예: "M2-1 진행")

- M2-1 무게/가방: LootDefinition.weight, RunInventory 무게 합산,
  3단계(일반/과적/초과적) 이동속도 배율, HUD 무게 표시.
  완료 기준: 많이 주울수록 느려져 붕괴에 쫓기는 선택 압박 성립
- 이후 순서: M2-2 획득 피드 -> M3-1 위험 그리드 -> M4-1 요소 4분리 -> M2-3~6 ...
  (전체 표는 구현 계획 v0.0.3)

## 사용자 결정 대기

- 이동 토글 해석 확정 (현재: 1회=연속이동, 같은키=정지 가정)
- 스테미나 도입 여부 (M2-1 무게 체감 후 판단 권장)
- 세계관 (현대/판타지) - 리소스 착수 전 필요

## 문서 맵

- 기획: Docs/agent-temp/scavenger-extraction_v0.0.2_plan.md
- 구현 계획(마일스톤): Docs/agent-temp/scavenger-impl_v0.0.3_plan.md
- 진행 기록: Docs/agent-temp/scavenger-worklog.md (세션마다 항목 추가할 것)
- 결정: Docs/adr/ADR-0001~0006
- 코드 맵: Assets/Scripts/Game/CLAUDE.md + 폴더별 CLAUDE.md

## 환경 제약 (2026-07-22 기준)

- 조직 월간 사용 한도 도달: 서브에이전트 워크플로우/Codex 대량 호출 불가.
  한도 리셋 후 M1 배치에 대한 Codex 교차 검토 1회 권장 (워크로그의 트리아지
  기각 항목 재확인 포함)
- Unity MCP: .mcp.json에 UnityMCP 등록됨. 세션 시작 시 신뢰 승인 +
  Unity 에디터에서 MCP 브릿지 Running이면 씬 조작/테스트를 에이전트가 직접 가능
- 튜닝값은 프리팹에 있음: CollapseFront(SegmentSpawner 프리팹), PlayerMotor(Player),
  FollowCamera(Main Camera), TiltShiftProfile.asset, EnvironmentRenderer(출렁임/임펄스)
