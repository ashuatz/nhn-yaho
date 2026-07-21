using NUnit.Framework;
using Scavenger.Loot;
using Scavenger.Run;

namespace Scavenger.Tests
{
    public sealed class RunSettlementTests
    {
        [Test]
        public void BankInventory_AddsAllEntriesToStash()
        {
            RunInventory inventory = new RunInventory();
            LootDefinition paper = LootDefinition.Create("scrap_paper", "폐지", 10, 1, 1f);
            LootDefinition lockbox = LootDefinition.Create("lockbox", "금고", 90, 3, 4f);

            inventory.Add(paper);
            inventory.Add(paper);
            inventory.Add(lockbox);

            PlayerStash stash = new PlayerStash();
            RunSettlement.BankInventory(inventory, stash);

            Assert.AreEqual(2, stash.CountOf("scrap_paper"));
            Assert.AreEqual(1, stash.CountOf("lockbox"));
        }

        [Test]
        public void BankInventory_StacksOntoExistingStash()
        {
            RunInventory inventory = new RunInventory();
            inventory.Add(LootDefinition.Create("scrap_paper", "폐지", 10, 1, 1f));

            PlayerStash stash = new PlayerStash();
            stash.AddItem("scrap_paper", 5);

            RunSettlement.BankInventory(inventory, stash);

            Assert.AreEqual(6, stash.CountOf("scrap_paper"));
        }

        [Test]
        public void BankInventory_EmptyInventory_LeavesStashUntouched()
        {
            PlayerStash stash = new PlayerStash();
            stash.AddItem("scrap_metal", 3);

            RunSettlement.BankInventory(new RunInventory(), stash);

            Assert.AreEqual(3, stash.CountOf("scrap_metal"));
            Assert.AreEqual(1, stash.Entries.Count);
        }
    }
}
