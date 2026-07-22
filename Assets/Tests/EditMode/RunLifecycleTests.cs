using NUnit.Framework;
using Scavenger.Loot;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Tests
{
    public sealed class RunLifecycleTests
    {
        GameObject host;
        RunManager manager;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("RunManagerTest");
            manager = host.AddComponent<RunManager>();

            RunSettings settings = RunSettings.CreateDefault();
            settings.seedOverride = 42;
            manager.Configure(settings);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
        }

        [Test]
        public void StartRun_SameSeed_ReproducesRngSequence()
        {
            manager.StartRun();
            int firstRoll = manager.Rng.Next();

            // 종료 후 같은 시드로 재시작
            manager.KillRun("test");
            manager.StateMachine.TryTransition(RunState.Ready);
            manager.StartRun();

            Assert.AreEqual(firstRoll, manager.Rng.Next());
        }

        [Test]
        public void StartRun_ClearsPreviousInventory()
        {
            manager.Inventory.Add(LootDefinition.Create("scrap_paper", "폐지", 10, 1, 1f));

            manager.StartRun();

            Assert.AreEqual(0, manager.Inventory.TotalValue);
            Assert.AreEqual(0, manager.Inventory.Entries.Count);
        }

        [Test]
        public void StartRun_BeginsElapsedTimer()
        {
            manager.StartRun();

            Assert.IsTrue(manager.Timer.IsTicking);
            Assert.AreEqual(0f, manager.Timer.Elapsed, 0.0001f);
        }

        [Test]
        public void CompleteExtraction_StopsTimerAndTransitions()
        {
            manager.StartRun();

            manager.CompleteExtraction();

            Assert.AreEqual(RunState.Extracted, manager.StateMachine.Current);
            Assert.IsFalse(manager.Timer.IsTicking);
        }

        [Test]
        public void KillRun_TransitionsDeadOnce()
        {
            manager.StartRun();

            manager.KillRun("bomb");
            manager.KillRun("bomb");

            Assert.AreEqual(RunState.Dead, manager.StateMachine.Current);
        }

        [Test]
        public void CompleteExtraction_IgnoredWhenNotRunning()
        {
            manager.CompleteExtraction();

            Assert.AreEqual(RunState.Ready, manager.StateMachine.Current);
        }
    }
}
