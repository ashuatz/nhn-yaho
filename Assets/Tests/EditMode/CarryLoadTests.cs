using NUnit.Framework;
using Scavenger.Player;

namespace Scavenger.Tests
{
    // 과적 판정 (M2-1): 정적 순수 함수만 검증 - MonoBehaviour 수명 주기는 스코프 밖
    public sealed class CarryLoadTests
    {
        const float OverloadedAt = 8f;
        const float SeverelyOverloadedAt = 16f;

        [Test]
        public void EvaluateStage_BelowOverload_IsNormal()
        {
            CarryLoadStage stage = CarryLoad.EvaluateStage(7.9f, OverloadedAt, SeverelyOverloadedAt);

            Assert.AreEqual(CarryLoadStage.Normal, stage);
        }

        [Test]
        public void EvaluateStage_AtOverloadBoundary_IsOverloaded()
        {
            CarryLoadStage stage = CarryLoad.EvaluateStage(8f, OverloadedAt, SeverelyOverloadedAt);

            Assert.AreEqual(CarryLoadStage.Overloaded, stage);
        }

        [Test]
        public void EvaluateStage_AtSevereBoundary_IsSeverelyOverloaded()
        {
            CarryLoadStage stage = CarryLoad.EvaluateStage(16f, OverloadedAt, SeverelyOverloadedAt);

            Assert.AreEqual(CarryLoadStage.SeverelyOverloaded, stage);
        }

        [Test]
        public void EvaluateStage_ZeroWeight_IsNormal()
        {
            CarryLoadStage stage = CarryLoad.EvaluateStage(0f, OverloadedAt, SeverelyOverloadedAt);

            Assert.AreEqual(CarryLoadStage.Normal, stage);
        }

        [Test]
        public void ResolveSpeedScale_MapsEachStage()
        {
            Assert.AreEqual(1f, CarryLoad.ResolveSpeedScale(CarryLoadStage.Normal, 0.7f, 0.45f), 0.0001f);
            Assert.AreEqual(0.7f, CarryLoad.ResolveSpeedScale(CarryLoadStage.Overloaded, 0.7f, 0.45f), 0.0001f);
            Assert.AreEqual(
                0.45f, CarryLoad.ResolveSpeedScale(CarryLoadStage.SeverelyOverloaded, 0.7f, 0.45f), 0.0001f);
        }
    }
}
