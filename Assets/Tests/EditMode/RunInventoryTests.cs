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
        public void Add_FiveOfSameId_MergesToNextGrade()
        {
            RunInventory inventory = new RunInventory();

            for (int i = 0; i < 5; i++)
                inventory.Add(LootDefinition.Create("gear", "톱니", 3, 1, 1f, 0.6f));

            // 일반(1) 다섯 개 -> 희귀(2) 한 개로 압축
            Assert.AreEqual(1, inventory.Entries.Count);
            Assert.AreEqual(2, inventory.Entries[0].Grade);
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
            Assert.AreEqual(1, inventory.Entries[0].Grade);
            Assert.AreEqual(4, inventory.Entries[0].Count);
        }

        // -- 등급 4단계 / 합성 N 컬럼 (문서 대비 블로커 해소) -------------------

        [Test]
        public void Add_StartsAtItemGrade()
        {
            RunInventory inventory = new RunInventory();

            // 영웅(3) 등급 아이템은 일반이 아니라 영웅 스택으로 들어간다
            inventory.Add(LootDefinition.Create("relic", "유물", 50, 3, 1f, 2f));

            Assert.AreEqual(3, inventory.Entries[0].Grade);
        }

        [Test]
        public void Merge_ReachesLegendary()
        {
            RunInventory inventory = new RunInventory();

            for (int i = 0; i < 5; i++)
                inventory.Add(LootDefinition.Create("relic", "유물", 50, 3, 1f, 2f));

            // 영웅(3) 다섯 개 -> 전설(4) 한 개
            Assert.AreEqual(1, inventory.Entries.Count);
            Assert.AreEqual(LootDefinition.MaxTier, inventory.Entries[0].Grade);
        }

        [Test]
        public void Merge_StopsAtLegendary()
        {
            RunInventory inventory = new RunInventory();

            for (int i = 0; i < 5; i++)
                inventory.Add(LootDefinition.Create("relic", "유물", 50, 4, 1f, 2f));

            // 전설(4)은 더 올라갈 등급이 없으므로 다섯 개가 그대로 쌓인다
            Assert.AreEqual(1, inventory.Entries.Count);
            Assert.AreEqual(LootDefinition.MaxTier, inventory.Entries[0].Grade);
            Assert.AreEqual(5, inventory.Entries[0].Count);
        }

        [Test]
        public void Merge_UsesItemMergeCountOverride()
        {
            RunInventory inventory = new RunInventory();

            LootDefinition farmed = LootDefinition.Create("crystal", "결정", 20, 1, 1f, 1f);
            farmed.mergeCount = 3;

            for (int i = 0; i < 3; i++)
                inventory.Add(farmed);

            // 아이템 컬럼 N(3)이 전역 기본값(5)을 덮어쓴다 (파밍 아이템 규칙)
            Assert.AreEqual(1, inventory.Entries.Count);
            Assert.AreEqual(2, inventory.Entries[0].Grade);
            Assert.AreEqual(1, inventory.Entries[0].Count);
        }

        [Test]
        public void Merge_UsesConfiguredDefaultMergeCount()
        {
            RunInventory inventory = new RunInventory();
            inventory.Configure(RunInventory.UnlimitedSlots, mergeCountDefault: 3);

            for (int i = 0; i < 3; i++)
                inventory.Add(LootDefinition.Create("gear", "톱니", 3, 1, 1f, 0.6f));

            Assert.AreEqual(2, inventory.Entries[0].Grade);
        }

        // -- 슬롯 한도 (가방 문서 2.1 / 5장) ----------------------------------

        [Test]
        public void HasSlotFor_ExistingStack_DoesNotNeedFreeSlot()
        {
            RunInventory inventory = new RunInventory();
            inventory.Configure(slotCapacity: 1, mergeCountDefault: 5);

            LootDefinition paper = LootDefinition.Create("scrap_paper", "폐지", 10, 1, 1f);
            inventory.Add(paper);

            // 같은 (id, 등급)은 한 칸에 개수로 쌓이므로 슬롯을 더 쓰지 않는다
            Assert.IsTrue(inventory.HasSlotFor(paper));
        }

        [Test]
        public void HasSlotFor_NewStackWithoutFreeSlot_IsRejected()
        {
            RunInventory inventory = new RunInventory();
            inventory.Configure(slotCapacity: 1, mergeCountDefault: 5);

            inventory.Add(LootDefinition.Create("scrap_paper", "폐지", 10, 1, 1f));

            LootDefinition metal = LootDefinition.Create("scrap_metal", "고철", 30, 2, 1f);

            Assert.AreEqual(1, inventory.UsedSlots);
            Assert.IsFalse(inventory.HasSlotFor(metal));
        }

        [Test]
        public void HasSlotFor_UnlimitedCapacity_AlwaysAccepts()
        {
            RunInventory inventory = new RunInventory();

            for (int i = 0; i < 4; i++)
                inventory.Add(LootDefinition.Create($"item_{i}", "아이템", 10, 1, 1f));

            Assert.IsTrue(inventory.HasSlotFor(LootDefinition.Create("extra", "추가", 10, 1, 1f)));
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
