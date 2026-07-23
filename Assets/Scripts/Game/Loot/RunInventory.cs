using System.Collections.Generic;

namespace Scavenger.Loot
{
    /// <summary>
    /// 런 한정 인벤토리. 사망 시 전량 소멸, 탈출 시 스태시로 확정 (S7).
    /// MonoBehaviour가 아닌 순수 클래스 - EditMode 테스트 대상.
    ///
    /// 등급 합성 (웹 프로토타입 이식, A안): 같은 id + 같은 등급이 MergeThreshold(5)개
    /// 모이면 상위 등급 1개로 자동 합성. 무게는 1개분으로 압축(과적 압박 완화),
    /// 가치는 등급당 GradeValueMultiplier(6)배로 증폭 = 파밍 보상감.
    /// 스태시 저장은 등급을 인식하지 않는다 - BankedCounts(id별 실물 총 획득 개수)를
    /// 별도 누적해 정산이 참조 (창고는 기존 id 스택 유지, 아웃게임 설계 불변).
    /// </summary>
    public sealed class RunInventory
    {
        public sealed class Entry
        {
            public LootDefinition Definition;
            public int Count;

            /// <summary>등급 (0=일반, 1=희귀, 2=레어). 합성으로 상승.</summary>
            public int Grade;

            /// <summary>이 (id, 등급) 스택의 무게 합. 합성 시 압축 반영.</summary>
            public float Weight;

            /// <summary>이 (id, 등급) 스택의 가치 합. 합성 시 등급 배수 반영.</summary>
            public int Value;
        }

        // 웹 이식 상수: 5개 합성, 최대 등급 2(레어), 등급당 가치 x6
        const int MergeThreshold = 5;
        const int MaxGrade = 2;
        const int GradeValueMultiplier = 6;

        readonly List<Entry> entries = new List<Entry>();

        // 스태시 저장용 - id별 실물 총 획득 개수 (합성과 무관, 합성 전 원본 기준)
        readonly Dictionary<string, int> bankedCounts = new Dictionary<string, int>();

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

        public void Add(LootDefinition definition)
        {
            if (definition == null)
                return;

            Add(definition, definition.value, definition.weight);
        }

        /// <summary>
        /// 조각 획득용 (LootPickup): 정의는 공유 참조 그대로 두고 가치/무게만
        /// 지분으로 반영한다 - 조각마다 런타임 SO를 만들지 않는다 (Codex 교차 검토).
        /// 등급 0 스택에 쌓이고, 5개가 되면 상위 등급으로 합성된다.
        /// </summary>
        public void Add(LootDefinition definition, int value, float weight)
        {
            if (definition == null)
                return;

            TotalValue += value;
            TotalWeight += weight;

            // 스태시 저장 개수는 합성과 무관하게 실물 획득 시점에 누적
            BankOne(definition.id);

            Entry existing = FindEntry(definition.id, grade: 0);

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
                    Grade = 0,
                    Weight = weight,
                    Value = value,
                });
            }

            MergeChain(definition.id, grade: 0);
        }

        public void Clear()
        {
            entries.Clear();
            bankedCounts.Clear();
            TotalValue = 0;
            TotalWeight = 0f;
        }

        // 같은 (id, grade)가 5개면 상위 등급 1개로 합성하고, 상위에서 다시 5개가
        // 되면 연쇄 합성한다 (등급 2에서 정지). 무게 압축/가치 배수는 여기서 반영.
        //
        // 회계 원칙 (자체 검토 반영): "평균 단가"가 아니라 제거되는 5개 스택의 실제
        // 무게/가치 합을 정확히 회수한다. 조각 지분(LootPickup)으로 개당 값이 달라도
        // 스택 합과 TotalWeight/Value가 어긋나지 않는다. 정수 나눗셈 잔차는 회수 합에
        // 자연히 포함되므로 누수 없음.
        void MergeChain(string id, int grade)
        {
            if (grade >= MaxGrade)
                return;

            Entry lower = FindEntry(id, grade);

            if (lower == null || lower.Count < MergeThreshold)
                return;

            // 제거할 5개분의 실제 무게/가치 = 스택 합의 5/Count 비례 (float 정확)
            float removedWeight = lower.Weight * MergeThreshold / lower.Count;
            int removedValue = (int)System.Math.Round((double)lower.Value * MergeThreshold / lower.Count);

            RemoveUnits(lower, MergeThreshold, removedWeight, removedValue);

            // 상위 등급 1개: 무게는 5개분의 1/5(압축), 가치는 5개분에 등급 배수
            float mergedWeight = removedWeight / MergeThreshold;
            int mergedValue = removedValue * GradeValueMultiplier / MergeThreshold;

            AddMerged(lower.Definition, grade + 1, mergedWeight, mergedValue);

            // 총량 조정 = 넣는 값 - 회수한 값 (스택 실제값과 항상 정합)
            TotalWeight += mergedWeight - removedWeight;
            TotalValue += mergedValue - removedValue;

            // 연쇄: 방금 만든 상위 등급이 5개가 됐는지 재확인
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
