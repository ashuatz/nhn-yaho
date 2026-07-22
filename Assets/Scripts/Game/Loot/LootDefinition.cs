using UnityEngine;

namespace Scavenger.Loot
{
    /// <summary>
    /// 파밍 요소 데이터. 가치가 클수록 홀드 시간이 길다 = 더 긴 정지 = 더 큰 리스크 (ADR-0001).
    /// id는 스태시 저장의 안정 키이므로 한번 배포되면 변경 금지.
    /// </summary>
    [CreateAssetMenu(menuName = "Scavenger/Loot Definition", fileName = "LootDefinition")]
    public sealed class LootDefinition : ScriptableObject
    {
        [Header("스태시 저장 키. 배포 후 변경 금지")]
        public string id = "scrap_paper";

        public string displayName = "폐지";

        [Header("HUD 아이템 라벨용 한 줄 설명 (M5-1)")]
        public string shortDescription = "";

        public int value = 10;
        public int tier = 1;

        [Header("루팅 홀드 시간 (초) = 정지 리스크")]
        public float holdSeconds = 1.5f;

        [Header("무게 - 과적 이동속도 판정의 입력 (M2-1)")]
        public float weight = 1f;

        /// <summary>tier별 그레이박스 표시 색 (스팟/조각/라벨 공용 단일 소스).</summary>
        public static Color TierColor(int tier)
        {
            if (tier >= 3)
                return new Color(0.95f, 0.8f, 0.2f);

            if (tier == 2)
                return new Color(0.6f, 0.7f, 0.85f);

            return new Color(0.7f, 0.55f, 0.3f);
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
            definition.tier = tier;
            definition.holdSeconds = holdSeconds;
            definition.weight = weight;
            return definition;
        }
    }
}
