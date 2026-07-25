using System.Collections.Generic;

namespace Scavenger.Loot
{
    /// <summary>
    /// 그레이박스 기본 루트 카탈로그. 에셋 없이도 런이 돌아가게 하는 코드 폴백.
    /// 정식 데이터는 ScriptableObject 에셋으로 승격 예정 (트랙 B 이후).
    ///
    /// 압축 무게/압축 시간/정렬 순서는 드랍 문서 9.1 컬럼에 대응하는 임시 값이다.
    /// 밸런스 값이 정해지면 에셋으로 옮긴다 - 여기 값은 시스템 검증용 자리표시.
    /// </summary>
    public static class LootCatalog
    {
        /// <summary>등급(tier) 순으로 정렬된 기본 카탈로그를 생성한다.</summary>
        public static List<LootDefinition> CreateDefaults()
        {
            List<LootDefinition> catalog = new List<LootDefinition>
            {
                Build(
                    "scrap_paper", "폐지 더미", value: 10, tier: 1, holdSeconds: 1.2f,
                    weight: 1f, "가볍고 흔한 저가치 재활용지",
                    sortPriority: 10, compress1: 0.9f, compress2: 0.8f, compressSeconds: 0.35f),
                Build(
                    "scrap_metal", "고철", value: 30, tier: 2, holdSeconds: 2.5f,
                    weight: 4f, "무겁지만 값이 나가는 금속 조각",
                    sortPriority: 20, compress1: 3.6f, compress2: 3.2f, compressSeconds: 0.6f),
                Build(
                    "lockbox", "잠긴 금고", value: 90, tier: 3, holdSeconds: 4.5f,
                    weight: 9f, "매우 무겁고 고가치 - 오래 뒤져야 한다",
                    sortPriority: 30, compress1: 8.1f, compress2: 7.2f, compressSeconds: 1.1f),
            };

            return catalog;
        }

        // 압축 컬럼은 Create의 인자가 아니라 필드로 채운다 -
        // Create는 기존 호출부(테스트 포함)와 인자 순서를 유지해야 한다
        static LootDefinition Build(
            string id, string displayName, int value, int tier, float holdSeconds,
            float weight, string shortDescription,
            int sortPriority, float compress1, float compress2, float compressSeconds)
        {
            LootDefinition definition = LootDefinition.Create(
                id, displayName, value, tier, holdSeconds, weight, shortDescription);

            definition.sortPriority = sortPriority;
            definition.compressedWeight1 = compress1;
            definition.compressedWeight2 = compress2;
            definition.compressSeconds = compressSeconds;

            return definition;
        }
    }
}
