using UnityEngine;

namespace Scavenger.Loot
{
    /// <summary>
    /// 가방 규격 데이터 (가방과 무게 문서 7.1 가방 컬럼 + 드랍 문서 9.4 전역 설정).
    /// 최대 무게와 슬롯 한도의 단일 소스이며, GameFlow가 CarryLoad(무게 판정)와
    /// RunInventory(슬롯 판정)에 배선한다.
    ///
    /// 슬롯 하나 = (아이템 id, 등급) 스택 하나. 같은 종류 + 같은 등급은 한 칸에
    /// 개수로 쌓이므로 슬롯을 더 쓰지 않는다 (가방 문서 2.2).
    /// </summary>
    [CreateAssetMenu(menuName = "Scavenger/Bag Definition", fileName = "BagDefinition")]
    public sealed class BagDefinition : ScriptableObject
    {
        [Header("최대 무게 - 무게 비율 5단계의 기준값 (가방 문서 2.1)")]
        public float maxWeight = 45f;

        [Header("슬롯 개수 (기본 값). 0이면 무제한 - 한도를 쓰지 않는 구성")]
        public int slotCountDefault = 8;

        [Header("슬롯 개수 (최대 값). 아웃게임에서 여기까지 늘린다")]
        public int slotCountMax = 12;

        [Header("합성 필요 개수 기본값 (드랍 문서 9.4). 아이템이 0이면 이 값")]
        public int mergeCountDefault = 5;

        // 튜닝 실수 가드: 최대가 기본보다 작으면 슬롯 증가가 성립하지 않는다
        void OnValidate()
        {
            maxWeight = Mathf.Max(0.01f, maxWeight);

            slotCountDefault = Mathf.Max(0, slotCountDefault);
            slotCountMax = Mathf.Max(slotCountDefault, slotCountMax);

            mergeCountDefault = Mathf.Max(2, mergeCountDefault);
        }

        public static BagDefinition CreateDefault()
        {
            BagDefinition definition = CreateInstance<BagDefinition>();
            definition.name = "BagDefinition (Default)";
            return definition;
        }
    }
}
