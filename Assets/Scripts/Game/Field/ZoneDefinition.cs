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

        [Header("존과 존 사이 구간 길이 (블록). 웨이포인트가 놓이는 구간")]
        public int junctionLengthBlocks = 8;

        [Header("전방 미리 생성 존 개수 (패딩). 생성 장면이 화면에 잘려 보이지 않게")]
        [Range(1, 4)] public int zoneLookAheadCount = 2;

        [Header("바닥 제거 시작 거리 (캐릭터 뒤 몇 칸부터 제거 대상)")]
        public int floorRemoveStartBlocks = 6;

        [Header("흔들림 시간 (초). 제거 예정 상태 유지 시간 = 마지막 경고")]
        public float shakeSeconds = 1.2f;

        [Header("존의 아이템 가치 예산 (min / max, 드랍 아이템 생성량 조정)")]
        public int dropValueBudgetMin = 60;
        public int dropValueBudgetMax = 110;

        [Header("드랍 아이템 생성 간격 거리 (블록 단위 - 서로 붙지 않게)")]
        public float dropSpacingDistance = 2f;

        // min 2 = 배분 규칙(1번째 상단 / 2번째 하단)에 따라 존마다 상단과 하단이
        // 하나씩 생겨 화면에 3층이 동시에 읽힌다 (3층 구조 계획 2.1의 목표).
        // 구역이 길어져(입구 + 본체 + 출구) 25블록 존에는 최소 간격을 지켜 2개까지 들어간다 -
        // 3개 이상을 넣으려면 존 길이나 최소 간격을 함께 조정할 것
        [Header("파밍 포인트 개수 (min / max, 존 시트 값)")]
        public int farmingPointCountMin = 2;
        public int farmingPointCountMax = 2;

        [Header("파밍 포인트 플랫폼 깊이 (블록. 존 밖으로 뻗는 x 길이, 문서 규격 4~7)")]
        [Range(4, 7)] public int farmingPointDepthBlocks = 5;

        [Header("파밍 포인트 길이 (블록. z 방향 - 입구/출구가 나뉘어 조금 더 길다)")]
        public int farmingPointLengthBlocks = 9;

        [Header("입구/출구 게이트 길이 (블록. 계단이 놓이는 z 구간, 앞뒤 각각)")]
        public int farmingPointGateBlocks = 2;

        [Header("파밍 포인트 단차 (블록. 상단은 +y / 하단은 -y - 3층 구조)")]
        public float farmingPointRiseBlocks = 1f;

        [Header("계단 길이 (블록. x 방향 - 단차를 오르내리는 구간)")]
        public float farmingPointStairBlocks = 2f;

        [Header("파밍 포인트 간 최소 간격 (블록)")]
        public float farmingPointSpacingBlocks = 10f;

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

        /// <summary>존과 존 사이 구간 길이 (m). 웨이포인트가 놓이는 세그먼트.</summary>
        public float JunctionLengthMeters
        {
            get { return junctionLengthBlocks * blockSize; }
        }

        /// <summary>전방 미리 생성 거리 (m). 플레이어 앞으로 확보할 바닥 패딩.</summary>
        public float LookAheadDistance
        {
            get { return zoneLookAheadCount * lengthMeters; }
        }

        // 튜닝 실수 가드: 역전된 min/max와 0 규격 방지
        void OnValidate()
        {
            blockSize = Mathf.Max(0.1f, blockSize);
            zoneLengthBlocks = Mathf.Max(1, zoneLengthBlocks);
            zoneWidthBlocks = Mathf.Max(1, zoneWidthBlocks);

            zoneCountMin = Mathf.Max(1, zoneCountMin);
            zoneCountMax = Mathf.Max(zoneCountMin, zoneCountMax);

            junctionLengthBlocks = Mathf.Max(1, junctionLengthBlocks);
            zoneLookAheadCount = Mathf.Max(1, zoneLookAheadCount);

            floorRemoveStartBlocks = Mathf.Max(1, floorRemoveStartBlocks);
            shakeSeconds = Mathf.Max(0f, shakeSeconds);

            dropValueBudgetMin = Mathf.Max(0, dropValueBudgetMin);
            dropValueBudgetMax = Mathf.Max(dropValueBudgetMin, dropValueBudgetMax);
            dropSpacingDistance = Mathf.Max(0f, dropSpacingDistance);

            farmingPointCountMin = Mathf.Max(0, farmingPointCountMin);
            farmingPointCountMax = Mathf.Max(farmingPointCountMin, farmingPointCountMax);
            farmingPointSpacingBlocks = Mathf.Max(0f, farmingPointSpacingBlocks);
            farmingPointShakeStartBlocks = Mathf.Max(0f, farmingPointShakeStartBlocks);

            // 입구/출구가 겹치면 통과 동선이 사라진다 - 게이트 2개 + 본체 1블록 이상
            farmingPointGateBlocks = Mathf.Max(1, farmingPointGateBlocks);
            farmingPointLengthBlocks = Mathf.Max(
                farmingPointGateBlocks * 2 + 1, farmingPointLengthBlocks);

            farmingPointRiseBlocks = Mathf.Max(0f, farmingPointRiseBlocks);
            farmingPointStairBlocks = Mathf.Max(0.5f, farmingPointStairBlocks);
        }

        /// <summary>존에 배치할 파밍 포인트 개수를 확정한다 (시드 기반).</summary>
        public int RollFarmingPointCount(System.Random rng)
        {
            if (rng == null)
                return farmingPointCountMin;

            return rng.Next(farmingPointCountMin, farmingPointCountMax + 1);
        }

        /// <summary>파밍 포인트 플랫폼 깊이 (m. 계단을 제외한 평면 구간).</summary>
        public float FarmingPointDepth
        {
            get { return farmingPointDepthBlocks * blockSize; }
        }

        /// <summary>파밍 포인트 길이 (m. z 방향 - 입구 게이트 + 본체 + 출구 게이트).</summary>
        public float FarmingPointLength
        {
            get { return farmingPointLengthBlocks * blockSize; }
        }

        /// <summary>입구/출구 게이트 길이 (m. 계단이 놓이는 z 구간).</summary>
        public float FarmingPointGateLength
        {
            get { return farmingPointGateBlocks * blockSize; }
        }

        /// <summary>파밍 포인트 단차 (m. 부호 없는 크기 - 방향은 타입이 정한다).</summary>
        public float FarmingPointRise
        {
            get { return farmingPointRiseBlocks * blockSize; }
        }

        /// <summary>계단 길이 (m. 존 가장자리에서 플랫폼까지의 x 거리).</summary>
        public float FarmingPointStairLength
        {
            get { return farmingPointStairBlocks * blockSize; }
        }

        /// <summary>파밍 포인트 전체 깊이 (m. 계단 + 플랫폼 - 안전지대 x 범위).</summary>
        public float FarmingPointTotalDepth
        {
            get { return FarmingPointStairLength + FarmingPointDepth; }
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
