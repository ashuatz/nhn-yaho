# ADR-0005: 배경 렌더링을 Graphics.RenderMeshInstanced로 전환

- 날짜: 2026-07-21
- 상태: 승인
- 결정자: 사용자 (프로젝트 오너)
- 관련: ADR-0004 (복층 배경, 사전 배치)

## 배경

배경 스케일링 후보였던 BatchRendererGroup은 웹 빌드에서 사용 불가
(WebGL2는 지원 플랫폼 목록에 없고 SSBO 미지원, WebGPU는 실험 단계).
웹 타깃 가능성을 고려해 사용자가 Graphics.RenderMeshInstanced 사용을 결정했다.

## 결정

1. 배경 블록(럽블/상부층/데브리)은 GameObject 대신 인스턴스 데이터
   (행렬 + 팔레트 인덱스)로 생성하고 Graphics.RenderMeshInstanced로 그린다.
   - 색은 연속 지터 대신 팔레트 6색으로 양자화 (팔레트별 머티리얼 1개,
     per-instance 색 셰이더 없이 URP Lit 그대로 사용).
   - 보행로 바닥만 GameObject 유지 (콜라이더 필요). 배경 블록 콜라이더는 제거
     (좌우 이동이 사전 클램프라 게임플레이에 불필요).
2. SegmentEnvironment는 데이터 생성기 (GenerateBlocks)로 분리.
   - 런타임: EnvironmentRenderer가 구간 청크 단위로 등록/해제하며 매 프레임 드로우.
   - 사전 배치(수동 편집) 경로: 기존 GameObject 백엔드(BuildGameObjects) 유지 -
     손으로 다듬는 용도라 인스턴싱 대상이 아니며, 유한 범위라 비용 허용.

## 결과

- 웹(WebGL2) 호환. 인스턴싱은 WebGL2에서 지원됨.
- 구간당 드로우: 팔레트 수(6) x 청크 수(최대 3) 수준으로 감소.
- 배경 밀도를 올려도 GameObject 생성/파괴 비용 없음 (배열 교체만).
- brg-shooter의 셀 애니메이션 같은 연출은 필요 시 행렬 갱신으로 구현 가능
  (BRG 전환은 웹 포기 또는 WebGPU 정식화 시 재검토).
