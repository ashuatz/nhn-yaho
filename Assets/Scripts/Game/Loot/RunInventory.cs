using System.Collections.Generic;
using UnityEngine;

namespace Scavenger.Loot
{
    /// <summary>
    /// 런 한정 인벤토리. 사망 시 전량 소멸, 탈출 시 스태시로 확정 (S7).
    /// MonoBehaviour가 아닌 순수 클래스 - EditMode 테스트 대상.
    ///
    /// 등급 합성 (드랍 문서 3장 / 파밍 문서 4.2): 같은 id + 같은 등급이
    /// 합성 필요 개수(N)만큼 모이면 상위 등급 1개로 자동 합성. 등급 축은 아이템 등급과
    /// 같다 (1=일반 / 2=희귀 / 3=영웅 / 4=전설) - 아이템의 tier가 시작 등급이고
    /// 전설에서 멈춘다. 무게는 1개분으로 압축(과적 압박 완화), 가치는 등급당
    /// GradeValueMultiplier(6)배로 증폭 = 파밍 보상감.
    ///
    /// N은 전역 기본값 + 아이템별 덮어쓰기다 (드랍 9.4 전역 컬럼 + 파밍 4.4 아이템 컬럼).
    ///
    /// 슬롯 한도 (가방 문서 2.1): 슬롯 하나 = (id, 등급) 스택 하나.
    /// 새 스택이 필요한데 슬롯이 없으면 획득이 거부된다 (가방 문서 5장).
    ///
    /// 스태시 저장은 등급을 인식하지 않는다 - BankedCounts(id별 실물 총 획득 개수)를
    /// 별도 누적해 정산이 참조 (창고는 기존 id 스택 유지, 아웃게임 설계 불변).
    /// </summary>
    public sealed class RunInventory
    {
        public sealed class Entry
        {
            public LootDefinition Definition;
            public int Count;

            /// <summary>등급 (1=일반 / 2=희귀 / 3=영웅 / 4=전설). 합성으로 상승.</summary>
            public int Grade;

            /// <summary>이 (id, 등급) 스택의 무게 합. 합성 시 압축 반영.</summary>
            public float Weight;

            /// <summary>이 (id, 등급) 스택의 가치 합. 합성 시 등급 배수 반영.</summary>
            public int Value;
        }

        /// <summary>합성 필요 개수 전역 기본값 (드랍 문서 9.4). 아이템이 0이면 이 값.</summary>
        public const int DefaultMergeCount = 5;

        /// <summary>슬롯 무제한 (한도를 배선하지 않은 경우).</summary>
        public const int UnlimitedSlots = 0;

        // 등급당 가치 배수 (웹 이식). 합성 보상감의 크기
        const int GradeValueMultiplier = 6;

        readonly List<Entry> entries = new List<Entry>();

        // 스태시 저장용 - id별 실물 총 획득 개수 (합성과 무관, 합성 전 원본 기준)
        readonly Dictionary<string, int> bankedCounts = new Dictionary<string, int>();

        int mergeCountDefault = DefaultMergeCount;

        public IReadOnlyList<Entry> Entries
        {
            get { return entries; }
        }

        /// <summary>id별 실물 총 획득 개수 (스태시 정산용, 합성 전 원본 기준).</summary>
        public IReadOnlyDictionary<string, int> BankedCounts
        {
            get { return bankedCounts; }
        }

        public int TotalValue { get; private set; }

        /// <summary>무게 합산 (M2-1). CarryLoad가 과적 판정에 사용.</summary>
        public float TotalWeight { get; private set; }

        /// <summary>사용 가능한 슬롯 개수 (가방 문서 2.1). 0이면 무제한.</summary>
        public int SlotCapacity { get; private set; } = UnlimitedSlots;

        /// <summary>사용 중인 슬롯 개수 = (id, 등급) 스택 개수.</summary>
        public int UsedSlots
        {
            get { return entries.Count; }
        }

        /// <summary>
        /// 가방 규격 주입 (가방 컬럼). GameFlow가 BagDefinition에서 배선한다.
        /// slotCapacity 0 = 무제한 (한도를 쓰지 않는 구성).
        /// </summary>
        public void Configure(int slotCapacity, int mergeCountDefault)
        {
            SlotCapacity = slotCapacity < 0 ? UnlimitedSlots : slotCapacity;

            if (mergeCountDefault >= 2)
                this.mergeCountDefault = mergeCountDefault;
        }

