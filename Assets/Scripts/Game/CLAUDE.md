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
| Obstacle/ | Scavenger.Obstacle | (S4) 폭탄 |
| UI/ | Scavenger.UI | (S3+) 플레이어 HUD |

## 루트 파일

- GameBootstrap.cs: 빈 씬에서 전체 시스템을 코드로 조립. R키 = 런 재시작.
  씬에는 이 컴포넌트 하나만 배치하면 된다.

## 전역 규칙

- 숨김 타이머 실수치는 Diagnostics 대시보드에만 노출. 플레이어 HUD 노출 금지.
- 난수는 반드시 RunManager.Rng (시드 기반) 사용. UnityEngine.Random 금지 (재현성).
- 이모지/유니코드 특수문자 금지 (프로젝트 규약).
