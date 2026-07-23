# ADR-0008: 웹 프로토타입 이식과 HP 시스템 도입

- 날짜: 2026-07-23
- 상태: 승인
- 결정자: 사용자 (프로젝트 오너)
- 관련: ADR-0003 (카메라 - 이식 배제 근거), ADR-0006 (즉사/붕괴 - HP와 공존),
  ADR-0007 (이동 - 관성 보강)

## 배경

별도 웹 프로토타입(D:/NHN2/voxel-extraction-proto, HTML5 Canvas)이
같은 익스트랙션 컨셉의 조작감/기믹/속도감/카메라를 검증해 두었다.
사용자가 이를 정답지로 삼아 현재 Unity 프로젝트에 이식하기로 결정했다.
대상 4영역: 인터페이스 / 장애물 기믹 / 플레이 속도감 / 카메라 각도.

## 결정

### 이식한 것

1. HP 시스템 도입 (A안 - 회복 없음). 100 HP 순수 소모 자원. 즉사 요인
   (낙사/붕괴/직격)은 기존 Kill 유지, 신규 데미지형 장애물만 HP 감소.
   회복 수단은 두지 않는다 - 피해를 덜 맞는 회피 실력이 생존 축.
2. 속도감(관성): PlayerMotor에 지수 보간 가속(k = 1 - exp(-accel*dt*load))
   도입. 무게가 실릴수록 가속 반응 저하. 최대속도 4.2 -> 5.0.
3. 무게 페널티: 절대 임계 3단계 -> 무게 비율 기반 4경계 5단계
   (25/50/75/90% -> -7/-13/-23/-33%). maxCarryWeight 필드 도입.
4. 장애물 3종 (전부 데미지형, DangerGrid 예고로 회피 가능):
   바닥 장판(지속 26/s) / 미사일 폭격(반복, 예고 1.35s -> 40) /
   굴러오는 블록(정면 스폰, 35).
5. 등급 합성 (A안): 같은 id 5개 -> 상위 등급 1개. 무게 압축 + 가치 x6.
   스태시는 등급 미인식 - BankedCounts(합성 전 원본 개수)로 저장해
   아웃게임 창고 설계 불변.
6. HP HUD: 좌상단 체력 바 + 피격 붉은 화면 플래시.

### 이식하지 않은 것 (상위 설계가 이미 배제)

- 카메라 아이소메트릭(2:1): ADR-0003의 원근 3D 로우앵글이 상위 결정.
  웹보다 Unity 카메라(SmoothDamp + 벨트스크롤 래칫 + 트라우마 쉐이크)가 우위.
- 제한 시간 타이머 잠금: ADR-0006에서 붕괴 전선으로 대체 완료.
- 토글 이동 3종: ADR-0007에서 홀드 단일화.
- 퀵슬롯 5종(스캔/탐지기/손전등/신속이동/응급키트): 사용자 지시로 제거.
  응급키트 제거로 회복 수단이 없어지는 것은 A안(순수 소모)으로 수용.

## 결과

- Player/: PlayerHealth 신설 (Damage 진입점, 정적 ApplyDamage 테스트 대상).
  PlayerController에 Health 게터 + 리셋. PlayerMotor 관성 보간.
  CarryLoad 비율 5단계 재작성 (enum/시그니처 변경 - 테스트 갱신).
- Obstacle/: HazardFloor / StrikeZone / RollingBlock + RollingBlockSpawner 신설.
  전부 Health.Damage 경유, 산포 난수는 RunManager.Rng 시드 배정.
- Segment/: DepthCurve에 3종 수량 함수, SegmentSpawner에 3종 배치.
- Loot/: RunInventory 등급 합성 (Entry.Grade/Weight/Value, BankedCounts).
  RunSettlement.BankInventory가 BankedCounts 참조로 전환.
- UI/: HudController HP 바 + 피격 플래시, HudCanvasTemplate 배선.
- GameFlow: PlayerHealth 폴백 보강 (CarryLoad와 동일 패턴).
- EditMode 테스트 51개 전부 통과 (신규 등급합성 5 + 무게판정 재작성).

## 미결/후속

- 밸런스 세트 튜닝: 등급 합성의 무게 압축이 과적 압박을 약화시킨다
  (웹 검증 결론). C의 무게 배율과 G의 압축률은 플레이 확인 후 함께 재튜닝.
- HudCanvas 프리팹 재생성 필요: 템플릿 변경은 기존 프리팹에 자동 반영되지
  않는다 (규약: 최초 1회 생성). Scavenger > Setup Greybox Scene 재실행 또는
  수동 배선으로 HP 바/플래시 노출.
- 회복 수단(응급키트 등)은 A안 밸런스 확인 후 재검토 여지.