        /// <summary>
        /// 이 아이템을 담을 슬롯이 있는가 (가방 문서 5장 - 슬롯 초과 판정).
        /// 이미 같은 (id, 등급) 스택이 있으면 개수만 늘어나므로 슬롯을 쓰지 않는다.
        /// 무게 초과 판정은 CarryLoad가 소유한다 - 여기서는 슬롯만 본다.
        /// </summary>
        public bool HasSlotFor(LootDefinition definition)
        {
            if (definition == null)
                return false;

            if (SlotCapacity == UnlimitedSlots)
                return true;

            if (FindEntry(definition.id, ResolveStartGrade(definition)) != null)
                return true;

            return entries.Count < SlotCapacity;
        }

        public void Add(LootDefinition definition)
        {
            if (definition == null)
                return;

            Add(definition, definition.value, definition.weight);
        }

        /// <summary>
        /// 조각 획득용 (LootPickup): 정의는 공유 참조 그대로 두고 가치/무게만
        /// 지분으로 반영한다 - 조각마다 런타임 SO를 만들지 않는다 (Codex 교차 검토).
        /// 아이템의 등급(tier)에서 시작하고, N개가 되면 상위 등급으로 합성된다.
        /// </summary>
        public void Add(LootDefinition definition, int value, float weight)
        {
            if (definition == null)
                return;

            int startGrade = ResolveStartGrade(definition);

            TotalValue += value;
            TotalWeight += weight;

            // 스태시 저장 개수는 합성과 무관하게 실물 획득 시점에 누적
            BankOne(definition.id);

            Entry existing = FindEntry(definition.id, startGrade);

            if (existing != null)
            {
                existing.Count += 1;
                existing.Weight += weight;
                existing.Value += value;
            }
            else
            {
                entries.Add(new Entry
                {
                    Definition = definition,
                    Count = 1,
                    Grade = startGrade,
                    Weight = weight,
                    Value = value,
                });
            }

            MergeChain(definition.id, startGrade);
        }

        public void Clear()
        {
            entries.Clear();
            bankedCounts.Clear();
            TotalValue = 0;
            TotalWeight = 0f;
        }

        // -- 버리기 / 되돌리기 (가방 문서 6장, 사용자 지시 2026-07-26 드래그앤드롭) ----

        /// <summary>
        /// 가방에서 밖으로 나간 아이템 1개의 몫. 등급까지 들고 나가야
        /// 다시 주웠을 때 합성 결과(가치/무게)가 사라지지 않는다.
        /// </summary>
        public struct DroppedItem
        {
            public LootDefinition Definition;
            public int Grade;
            public int Value;
            public float Weight;

            public bool IsValid
            {
                get { return Definition != null; }
            }
        }

        /// <summary>
        /// 슬롯(스택)에서 1개를 덜어낸다. 반환값이 월드에 놓을 몫이며,
        /// 스택이 비면 슬롯도 함께 비운다. 실패하면 false (인벤토리는 그대로).
        /// </summary>
        public bool TryDropOne(int slotIndex, out DroppedItem dropped)
        {
            dropped = default;

            if (slotIndex < 0 || slotIndex >= entries.Count)
                return false;

            Entry entry = entries[slotIndex];

            if (entry == null || entry.Count <= 0)
                return false;

            // 스택 합에서 1개분을 정확히 떼어낸다 (조각 지분 때문에 개당 값이 다를 수 있다)
            float weight = entry.Weight / entry.Count;
            int value = (int)System.Math.Round((double)entry.Value / entry.Count);

            dropped = new DroppedItem
            {
                Definition = entry.Definition,
                Grade = entry.Grade,
                Value = value,
                Weight = weight,
            };

            RemoveUnits(entry, 1, weight, value);

            TotalWeight -= weight;
            TotalValue -= value;

            UnbankOne(dropped.Definition.id);

            return true;
        }

        /// <summary>
        /// 버렸던 아이템을 다시 담는다 (등급 유지). 일반 획득과 달리 시작 등급으로
        /// 되돌리지 않는다 - 합성해 둔 결과를 버렸다 주웠다고 잃으면 안 된다.
        /// </summary>
        public void Restore(DroppedItem item)
        {
            if (!item.IsValid)
                return;

            int grade = Mathf.Clamp(item.Grade, 1, LootDefinition.MaxTier);

            TotalValue += item.Value;
            TotalWeight += item.Weight;

            BankOne(item.Definition.id);
            AddMerged(item.Definition, grade, item.Weight, item.Value);

            MergeChain(item.Definition.id, grade);
        }

        /// <summary>
        /// 이 (아이템, 등급)을 담을 슬롯이 있는가. 버린 아이템을 다시 주울 때는
        /// 시작 등급이 아니라 들고 나간 등급으로 판정해야 한다.
        /// </summary>
        public bool HasSlotFor(LootDefinition definition, int grade)
        {
            if (definition == null)
                return false;

            if (SlotCapacity == UnlimitedSlots)
                return true;

            if (FindEntry(definition.id, Mathf.Clamp(grade, 1, LootDefinition.MaxTier)) != null)
                return true;

            return entries.Count < SlotCapacity;
        }

