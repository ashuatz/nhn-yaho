using System.IO;
using NUnit.Framework;
using Scavenger.Loot;

namespace Scavenger.Tests
{
    public sealed class PlayerStashTests
    {
        string tempDirectory;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "scavenger_stash_tests");
            Directory.CreateDirectory(tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, true);
        }

        string TempFile(string fileName)
        {
            return Path.Combine(tempDirectory, fileName);
        }

        [Test]
        public void AddItem_StacksById()
        {
            PlayerStash stash = new PlayerStash();

            stash.AddItem("scrap_paper", 2);
            stash.AddItem("scrap_paper", 3);

            Assert.AreEqual(5, stash.CountOf("scrap_paper"));
            Assert.AreEqual(1, stash.Entries.Count);
        }

        [Test]
        public void JsonRoundtrip_PreservesEntries()
        {
            PlayerStash stash = new PlayerStash();
            stash.AddItem("scrap_paper", 4);
            stash.AddItem("lockbox", 1);

            PlayerStash restored = PlayerStash.FromJson(stash.ToJson());

            Assert.AreEqual(4, restored.CountOf("scrap_paper"));
            Assert.AreEqual(1, restored.CountOf("lockbox"));
        }

        [Test]
        public void SaveLoad_Roundtrips()
        {
            string path = TempFile("stash.json");
            PlayerStash stash = new PlayerStash();
            stash.AddItem("scrap_metal", 7);

            stash.SaveTo(path);
            PlayerStash loaded = PlayerStash.LoadFrom(path);

            Assert.AreEqual(7, loaded.CountOf("scrap_metal"));
        }

        [Test]
        public void Save_OverwritesExistingAtomically()
        {
            string path = TempFile("stash.json");

            PlayerStash first = new PlayerStash();
            first.AddItem("scrap_paper", 1);
            first.SaveTo(path);

            PlayerStash second = new PlayerStash();
            second.AddItem("scrap_paper", 9);
            second.SaveTo(path);

            PlayerStash loaded = PlayerStash.LoadFrom(path);

            Assert.AreEqual(9, loaded.CountOf("scrap_paper"));
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }

        [Test]
        public void Load_CorruptFile_QuarantinesAndReturnsEmpty()
        {
            string path = TempFile("stash.json");
            File.WriteAllText(path, "{ this is not valid json !!!");

            PlayerStash loaded = PlayerStash.LoadFrom(path);

            Assert.AreEqual(0, loaded.Entries.Count);
            Assert.IsFalse(File.Exists(path));
            Assert.IsTrue(File.Exists(path + ".corrupt"));
        }

        [Test]
        public void Load_MissingFile_ReturnsEmpty()
        {
            PlayerStash loaded = PlayerStash.LoadFrom(TempFile("does_not_exist.json"));

            Assert.AreEqual(0, loaded.Entries.Count);
        }
    }
}
