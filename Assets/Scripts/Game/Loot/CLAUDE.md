# Loot/ - 파밍과 인벤토리

네임스페이스: Scavenger.Loot

## 파일 목차

- LootDefinition.cs: 아이템 정의 SO = 드랍 문서 9.1 아이템 정의 컬럼의 코드 대응물.
  id(스태시 안정 키 - 배포 후 변경 금지) / displayName / shortDescription /
  value(가치 = 존 예산 소비량) / tier(**등급 1~4 = 일반·희귀·영웅·전설**) /
  holdSeconds / weight / sortPriority(가방 정렬·압축 순서) /
  compressedWeight1·2(압축 무게) / compressSeconds / mergeCount(합성 N, 0 = 전역 기본) /
  spawnZones(등장 존, 비우면 전체).
  헬퍼: CanSpawnInZone / CompressedWeight(step) / MaxCompressStep /
  static GradeColor(등급 색 단일 소스 - 흰/파랑/보라/빨강) / GradeName / MaxTier(4).
  tier는 "종류 구분"이 아니라 아이템 등급이며 합성의 시작 등급이다
- BagDefinition.cs: 가방 규격 SO = 가방 문서 7.1 가방 컬럼.
  maxWeight / slotCountDefault(0 = 무제한) / slotCountMax / mergeCountDefault(전역 N).
  GameFlow가 CarryLoad(무게)와 RunInventory(슬롯·N)에 배선한다 - 두 판정이
  같은 값을 보게 하는 것이 목적. CreateDefault 폴백
- FarmingItemCatalog.cs: 파밍 아이템 카탈로그 (파밍 문서 4장). 드랍과 다른 타입 -
  아이템 오브젝트를 열어야 나오는 고가치 4종 (골동 그릇 t1 60 / 손목시계 t2 150 /
  유물 조각 t3 400 / 도금 왕관 t4 1000). 합성 필요 개수 N = 3 (전역 기본 5를 덮어쓴다).
  Draw(카탈로그, 오브젝트 등급, rng) = 등급별 아이템 등급 확률로 1개 추첨
  (문서 3.2 아이템 그룹 표가 들어오면 여기만 교체한다).
  GameFlow가 만들어 FieldSpawner.Configure로 주입한다
- LootCatalog.cs: 코드 폴백 카탈로그 3종 (폐지 t1 w1 / 고철 t2 w4 / 금고 t3 w9)
  + 한 줄 설명 + 압축/정렬 임시값. 정식 에셋 승격은 트랙 B 이후
- RunInventory.cs: 순수 클래스 (EditMode 테스트 대상). Add/Clear/TotalValue/TotalWeight,
  (id, 등급) 기준 스택. Add(definition, value, weight) 오버로드 = 조각 지분 획득용
  (LootPickup). TotalWeight는 CarryLoad(Player/)의 과적 판정 입력 (M2-1).
  등급 합성 (드랍 3장 / 파밍 4.2): 같은 (id, 등급) N개 -> 상위 등급 1개 자동 합성
  (연쇄, **전설(4)에서 정지**). 시작 등급 = 아이템 tier.
  N = 아이템 mergeCount, 0이면 Configure로 받은 전역 기본값 (기본 5).
  Entry.Grade(1~4)/Weight/Value 보유, 무게 1개분 압축 + 가치 x6 배수.
  Configure(slotCapacity, mergeCountDefault) / HasSlotFor(definition[, grade]) / UsedSlots =
  슬롯 한도 (가방 2.1).
  버리기 (사용자 지시 2026-07-26, 드래그앤드롭): TryDropOne(slot) = 스택에서 1개분
  (무게/가치/등급)을 떼어 DroppedItem으로 넘긴다. Restore(item) = 그 몫 그대로 되담기
  - **시작 등급으로 되돌리지 않는다** (합성해 둔 등급을 버렸다 줍는 것만으로 잃으면 안 된다).
  BankedCounts도 버릴 때 되돌린다(UnbankOne) - 안 그러면 버리고 줍기를 반복해
  창고 개수를 부풀릴 수 있다. 슬롯 하나 = (id, 등급) 스택 하나이므로 같은 칸에 쌓이는
  획득은 슬롯을 쓰지 않는다. 무게 초과 판정은 CarryLoad 소유 - 여기선 슬롯만 본다.
  스태시 저장은 등급 미인식 - BankedCounts(id별 실물 총 획득 개수, 합성 전 원본)를
  별도 누적해 RunSettlement.BankInventory가 참조 (아웃게임 창고 설계 불변)
- LootSpot.cs: 씬 배치물. 획득 거리 안에 들어오면 자동 수집.
  InitializeDropped(가방에서 버린 몫) = 등급/무게/가치를 들고 있다가 다시 주우면
  Restore로 되돌린다. 버린 직후에는 잠겨 있고(armed=false) **플레이어가 수집 반경을
  한 번 벗어나야** 열린다 - 발밑에서 즉시 되빨리면 버리기가 성립하지 않는다.
  획득 제한 2조건 (가방 5장 / 드랍 6.3): 무게 초과 OR 슬롯 초과면 바닥에 남는다.
  획득 거리는 PlayerController.itemCollectDistance(플레이어 옵션 컬럼)가 정본이고
  스포너가 주입한다. E 홀드 루팅, 좌우 입력/홀드 해제 = 취소(진행도 리셋).
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
  PieceValue/PieceWeight 지분. 포물선 낙하(1회 바운스, 비행 중 tier색 트레일 +
  비주얼 자식만 자기 중심 회전 - 루트 회전은 궤도 왜곡) 후 착지 - E 홀드
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

