using UnityEngine;

namespace Scavenger.Field
{
    /// <summary>
    /// 필드 규격 데이터 (필드 규칙 및 절차 문서 2.1 / 4.2 컬럼).
    /// 블록(1x1) - 존(25x7) - 필드(스테이지) 계층의 단일 소스.
    /// 스테이지 시트 값(존 개수, 바닥 제거 시작 거리, 흔들림 시간)과
    /// 존 시트 값(아이템 가치 예산, 파밍 포인트 개수)을 함께 보유한다.
    /// 스테이지별 분리는 프로토타입 이후 - 지금은 한 에셋이 전 스테이지 공통값.
    /// </summary>
    [CreateAssetMenu(menuName = "Scavenger/Zone Definition", fileName = "ZoneDefinition")]
    public sealed class ZoneDefinition : ScriptableObject
    {
        [Header("블록/존 규격 (문서 2.1 - 블록 1x1, 존 25 x 7)")]
        public float blockSize = 1f;
        public int zoneLengthBlocks = 25;
        public int zoneWidthBlocks = 7;

        [Header("스테이지: 존 개수 (min / max, 진입 시 랜덤 확정)")]
        public int zoneCountMin = 6;
        public int zoneCountMax = 9;

        [Header("바닥 제거 시작 거리 (캐릭터 뒤 몇 칸부터 제거 대상)")]
        public int floorRemoveStartBlocks = 6;

        [Header("흔들림 시간 (초). 제거 예정 상태 유지 시간 = 마지막 경고")]
        public float shakeSeconds = 1.2f;

        [Header("존의 아이템 가치 예산 (min / max, 드랍 아이템 생성량 조정)")]
        public int dropValueBudgetMin = 60;
        public int dropValueBudgetMax = 110;

        [Header("드랍 아이템 생성 간격 거리 (블록 단위 - 서로 붙지 않게)")]
        public float dropSpacingDistance = 2f;

        [Header("파밍 포인트 개수 (min / max, 존 시트 값)")]
        public int farmingPointCountMin = 1;
        public int farmingPointCountMax = 3;

        [Header("파밍 포인트 크기 (블록. 문서 규격 4 이상 ~ 7)")]
        [Range(4, 7)] public int farmingPointSizeBlocks = 5;

        [Header("파밍 포인트 간 최소 간격 (블록)")]
        public float farmingPointSpacingBlocks = 7f;

        [Header("파밍 포인트 흔들림 시작 거리 (블록. 소켓과 제거 기준선 거리)")]
        public float farmingPointShakeStartBlocks = 4f;

        /// <summary>존 길이 (m). 배경/생성 로직 공용 - 블록 수 x 블록 크기.</summary>
        public float lengthMeters
        {
            get { return zoneLengthBlocks * blockSize; }
        }

        /// <summary>존 반폭 (m). 이동 클램프와 배경 클리어런스 기준.</summary>
        public float corridorHalfWidth
        {
            get { return zoneWidthBlocks * blockSize * 0.5f; }
        }

        /// <summary>바닥 제거 기준선이 플레이어보다 뒤에 놓이는 거리 (m).</summary>
        public float FloorRemoveStartDistance
        {
            get { return floorRemoveStartBlocks * blockSize; }
        }

        // 튜닝 실수 가드: 역전된 min/max와 0 규격 방지
        void OnValidate()
        {
            blockSize = Mathf.Max(0.1f, blockSize);
            zoneLengthBlocks = Mathf.Max(1, zoneLengthBlocks);
            zoneWidthBlocks = Mathf.Max(1, zoneWidthBlocks);

            zoneCountMin = Mathf.Max(1, zoneCountMin);
            zoneCountMax = Mathf.Max(zoneCountMin, zoneCountMax);

            floorRemoveStartBlocks = Mathf.Max(1, floorRemoveStartBlocks);
            shakeSeconds = Mathf.Max(0f, shakeSeconds);

            dropValueBudgetMin = Mathf.Max(0, dropValueBudgetMin);
            dropValueBudgetMax = Mathf.Max(dropValueBudgetMin, dropValueBudgetMax);
            dropSpacingDistance = Mathf.Max(0f, dropSpacingDistance);

            farmingPointCountMin = Mathf.Max(0, farmingPointCountMin);
            farmingPointCountMax = Mathf.Max(farmingPointCountMin, farmingPointCountMax);
            farmingPointSpacingBlocks = Mathf.Max(0f, farmingPointSpacingBlocks);
            farmingPointShakeStartBlocks = Mathf.Max(0f, farmingPointShakeStartBlocks);
        }

        /// <summary>존에 배치할 파밍 포인트 개수를 확정한다 (시드 기반).</summary>
        public int RollFarmingPointCount(System.Random rng)
        {
            if (rng == null)
                return farmingPointCountMin;

            return rng.Next(farmingPointCountMin, farmingPointCountMax + 1);
        }

        /// <summary>파밍 포인트 크기 (m).</summary>
        public float FarmingPointSize
        {
            get { return farmingPointSizeBlocks * blockSize; }
        }

        /// <summary>스테이지 진입 시 존 개수를 확정한다 (시드 기반 - 재현성).</summary>
        public int RollZoneCount(System.Random rng)
        {
            if (rng == null)
                return zoneCountMin;

            return rng.Next(zoneCountMin, zoneCountMax + 1);
        }

        /// <summary>존 하나에 채울 아이템 가치 예산을 확정한다.</summary>
        public int RollDropBudget(System.Random rng)
        {
            if (rng == null)
                return dropValueBudgetMin;

            return rng.Next(dropValueBudgetMin, dropValueBudgetMax + 1);
        }

        public static ZoneDefinition CreateDefault()
        {
            ZoneDefinition definition = CreateInstance<ZoneDefinition>();
            definition.name = "ZoneDefinition (Default)";
            return definition;
        }
    }
}
