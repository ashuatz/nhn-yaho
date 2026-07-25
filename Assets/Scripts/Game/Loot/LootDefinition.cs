using System.Collections.Generic;
using UnityEngine;

namespace Scavenger.Loot
{
    /// <summary>
    /// 아이템 정의 데이터 (드랍 문서 9.1 아이템 정의 컬럼 + 파밍 문서 4.4).
    /// 가치가 클수록 홀드 시간이 길다 = 더 긴 정지 = 더 큰 리스크 (ADR-0001).
    /// id는 스태시 저장의 안정 키이므로 한번 배포되면 변경 금지.
    ///
    /// 등급(tier)은 문서의 아이템 등급과 같은 축이다 (1=일반 / 2=희귀 / 3=영웅 / 4=전설).
    /// 인벤토리에서 같은 등급이 N개 모이면 다음 등급으로 합성되며, 시작 등급이 이 값이다.
    /// </summary>
    [CreateAssetMenu(menuName = "Scavenger/Loot Definition", fileName = "LootDefinition")]
    public sealed class LootDefinition : ScriptableObject
    {
        /// <summary>등급 상한 (전설). 합성은 여기서 멈춘다.</summary>
        public const int MaxTier = 4;

        [Header("스태시 저장 키. 배포 후 변경 금지")]
        public string id = "scrap_paper";

        public string displayName = "폐지";

        [Header("HUD 아이템 라벨용 한 줄 설명 (M5-1)")]
        public string shortDescription = "";

        [Header("아이템 가치 값 - 존 예산 소비량 (드랍 문서 5.2 (3))")]
        public int value = 10;

        [Header("아이템 등급 (1=일반 / 2=희귀 / 3=영웅 / 4=전설). 합성의 시작 등급")]
        [Range(1, MaxTier)] public int tier = 1;

        [Header("루팅 홀드 시간 (초) = 정지 리스크")]
        public float holdSeconds = 1.5f;

        [Header("무게 - 과적 이동속도 판정의 입력 (M2-1)")]
        public float weight = 1f;

        [Header("가방 정렬 순서 (클수록 앞. 압축도 이 순서 - 가방 문서 6.2)")]
        public int sortPriority;

        [Header("압축 무게 (가방 문서 6.1). 0이면 그 단계 압축 불가")]
        public float compressedWeight1;
        public float compressedWeight2;

        [Header("1개를 압축하는 데 걸리는 시간 (초)")]
        public float compressSeconds = 0.5f;

        [Header("합성 필요 개수 (N). 0이면 전역 기본값 (파밍 문서 4.2)")]
        public int mergeCount;

        [Header("등장 존 (스테이지 내 존 순번, 0부터). 비우면 모든 존")]
        public List<int> spawnZones = new List<int>();

        /// <summary>
        /// 이 아이템이 해당 존에 등장할 수 있는가 (드랍 문서 5.2 (4)).
        /// 등장 존 목록이 비어 있으면 제한 없음.
        /// </summary>
        public bool CanSpawnInZone(int zoneIndexInStage)
        {
            if (spawnZones == null || spawnZones.Count == 0)
                return true;

            return spawnZones.Contains(zoneIndexInStage);
        }

        /// <summary>
        /// 압축 단계별 무게 (가방 문서 6.1). step 0 = 원본.
        /// 해당 단계 값이 0이면 압축 불가이므로 이전 단계 무게를 유지한다.
        /// </summary>
        public float CompressedWeight(int step)
        {
            if (step >= 2 && compressedWeight2 > 0f)
                return compressedWeight2;

            if (step >= 1 && compressedWeight1 > 0f)
                return compressedWeight1;

            return weight;
        }

        /// <summary>압축 가능한 최대 단계 (0 = 압축 불가 / 1 / 2).</summary>
        public int MaxCompressStep
        {
            get
            {
                if (compressedWeight2 > 0f)
                    return 2;

                if (compressedWeight1 > 0f)
                    return 1;

                return 0;
            }
        }

        /// <summary>
        /// 등급 표시 색 (드랍 문서 2.2 / 파밍 문서 4.2 - 일반 흰 / 희귀 파랑 /
        /// 영웅 보라 / 전설 빨강). 가방 외곽선과 월드 표시물의 단일 소스.
        /// </summary>
        public static Color GradeColor(int grade)
        {
            if (grade >= 4)
                return new Color(1f, 0.27f, 0.27f);

            if (grade == 3)
                return new Color(0.6f, 0.4f, 0.8f);

            if (grade == 2)
                return new Color(0.27f, 0.53f, 1f);

            return new Color(0.88f, 0.88f, 0.85f);
        }

        /// <summary>등급 이름 (HUD 라벨/디버그 공용).</summary>
        public static string GradeName(int grade)
        {
            if (grade >= 4)
                return "전설";

            if (grade == 3)
                return "영웅";

            if (grade == 2)
                return "희귀";

            return "일반";
        }

        // 튜닝 실수 가드: 압축 무게가 원본보다 크면 압축이 아니고,
        // 2회 압축이 1회보다 무거우면 단계가 역전된다
        void OnValidate()
        {
            tier = Mathf.Clamp(tier, 1, MaxTier);
            weight = Mathf.Max(0f, weight);
            mergeCount = Mathf.Max(0, mergeCount);
            compressSeconds = Mathf.Max(0f, compressSeconds);

            if (compressedWeight1 > 0f)
                compressedWeight1 = Mathf.Min(compressedWeight1, weight);

            if (compressedWeight2 > 0f)
                compressedWeight2 = Mathf.Min(compressedWeight2, CompressedWeight(1));
        }

        public static LootDefinition Create(
            string id, string displayName, int value, int tier, float holdSeconds,
            float weight = 1f, string shortDescription = "")
        {
            LootDefinition definition = CreateInstance<LootDefinition>();
            definition.name = $"Loot ({id})";
            definition.id = id;
            definition.displayName = displayName;
            definition.shortDescription = shortDescription;
            definition.value = value;
            definition.tier = Mathf.Clamp(tier, 1, MaxTier);
            definition.holdSeconds = holdSeconds;
            definition.weight = weight;
            return definition;
        }
    }
}
