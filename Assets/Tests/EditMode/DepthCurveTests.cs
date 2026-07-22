using NUnit.Framework;
using Scavenger.Segment;

namespace Scavenger.Tests
{
    public sealed class DepthCurveTests
    {
        [Test]
        public void TierWeights_SumToOne_AtAnyDepth()
        {
            DepthCurve curve = DepthCurve.CreateDefault();

            for (int depth = 1; depth <= 20; depth++)
            {
                float[] weights = curve.EvaluateTierWeights(depth);
                float sum = weights[0] + weights[1] + weights[2];

                Assert.AreEqual(1f, sum, 0.001f, $"depth {depth}");
            }
        }

        [Test]
        public void Fuse_NeverBelowMinimumWarning()
        {
            DepthCurve curve = DepthCurve.CreateDefault();

            for (int depth = 1; depth <= 50; depth++)
                Assert.GreaterOrEqual(curve.EvaluateFuseSeconds(depth), curve.fuseMinSeconds);
        }

        [Test]
        public void BlastRadius_NeverSealsCorridor()
        {
            DepthCurve curve = DepthCurve.CreateDefault();
            SegmentDefinition segment = SegmentDefinition.CreateDefault();

            for (int depth = 1; depth <= 50; depth++)
            {
                float radius = curve.EvaluateBlastRadius(depth);

                // 폭발 지름이 복도 폭보다 작아야 항상 회피 경로가 남는다
                Assert.Less(radius * 2f, segment.corridorHalfWidth * 2f, $"depth {depth}");
            }
        }

        [Test]
        public void BombCount_Capped()
        {
            DepthCurve curve = DepthCurve.CreateDefault();

            Assert.LessOrEqual(curve.EvaluateBombCount(100), curve.bombMaxCount);
        }

        [Test]
        public void TrapCounts_CappedAndNeverNegative()
        {
            DepthCurve curve = DepthCurve.CreateDefault();

            for (int depth = 1; depth <= 50; depth++)
            {
                int sinkCount = curve.EvaluateSinkTrapCount(depth);
                int pushCount = curve.EvaluatePushTrapCount(depth);

                Assert.GreaterOrEqual(sinkCount, 0, $"depth {depth}");
                Assert.LessOrEqual(sinkCount, curve.sinkTrapMaxCount, $"depth {depth}");
                Assert.GreaterOrEqual(pushCount, 0, $"depth {depth}");
                Assert.LessOrEqual(pushCount, curve.pushTrapMaxCount, $"depth {depth}");
            }
        }

        [Test]
        public void PushTraps_AbsentAtDepthOne_ByDefault()
        {
            DepthCurve curve = DepthCurve.CreateDefault();

            // 기본 튜닝: 첫 구간은 밀기 트랩 없이 학습 구간으로 남긴다
            Assert.AreEqual(0, curve.EvaluatePushTrapCount(1));
            Assert.GreaterOrEqual(curve.EvaluatePushTrapCount(2), 1);
        }

        [Test]
        public void DeeperDepth_ShiftsWeightTowardHighTier()
        {
            DepthCurve curve = DepthCurve.CreateDefault();

            float[] shallow = curve.EvaluateTierWeights(1);
            float[] deep = curve.EvaluateTierWeights(6);

            Assert.Greater(deep[2], shallow[2]);
            Assert.Less(deep[0], shallow[0]);
        }
    }
}