        // 아이템의 시작 등급 = 아이템 등급(tier). 범위를 벗어난 데이터는 클램프
        static int ResolveStartGrade(LootDefinition definition)
        {
            if (definition.tier < 1)
                return 1;

            if (definition.tier > LootDefinition.MaxTier)
                return LootDefinition.MaxTier;

            return definition.tier;
        }

        // 합성 필요 개수: 아이템 컬럼이 0이면 전역 기본값 (파밍 문서 4.2)
        int ResolveMergeCount(LootDefinition definition)
        {
            if (definition != null && definition.mergeCount >= 2)
                return definition.mergeCount;

            return mergeCountDefault;
        }

        // 같은 (id, grade)가 N개면 상위 등급 1개로 합성하고, 상위에서 다시 N개가
        // 되면 연쇄 합성한다 (전설에서 정지). 무게 압축/가치 배수는 여기서 반영.
        //
        // 회계 원칙 (자체 검토 반영): "평균 단가"가 아니라 제거되는 N개 스택의 실제
        // 무게/가치 합을 정확히 회수한다. 조각 지분(LootPickup)으로 개당 값이 달라도
        // 스택 합과 TotalWeight/Value가 어긋나지 않는다. 정수 나눗셈 잔차는 회수 합에
        // 자연히 포함되므로 누수 없음.
        void MergeChain(string id, int grade)
        {
            if (grade >= LootDefinition.MaxTier)
                return;

            Entry lower = FindEntry(id, grade);

            if (lower == null)
                return;

            int mergeCount = ResolveMergeCount(lower.Definition);

            if (lower.Count < mergeCount)
                return;

            // 제거할 N개분의 실제 무게/가치 = 스택 합의 N/Count 비례 (float 정확)
            float removedWeight = lower.Weight * mergeCount / lower.Count;
            int removedValue = (int)System.Math.Round((double)lower.Value * mergeCount / lower.Count);

            LootDefinition definition = lower.Definition;

            RemoveUnits(lower, mergeCount, removedWeight, removedValue);

            // 상위 등급 1개: 무게는 N개분의 1/N(압축), 가치는 N개분에 등급 배수
            float mergedWeight = removedWeight / mergeCount;
            int mergedValue = removedValue * GradeValueMultiplier / mergeCount;

            AddMerged(definition, grade + 1, mergedWeight, mergedValue);

            // 총량 조정 = 넣는 값 - 회수한 값 (스택 실제값과 항상 정합)
            TotalWeight += mergedWeight - removedWeight;
            TotalValue += mergedValue - removedValue;

            // 연쇄: 방금 만든 상위 등급이 N개가 됐는지 재확인
            MergeChain(id, grade + 1);
        }

        void RemoveUnits(Entry entry, int count, float removedWeight, int removedValue)
        {
            entry.Count -= count;
            entry.Weight -= removedWeight;
            entry.Value -= removedValue;

            if (entry.Count > 0)
                return;

            // 스택이 비면 부동소수/정수 잔차가 남지 않도록 0으로 정리하고 제거
            entry.Weight = 0f;
            entry.Value = 0;
            entries.Remove(entry);
        }

        void AddMerged(LootDefinition definition, int grade, float weight, int value)
        {
            Entry existing = FindEntry(definition.id, grade);

            if (existing != null)
            {
                existing.Count += 1;
                existing.Weight += weight;
                existing.Value += value;
                return;
            }

            entries.Add(new Entry
            {
                Definition = definition,
                Count = 1,
                Grade = grade,
                Weight = weight,
                Value = value,
            });
        }

        void BankOne(string id)
        {
            if (bankedCounts.TryGetValue(id, out int current))
            {
                bankedCounts[id] = current + 1;
                return;
            }

            bankedCounts[id] = 1;
        }

        // 버리면 실물 획득 개수도 되돌린다 - 다시 주우면 Add가 또 세므로,
        // 되돌리지 않으면 버리고 줍기를 반복해 창고 개수를 부풀릴 수 있다
        void UnbankOne(string id)
        {
            if (!bankedCounts.TryGetValue(id, out int current))
                return;

            if (current <= 1)
            {
                bankedCounts.Remove(id);
                return;
            }

            bankedCounts[id] = current - 1;
        }

        Entry FindEntry(string id, int grade)
        {
            foreach (Entry entry in entries)
            {
                if (entry.Definition.id == id && entry.Grade == grade)
                    return entry;
            }

            return null;
        }
    }
}
