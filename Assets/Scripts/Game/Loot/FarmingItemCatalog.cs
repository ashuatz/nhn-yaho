using System.Collections.Generic;
using UnityEngine;

namespace Scavenger.Loot
{
    /// <summary>
    /// 파밍 아이템 기본 카탈로그 (파밍 문서 4장). 드랍 아이템(길바닥, 자동 수집)과
    /// 다른 타입이며 가치가 높다 - 아이템 오브젝트를 열어야 나온다.
    ///
    /// 드랍 카탈로그와 같은 코드 폴백 자리다 (정식 데이터는 에셋 승격 예정).
    /// 합성 필요 개수 N은 파밍 아이템만 3이다 (문서 4.2 - 전역 기본값 5를 덮어쓴다).
    ///
    /// 아이템 그룹(문서 3.2)은 지금 표가 없으므로 **오브젝트 등급 -> 아이템 등급 확률**
    /// 한 단계로 줄여 둔다. 그룹 테이블이 들어오면 Draw만 교체하면 된다.
    /// </summary>
    public static class FarmingItemCatalog
    {
        /// <summary>파밍 아이템의 합성 필요 개수 (문서 4.2).</summary>
        public const int FarmingMergeCount = 3;

        // 오브젝트 등급(일반/희귀/영웅)별 아이템 등급(일반/희귀/영웅/전설) 확률.
        // 전설은 별도 오브젝트를 만들지 않고 영웅 오브젝트에 섞는다 (문서 2.3)
        static readonly float[][] TierChanceByObjectGrade =
        {
            new[] { 0.70f, 0.25f, 0.05f, 0f },
            new[] { 0.30f, 0.45f, 0.22f, 0.03f },
            new[] { 0.05f, 0.30f, 0.50f, 0.15f },
        };

        /// <summary>등급 1..4 순으로 정렬된 기본 파밍 아이템 목록.</summary>
        public static List<LootDefinition> CreateDefaults()
        {
            List<LootDefinition> catalog = new List<LootDefinition>
            {
                Build(
                    "farm_bowl", "골동 그릇", value: 60, tier: 1,
                    weight: 3f, "흔하지만 길바닥 폐지와는 값이 다르다",
                    sortPriority: 40, compress1: 2.7f, compress2: 2.4f, compressSeconds: 0.5f),
                Build(
                    "farm_watch", "멈춘 손목시계", value: 150, tier: 2,
                    weight: 2f, "가볍고 값이 나가는 수집품",
                    sortPriority: 50, compress1: 1.8f, compress2: 1.6f, compressSeconds: 0.5f),
                Build(
                    "farm_relic", "유물 조각", value: 400, tier: 3,
                    weight: 6f, "무겁지만 한 개로 런이 바뀐다",
                    sortPriority: 60, compress1: 5.4f, compress2: 4.8f, compressSeconds: 0.9f),
                Build(
                    "farm_crown", "도금 왕관", value: 1000, tier: 4,
                    weight: 9f, "영웅 오브젝트에서만 드물게 나온다",
                    sortPriority: 70, compress1: 8.1f, compress2: 7.2f, compressSeconds: 1.3f),
            };

            return catalog;
        }

        /// <summary>
        /// 오브젝트 등급으로 파밍 아이템 하나를 뽑는다 (문서 3.2 아이템 추첨 규칙 -
        /// 확률 합산 안에서 하나, 결과는 항상 1개).
        /// </summary>
        /// <param name="objectGrade">0 = 일반 / 1 = 희귀 / 2 = 영웅.</param>
        public static LootDefinition Draw(
            List<LootDefinition> catalog, int objectGrade, System.Random rng)
        {
            if (catalog == null || catalog.Count == 0)
                return null;

            if (rng == null)
                return catalog[0];

            float[] tierChance = ResolveTierChance(objectGrade);
            float total = 0f;

            foreach (LootDefinition definition in catalog)
                total += ResolveChance(tierChance, definition);

            // 확률이 전부 0인 구성 - 목록에서 균등하게 하나
            if (total <= 0f)
                return catalog[rng.Next(0, catalog.Count)];

            float target = (float)rng.NextDouble() * total;
            float accumulated = 0f;

            foreach (LootDefinition definition in catalog)
            {
                accumulated += ResolveChance(tierChance, definition);

                if (target <= accumulated)
                    return definition;
            }

            // 부동소수 잔차로 끝을 넘긴 경우
            return catalog[catalog.Count - 1];
        }

        static float[] ResolveTierChance(int objectGrade)
        {
            int index = Mathf.Clamp(objectGrade, 0, TierChanceByObjectGrade.Length - 1);

            return TierChanceByObjectGrade[index];
        }

        static float ResolveChance(float[] tierChance, LootDefinition definition)
        {
            if (definition == null)
                return 0f;

            int index = Mathf.Clamp(definition.tier - 1, 0, tierChance.Length - 1);

            return Mathf.Max(0f, tierChance[index]);
        }

        static LootDefinition Build(
            string id, string displayName, int value, int tier,
            float weight, string shortDescription,
            int sortPriority, float compress1, float compress2, float compressSeconds)
        {
            // holdSeconds는 길바닥 루팅용 값이라 파밍 아이템에는 쓰이지 않는다
            // (상호작용 시간은 아이템이 아니라 오브젝트 등급이 정한다 - 문서 3.3 (2))
            LootDefinition definition = LootDefinition.Create(
                id, displayName, value, tier, holdSeconds: 0f, weight, shortDescription);

            definition.sortPriority = sortPriority;
            definition.compressedWeight1 = compress1;
            definition.compressedWeight2 = compress2;
            definition.compressSeconds = compressSeconds;
            definition.mergeCount = FarmingMergeCount;

            return definition;
        }
    }
}
