# Loot/ - 파밍과 인벤토리

네임스페이스: Scavenger.Loot

## 파일 목차

- LootDefinition.cs: ScriptableObject (id/가치/tier/홀드 시간). id는 스태시 안정 키 - 배포 후 변경 금지
- LootCatalog.cs: 코드 폴백 카탈로그 3종 (폐지 t1 / 고철 t2 / 금고 t3). 정식 에셋 승격은 트랙 B 이후
- RunInventory.cs: 순수 클래스 (EditMode 테스트 대상). Add/Clear/TotalValue, id 기준 스택
- LootSpot.cs: 씬 배치물. E 홀드 루팅, 좌우 입력/홀드 해제 = 취소(진행도 리셋).
  static Active = 현재 루팅 중 스팟 (HUD 게이지 참조)
- PlayerStash.cs: 아웃게임 창고. 안정 ID 목록 저장 (가치 합계 아님 - 확장 대비).
  원자적 저장(tmp 후 교체), 손상 파일 .corrupt 격리 후 빈 창고 복구.
  경로 주입 가능 - EditMode 테스트 대상 (Assets/Tests/EditMode/PlayerStashTests.cs)

## 루팅 규칙 (ADR-0001)

- 루팅 시작 = PlayerController.TryBeginLoot (Advancing/Stopped에서만 성공)
- 루팅 중 이동 완전 정지. 취소 판정(좌우 입력)은 LootSpot이 소유
- 취소 시 진행도 보존 없음 - 고가치 = 긴 정지 리스크 유지
- 동시 루팅 불가 (Active 단일 슬롯)

