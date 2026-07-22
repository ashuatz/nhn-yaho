using System.Collections.Generic;

namespace Scavenger.Loot
{
    /// <summary>
    /// 런 한정 인벤토리. 사망 시 전량 소멸, 탈출 시 스태시로 확정 (S7).
    /// MonoBehaviour가 아닌 순수 클래스 - EditMode 테스트 대상.
    /// </summary>
    public sealed class RunInventory
    {
        public sealed class Entry
        {
            public LootDefinition Definition;
            public int Count;
        }

        readonly List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries
        {
            get { return entries; }
        }

        public int TotalValue { get; private set; }

        /// <summary>무게 합산 (M2-1). CarryLoad가 과적 판정에 사용.</summary>
        public float TotalWeight { get; private set; }

        public void Add(LootDefinition definition)
        {
            if (definition == null)
                return;

            TotalValue += definition.value;
            TotalWeight += definition.weight;

            Entry existing = FindEntry(definition.id);

            if (existing != null)
            {
                existing.Count += 1;
                return;
            }

            entries.Add(new Entry { Definition = definition, Count = 1 });
        }

        public void Clear()
        {
            entries.Clear();
            TotalValue = 0;
            TotalWeight = 0f;
        }

        Entry FindEntry(string id)
        {
            foreach (Entry entry in entries)
            {
                if (entry.Definition.id == id)
                    return entry;
            }

            return null;
        }
    }
}
