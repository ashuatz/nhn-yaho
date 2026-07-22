using System.Collections.Generic;
using UnityEngine;

namespace Scavenger.Loot
{
    /// <summary>
    /// 아이템 아이콘 스프라이트 런타임 접근 (웹 이식, ADR-0008). id별 스프라이트를
    /// Resources/LootIcons/에서 로드해 캐시한다. 에셋은 에디터에서 LootIconMaker가
    /// 미리 생성(Scavenger > Generate Loot Icons). 없으면 null - 호출부가 색 폴백.
    /// </summary>
    public static class LootIcons
    {
        const string ResourcePath = "LootIcons/";

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>id에 해당하는 아이콘 스프라이트. 없으면 null (색 폴백은 호출부).</summary>
        public static Sprite Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            if (cache.TryGetValue(id, out Sprite cached))
                return cached;

            Sprite sprite = Resources.Load<Sprite>(ResourcePath + id);

            // null도 캐시 - 매 프레임 Resources.Load 반복 방지
            cache[id] = sprite;

            return sprite;
        }
    }
}
