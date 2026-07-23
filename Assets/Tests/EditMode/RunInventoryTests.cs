using NUnit.Framework;
using Scavenger.Loot;

namespace Scavenger.Tests
{
    public sealed class RunInventoryTests
    {
        [Test]
        public void Add_StacksByStableId()
        {
            RunInventory inventory = new RunInventory();
            LootDefinition paper = LootDefinition.Create("scrap_paper", "폐지", 10, 1, 1f);
            LootDefinition paperDuplicate = LootDefinition.Create("scrap_paper", "폐지", 10, 1, 1f);

            inventory.Add(paper);
            inventory.Add(paperDuplicate);

            Assert.AreEqual(1, inventory.Entries.Count);
            Assert.AreEqual(2, inventory.Entries[0].Count);
        }

        [Test]
        public void TotalValue_SumsAllAdds()
        {
            RunInventory inventory = new RunInventory();
            LootDefinition paper = LootDefinition.Create("scrap_paper", "폐지", 10, 1, 1f);
            LootDefinition metal = LootDefinition.Create("scrap_metal", "고철", 30, 2, 2f);

            inventory.Add(paper);
            inventory.Add(metal);
            inventory.Add(metal);

            Assert.AreEqual(70, inventory.TotalValue);
        }

        [Test]
        public void TotalWeight_SumsAllAdds()
        {
            RunInventory inventory = new RunInventory();
            LootDefinition paper = LootDefinition.Create("scrap_paper", "폐지", 10, 1, 1f, 1f);
            LootDefinition metal = LootDefinition.Create("scrap_metal", "고철", 30, 2, 2f, 4f);

            inventory.Add(paper);
            inventory.Add(metal);
            inventory.Add(metal);

            Assert.AreEqual(9f, inventory.TotalWeight, 0.0001f);
        }

        [Test]
        public void Clear_RemovesEverything()
        {
            RunInventory inventory = new RunInventory();
            inventory.Add(LootDefinition.Create("scrap_paper", "폐지", 10, 1, 1f, 2f));

            inventory.Clear();

            Assert.AreEqual(0, inventory.Entries.Count);
            Assert.AreEqual(0, inventory.TotalValue);
            Assert.AreEqual(0f, inventory.TotalWeight, 0.0001f);
            Assert.AreEqual(0, inventory.BankedCounts.Count);
        }

        // -- 등급 합성 (웹 이식, A안) --------------------------------------

        [Test]
        public void Add_FiveOfSameId_MergesToGradeOne()
        {
            RunInventory inventory = new RunInventory();

            for (int i = 0; i < 5; i++)
                inventory.Add(LootDefinition.Create("gear", "톱니", 3, 1, 1f, 0.6f));

            // 등급 0 다섯 개 -> 등급 1 한 개로 압축
            Assert.AreEqual(1, inventory.Entries.Count);
            Assert.AreEqual(1, inventory.Entries[0].Grade);
            Assert.AreEqual(1, inventory.Entries[0].Count);
        }

        [Test]
        public void Merge_CompressesWeightToOneUnit()
        {
            RunInventory inventory = new RunInventory();

            for (int i = 0; i < 5; i++)
                inventory.Add(LootDefinition.Create("gear", "톱니", 3, 1, 1f, 0.6f));

            // 무게는 5개분(3.0)이 아니라 1개분(0.6)으로 압축
            Assert.AreEqual(0.6f, inventory.TotalWeight, 0.0001f);
        }

        [Test]
        public void Merge_MultipliesValueByGrade()
        {
            RunInventory inventory = new RunInventory();

            for (int i = 0; i < 5; i++)
                inventory.Add(LootDefinition.Create("gear", "톱니", 3, 1, 1f, 0.6f));

            // 가치는 등급 배수(x6): 단가 3 -> 상위 1개 18
            Assert.AreEqual(18, inventory.TotalValue);
        }

        [Test]
        public void Merge_KeepsBankedCountAsRawTotal()
        {
            RunInventory inventory = new RunInventory();

            for (int i = 0; i < 5; i++)
                inventory.Add(LootDefinition.Create("gear", "톱니", 3, 1, 1f, 0.6f));

            // 스태시 저장용 실물 개수는 합성과 무관하게 5개 유지 (A안)
            Assert.AreEqual(5, inventory.BankedCounts["gear"]);
        }

        [Test]
        public void Merge_BelowThreshold_DoesNotMerge()
        {
            RunInventory inventory = new RunInventory();

            for (int i = 0; i < 4; i++)
                inventory.Add(LootDefinition.Create("gear", "톱니", 3, 1, 1f, 0.6f));

            Assert.AreEqual(1, inventory.Entries.Count);
            Assert.AreEqual(0, inventory.Entries[0].Grade);
            Assert.AreEqual(4, inventory.Entries[0].Count);
        }

        // 회계 정합성 (자체 검토 버그 회귀): 조각 지분으로 개당 가치가 달라도
        // TotalValue/Weight가 엔트리 합과 어긋나지 않아야 한다 (정수 나눗셈 누수 방지).
        [Test]
        public void Merge_UnevenPieceValues_KeepsTotalsConsistent()
        {
            RunInventory inventory = new RunInventory();
            LootDefinition gear = LootDefinition.Create("gear", "톱니", 3, 1, 1f, 0.6f);

            // 지분이 제각각인 5개 (합 16, 평균 3.2 - 정수 나눗셈이면 잔차 발생)
            int[] pieceValues = { 3, 3, 3, 4, 3 };
            float[] pieceWeights = { 0.6f, 0.6f, 0.7f, 0.6f, 0.5f };

            for (int i = 0; i < 5; i++)
                inventory.Add(gear, pieceValues[i], pieceWeights[i]);

            // 합성 후 TotalValue는 엔트리들의 Value 합과 정확히 일치해야 한다
            int entrySum = 0;
            float weightSum = 0f;

            foreach (RunInventory.Entry entry in inventory.Entries)
            {
                entrySum += entry.Value;
                weightSum += entry.Weight;
            }

            Assert.AreEqual(entrySum, inventory.TotalValue, "TotalValue가 엔트리 합과 어긋남");
            Assert.AreEqual(weightSum, inventory.TotalWeight, 0.0001f, "TotalWeight가 엔트리 합과 어긋남");
        }
    }
}
