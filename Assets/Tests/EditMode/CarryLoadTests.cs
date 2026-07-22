using NUnit.Framework;
using Scavenger.Player;

namespace Scavenger.Tests
{
    // 과적 판정 (M2-1, 비율 5단계 이식): 정적 순수 함수만 검증 -
    // MonoBehaviour 수명 주기는 스코프 밖. 경계 = 25/50/75/90% 비율.
    public sealed class CarryLoadTests
    {
        // ResolveSpeedScale 매핑 검증용 대표 배율 (웹 이식 기본값)
        const float Heavy = 0.93f;
        const float VeryHeavy = 0.87f;
        const float Severe = 0.77f;
        const float Overloaded = 0.67f;

        [Test]
        public void EvaluateStage_BelowFirstBoundary_IsLight()
        {
            CarryLoadStage stage = CarryLoad.EvaluateStage(0.24f);

            Assert.AreEqual(CarryLoadStage.Light, stage);
        }

        [Test]
        public void EvaluateStage_ZeroRatio_IsLight()
        {
            CarryLoadStage stage = CarryLoad.EvaluateStage(0f);

            Assert.AreEqual(CarryLoadStage.Light, stage);
        }

        [Test]
        public void EvaluateStage_At25Percent_IsNormal()
        {
            CarryLoadStage stage = CarryLoad.EvaluateStage(0.25f);

            Assert.AreEqual(CarryLoadStage.Normal, stage);
        }

        [Test]
        public void EvaluateStage_At50Percent_IsHeavy()
        {
            CarryLoadStage stage = CarryLoad.EvaluateStage(0.5f);

            Assert.AreEqual(CarryLoadStage.Heavy, stage);
        }

        [Test]
        public void EvaluateStage_At75Percent_IsVeryHeavy()
        {
            CarryLoadStage stage = CarryLoad.EvaluateStage(0.75f);

            Assert.AreEqual(CarryLoadStage.VeryHeavy, stage);
        }

        [Test]
        public void EvaluateStage_At90Percent_IsOverloaded()
        {
            CarryLoadStage stage = CarryLoad.EvaluateStage(0.9f);

            Assert.AreEqual(CarryLoadStage.Overloaded, stage);
        }

        [Test]
        public void EvaluateStage_FullRatio_IsOverloaded()
        {
            CarryLoadStage stage = CarryLoad.EvaluateStage(1f);

            Assert.AreEqual(CarryLoadStage.Overloaded, stage);
        }

        [Test]
        public void ResolveSpeedScale_MapsEachStage()
        {
            Assert.AreEqual(
                1f, CarryLoad.ResolveSpeedScale(CarryLoadStage.Light, Heavy, VeryHeavy, Severe, Overloaded), 0.0001f);

            Assert.AreEqual(
                Heavy, CarryLoad.ResolveSpeedScale(CarryLoadStage.Normal, Heavy, VeryHeavy, Severe, Overloaded), 0.0001f);

            Assert.AreEqual(
                VeryHeavy, CarryLoad.ResolveSpeedScale(CarryLoadStage.Heavy, Heavy, VeryHeavy, Severe, Overloaded), 0.0001f);

            Assert.AreEqual(
                Severe, CarryLoad.ResolveSpeedScale(CarryLoadStage.VeryHeavy, Heavy, VeryHeavy, Severe, Overloaded), 0.0001f);

            Assert.AreEqual(
                Overloaded, CarryLoad.ResolveSpeedScale(CarryLoadStage.Overloaded, Heavy, VeryHeavy, Severe, Overloaded), 0.0001f);
        }
    }
}
