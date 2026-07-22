# 작업 워크로그 (세션 인계용)

다른 PC/세션에서 이어받기 위한 진행 기록. 최신 항목이 위.
시작 가이드는 scavenger-landing.md 참조.

---

## 2026-07-22 (3) - 마젠타 수정 / 가상 D-패드 / 랜딩 문서

- 마젠타 원인: 에디터에서 new Material(...)을 프리팹/씬에 저장 - 디스크 에셋이
  아니라 리로드 후 참조 깨짐. 수정:
  - GreyboxMaterials (에디터): Assets/Materials/Greybox/에 머티리얼 에셋 생성/재사용
  - 플레이어 템플릿/사전 배치 배경(에디트 모드)이 에셋 머티리얼 사용
  - Scavenger > Repair Greybox Materials: 기존 Player 프리팹 제자리 수리
    (다른 튜닝 값 보존). 사전 배치 배경은 윈도우에서 재생성
- 가상 D-패드 (VirtualDPad, 좌하단 십자): 클릭 = 4방향 토글 (키보드와 동일 규칙),
  활성 방향 하이라이트. GameFlow 프리팹 템플릿 포함 + 기존 프리팹은 런타임 폴백
- 랜딩 문서 scavenger-landing.md 작성 (세션 시작 체크리스트/다음 작업/결정 대기)
- 푸시 여전히 대기 (GitHub 인증). 로컬 커밋 5개

---

## 2026-07-22 (2) - M1 구현 + 리뷰 트리아지

### 완료

- M1-1 수직 요소: 단차(상판+21도 경사로, tier 2-3 보상 루트, 붕괴 연계) - SegmentSpawner.PopulateLedges
- M1-2 전방 시야 차단: 리니어 포그 10-42m (정보 차단용)
- 멀티렌즈 리뷰(3렌즈, 지적 21건) 후 직접 트리아지 - 아래 표 참조

### 리뷰 트리아지 결과

주의: 워크플로우 검증 단계가 조직 사용 한도 초과로 전멸해 반박 검증은 무효.
21건을 직접 판정했다.

수정함 (P1급):
1. 루팅/선택지 중 중력·붕괴클램프 정지 (붕괴 면역) -> 상태와 무관하게 motor.Tick 유지
2. MinZ 클램프가 정지 플레이어를 전선 속도로 밀어줌 (컨베이어 - 정지 압박 불성립)
   -> backwardLimit = min(MinZ, 현재z), 절대 앞으로 밀지 않음
3. 루팅 중 트리거 이탈/스팟 침몰 시 EndLoot 미호출 (Looting 소프트락)
   -> lootingPlayer 별도 추적, Release가 항상 EndLoot
4. 사전 배치(EnvironmentAuthoring) 구간 FloorStrip 미등록 (붕괴 무효)
   -> RegisterPreplacedStrips + CollapseFront 중복 등록 가드
5. 단차가 단일 FloorStrip(최대 10m)이라 붕괴 면역 구간 발생
   -> SinkThresholdZ = 시작 + min(깊이, 2m)
6. 시야 클리어런스가 카메라 반대편(-x) 낮은 지형까지 거부 (단차가 +x에만 스폰)
   -> 반대편 블록은 검사 제외 (NearPoint.x * pos.x < 0)
7. 단차 옆 바닥에서 상판 루트 도둑질 가능 (트리거 반경 겹침)
   -> LootSpot.requiredMinPlayerY (상판 높이 - 0.3)
8. 폭탄이 단차 z구간 옆 통로에 스폰 (통로 폭 < 폭발 지름 = 사실상 봉쇄)
   -> 단차 z구간 전체에서 폭탄 제외 (IsInLedgeZRange)
9. 잔존 탈출 잠금 데드코드 (ChoiceNode.IsExtractionLocked + HUD 분기) 제거
10. CollapseFront를 스포너 프리팹 템플릿에 추가 (런타임 폴백은 유지)
11. 단차 클리어런스 검사에 상판 루트 비주얼 포함

