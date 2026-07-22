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
        }
    }
}
