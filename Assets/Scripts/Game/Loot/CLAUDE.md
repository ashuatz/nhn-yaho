# Loot/ - 파밍과 인벤토리

네임스페이스: Scavenger.Loot

## 파일 목차

- LootDefinition.cs: ScriptableObject (id/가치/tier/홀드 시간/무게/shortDescription).
  id는 스태시 안정 키 - 배포 후 변경 금지. static TierColor(tier) = 표시 색 단일 소스
- LootCatalog.cs: 코드 폴백 카탈로그 3종 (폐지 t1 w1 / 고철 t2 w4 / 금고 t3 w9)
  + 한 줄 설명 (HUD 라벨용). 정식 에셋 승격은 트랙 B 이후
- RunInventory.cs: 순수 클래스 (EditMode 테스트 대상). Add/Clear/TotalValue/TotalWeight,
  (id, 등급) 기준 스택. Add(definition, value, weight) 오버로드 = 조각 지분 획득용
  (LootPickup). TotalWeight는 CarryLoad(Player/)의 과적 판정 입력 (M2-1).
  등급 합성 (ADR-0008 A안): 같은 (id, 등급) 5개 -> 상위 등급 1개 자동 합성(연쇄,
  최대 레어). Entry.Grade/Weight/Value 보유, 무게 1개분 압축 + 가치 x6 배수.
  스태시 저장은 등급 미인식 - BankedCounts(id별 실물 총 획득 개수, 합성 전 원본)를
  별도 누적해 RunSettlement.BankInventory가 참조 (아웃게임 창고 설계 불변)
- LootSpot.cs: 씬 배치물. E 홀드 루팅, 좌우 입력/홀드 해제 = 취소(진행도 리셋).
  static All = 라벨 순회용 레지스트리 / Active = 현재 루팅 중 스팟 (HUD 게이지 참조).
  static PromptTarget = 시작 가능 조건 충족 스팟 (HUD 우하단 키 프롬프트 참조).
  진행 중에도 시작 조건 재검증 (범위 이탈/requiredMinPlayerY 미달 시 취소 - 교차 검토).
  파밍 연출 (사용자 지시): 루팅 중 0.28초마다 LootBurst 파편, 완료 시 획득 대신
  ScatterPickups - 조각 3개(DropPieces)를 포물선으로 흩뿌린다. 가치/무게는 조각
  지분으로 분배 (합계 보존, 나머지는 앞 조각 - 런타임 SO 생성 없음).
  scatterRadiusMin/Max(단차 위는 좁게), scatterClampHalfWidth(복도 밖 착지 방지),
  scatterSeed(배치 시 RunManager.Rng에서 배정 - 산포는 게임 결과라 재현성 대상,
  Codex 교차 검토). 모두 스포너가 주입
- LootPickup.cs: 완료 시 튀어나오는 아이템 조각. Definition은 공유 참조 +
  PieceValue/PieceWeight 지분. 포물선 낙하(1회 바운스) 후 착지 - E 홀드
  (상호작용 공용 키)로 줍기, 줍는 시점에 RunInventory.Add(def, value, weight).
  방치 = 두고 간 가치. 반경 내 일괄 수거/루팅 중 수거는 의도된 UX.
  지지 스트립의 자식 - 바닥과 함께 침몰, y<-8 자체 정리. 비주얼 머티리얼은
  OnDestroy 해제. static All(라벨용) / PromptTarget(HUD 프롬프트).
  판정은 거리 기반 (콜라이더 없음)
- LootBurst.cs: 파밍/착탄 파편 연출 (큐브 칩, SinkDebris 계열). Spawn(origin, count, color).
  순수 비주얼 - 인스턴스 시드 난수, 공유 런타임 머티리얼, 자체 파괴
- PlayerStash.cs: 아웃게임 창고. 안정 ID 목록 저장 (가치 합계 아님 - 확장 대비).
  원자적 저장(tmp 후 교체), 손상 파일 .corrupt 격리 후 빈 창고 복구.
  경로 주입 가능 - EditMode 테스트 대상 (Assets/Tests/EditMode/PlayerStashTests.cs)

## 루팅 규칙 (ADR-0001 + 조각 드롭)

- 루팅 시작 = PlayerController.TryBeginLoot (Advancing에서만 성공)
- 루팅 중 이동 완전 정지. 취소 판정(좌우 입력)은 LootSpot이 소유
- 취소 시 진행도 보존 없음 - 고가치 = 긴 정지 리스크 유지
- 동시 루팅 불가 (Active 단일 슬롯)
- 완료 = 즉시 획득이 아니라 조각 낙하. 조각을 주워야 인벤토리 반영
  (줍기도 E 홀드 - 상호작용 키 단일화, 사용자 지시)