기각/보류 (문서화만):
- 코리도 루트/폭탄이 스트립 침몰에 동반 안 됨 - 전선 뒤라 도달 불가, 코스메틱 (P3 보류)
- PopulateLedges의 rng 소비 가변 - 같은 시드+같은 카메라 설정이면 결정적. 카메라
  튜닝 간 재현성은 스코프 밖으로 정의
- 사전 배치 유무에 따른 rng 스트림 분기 - 씬 구성이 다르면 다른 맵 (의도된 동작)
- BuildShell의 new Random(0) 폴백 - RunManager 부재 시(에디터 프리뷰)만, 의도됨
- 포그 42m vs 룩어헤드 확정 노출 - 기획 확정(전방 포그 가림)이 ADR-0004 연출 근거에
  우선. 룩어헤드는 포그 경계 팝인 방지 역할로 유지
- 클리어런스 카메라 지점이 positionOffsetLocal 미반영 - 근사 허용

### 푸시 대기 (이 PC GitHub 인증 필요)

로컬 커밋 (origin 대비 +3, 이번 트리아지 커밋 추가 예정):
- 8b92582 feat: replace click-step with four-way toggle movement and fall death
- 6d9076c feat: floor collapse pressure, tilt-shift view and background impulses
- 4277a18 docs: split remaining work into implementation plan v0.0.3

해결법: 아무 터미널에서 git push 1회 실행 -> Git Credential Manager 브라우저 로그인.

### 환경 메모

- 조직 월간 사용 한도 도달 - 서브에이전트 워크플로우/Codex 대량 호출 불가.
  교차 검증은 한도 리셋 또는 승급 후 재개 (/usage-credits)
- Unity MCP는 .mcp.json에 등록됨 (UnityMCP). 세션 시작 시 신뢰 승인 필요.
  Unity 에디터에서 Window > MCP For Unity 브릿지 Running 확인
- EditMode 테스트 (Test Runner) 실행은 아직 미확인 - Unity에서 1회 돌려볼 것

### 다음 작업 (구현 계획 v0.0.3)

즉시: M2-1 무게/가방 (LootDefinition.weight, 3단계 이속 배율, HUD 표시)
이후: M2-2 획득 피드 -> M3-1 위험 그리드 표시 -> M4-1 요소 4분리 -> M2-3~6 ...
보류 결정 대기: 스테미나 (무게 체감 후), 세계관, 이동 토글 해석 확정

---

## 2026-07-22 (1) - 기획 v0.0.2 반영 대개편

- 기획 메모 -> scavenger-extraction_v0.0.2_plan.md 정리 (확정 5 / 검토 6)
- ADR-0006: 4방향 토글 이동(가정: 1회=연속이동, 같은키=정지), 점프 제외, 낙사 도입,
  시간 압박 = 바닥 붕괴 (숨김 타이머 잠금 제거), 틸트 시프트 뷰
- 구현: 이동 재작성, FloorStrip/CollapseFront, RunTimer 스톱워치화, DoF 볼륨,
  brg-shooter 셀 바운스 임펄스 (폭발/붕괴 반응), 워크 밥
- 구현 계획 v0.0.3: M1 지형 / M2 파밍 / M3 위험 / M4 맵 4분리 / M5 UI 로 분할

## 2026-07-21 - v0.1 그레이박스 (요약)

- 기획 v0.0.1 -> 구현계획 v0.0.2 (Codex 교차검증 반영) -> S1~S7 수직 슬라이스 구현
- ADR-0001(루팅 정지/거리 신호) ADR-0002(클릭 스텝, 폐기됨) ADR-0003(심리스/카메라)
  ADR-0004(복층/사전배치/룩어헤드) ADR-0005(RenderMeshInstanced 배경)
- Codex 교차 검토 1회 반영 (P1 4건 등 9건)
- 프리팹 셋업 체계 (Assets/Prefabs, Main Camera.prefab 경로 고정)
