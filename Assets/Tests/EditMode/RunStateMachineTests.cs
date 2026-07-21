using NUnit.Framework;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Tests
{
    public sealed class RunStateMachineTests
    {
        GameObject host;
        RunStateMachine machine;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("RunStateMachineTest");
            machine = host.AddComponent<RunStateMachine>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
        }

        [Test]
        public void StartsInReady()
        {
            Assert.AreEqual(RunState.Ready, machine.Current);
        }

        [Test]
        public void ValidFlow_ReadyRunningExtractedReady()
        {
            Assert.IsTrue(machine.TryTransition(RunState.Running));
            Assert.IsTrue(machine.TryTransition(RunState.Extracted));
            Assert.IsTrue(machine.TryTransition(RunState.Ready));
        }

        [Test]
        public void Running_CanDie()
        {
            machine.TryTransition(RunState.Running);

            Assert.IsTrue(machine.TryTransition(RunState.Dead));
        }

        [Test]
        public void Ready_CannotExtractOrDie()
        {
            Assert.IsFalse(machine.TryTransition(RunState.Extracted));
            Assert.IsFalse(machine.TryTransition(RunState.Dead));
        }

        [Test]
        public void Extracted_CannotGoRunningDirectly()
        {
            machine.TryTransition(RunState.Running);
            machine.TryTransition(RunState.Extracted);

            Assert.IsFalse(machine.TryTransition(RunState.Running));
        }

        [Test]
        public void DuplicateSettlementBlocked_ExtractedTwiceFails()
        {
            machine.TryTransition(RunState.Running);

            Assert.IsTrue(machine.TryTransition(RunState.Extracted));
            Assert.IsFalse(machine.TryTransition(RunState.Extracted));
        }
    }
}
