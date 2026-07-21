# Segment/ - 구간 생성과 진행

네임스페이스: Scavenger.Segment

## 파일 목차

- SegmentDefinition.cs: ScriptableObject (길이, 복도 반폭, 벽 높이).
  라운드 템포(2-3분)는 길이 x 전진 속도로 결정 - 템포 튜닝은 여기서
- SegmentSpawner.cs: 생성/제거 단일 경계 (풀링 교체 대비).
  BuildSegment(depth, startZ) / DespawnAll / DespawnBehind(z).
  셸 + 루트(tier 가중치) + 폭탄(z 간격 검증) + ChoiceNode 배치.
  전진 콜백에서 다음 구간 생성과 뒤쪽 정리 수행 (동시 생존 최대 2구간)
- DepthCurve.cs: 깊이 스케일링 단일 소스. tier 가중치 / 폭탄 수 / 기폭 시간 / 폭발 반경.
  모든 값 클램프 - 통과 불가 배치 방지 (blastMaxRadius x 2 < 복도 폭 유지 필수)
- ChoiceNode.cs: 구간 끝 선택지. W = 전진, E = 탈출. static Active = HUD 프롬프트 참조.
  IsExtractionLocked 델리게이트로 탈출 잠금 판정 주입 (S6)
- SegmentEnvironment.cs: 공간감 PCG 셸 (ADR-0003/0004). 평탄 보행로(y=0 유지) +
  측면 럽블 협곡 + 상부층(오버행 플랫폼/지지 기둥/브릿지 - 복층 느낌) + 데브리.
  에디트 모드 안전 (Destroy/DestroyImmediate 분기, sharedMaterial 틴트).
  난수는 주입된 rng만 사용. SegmentDefinition.wallHeight는 현재 미사용(구 셸 잔재)
- EnvironmentAuthoring.cs: 사전 배치 배경 마커. 커버 z 범위 내 런타임 배경 생성 스킵.
  생성은 에디터 윈도우 Scavenger > Environment Authoring (시드/구간 수 지정, 수동 다듬기 가능)

## 룩어헤드 체인 (ADR-0004)

- 다음 스테이지는 항상 미리 생성되어 확정 노출된다.
  BuildInitialChain(depth, z) = 현재+다음. 전진 선택 시 depth+2를 선생성.
  동시 생존 구간 최대 3개 (직전/현재/다음). TailEndZ가 체인 끝 기준점

## 스케일링 레퍼런스

- 배경 복셀 밀도를 레퍼런스 이미지 수준(수천 큐브)으로 올릴 때:
  https://github.com/Unity-Technologies/brg-shooter (BatchRendererGroup + Burst/Jobs
  인스턴싱, 저사양 모바일 대상 Unity 공식 데모). SegmentEnvironment를 데이터 생성기로
  유지하고 렌더만 BRG로 교체하는 경로 - GameObject 큐브는 그레이박스까지만

## 예정 (구현계획 v0.0.2)

- S6: SignalEmitter (거리 신호), ChoiceNode 탈출 잠금 연결

## 규칙

- 배치 난수는 RunManager.Rng만 사용 (시드 재현성)
- 생성/제거는 SegmentSpawner 내부에만. 외부에서 Instantiate/Destroy 금지
