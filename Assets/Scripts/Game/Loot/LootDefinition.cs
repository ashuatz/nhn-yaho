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
        public int value = 10;
        public int tier = 1;

        [Header("루팅 홀드 시간 (초) = 정지 리스크")]
        public float holdSeconds = 1.5f;

        public static LootDefinition Create(string id, string displayName, int value, int tier, float holdSeconds)
        {
            LootDefinition definition = CreateInstance<LootDefinition>();
            definition.name = $"Loot ({id})";
            definition.id = id;
            definition.displayName = displayName;
            definition.value = value;
            definition.tier = tier;
            definition.holdSeconds = holdSeconds;
            return definition;
        }
    }
}
