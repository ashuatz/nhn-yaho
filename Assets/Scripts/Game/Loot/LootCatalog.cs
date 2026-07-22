using System.Collections.Generic;

namespace Scavenger.Loot
{
    /// <summary>
    /// 그레이박스 기본 루트 카탈로그. 에셋 없이도 런이 돌아가게 하는 코드 폴백.
    /// 정식 데이터는 ScriptableObject 에셋으로 승격 예정 (트랙 B 이후).
    /// </summary>
    public static class LootCatalog
    {
        /// <summary>tier 순으로 정렬된 기본 카탈로그를 생성한다.</summary>
        public static List<LootDefinition> CreateDefaults()
        {
            List<LootDefinition> catalog = new List<LootDefinition>
            {
                LootDefinition.Create("scrap_paper", "폐지 더미", 10, 1, 1.2f, 1f),
                LootDefinition.Create("scrap_metal", "고철", 30, 2, 2.5f, 4f),
                LootDefinition.Create("lockbox", "잠긴 금고", 90, 3, 4.5f, 9f),
            };

            return catalog;
        }
    }
}
