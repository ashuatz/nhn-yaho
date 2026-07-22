using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 깊이 -> 난이도/보상 스케일링의 단일 소스 (구현계획 v0.0.2 섹션 2.3).
    /// 밀도만이 아니라 기폭 시간, 폭발 반경까지 함께 조정한다 (검증 반영).
    /// 모든 값은 클램프로 상한/하한을 강제해 통과 불가 배치를 막는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Scavenger/Depth Curve", fileName = "DepthCurve")]
    public sealed class DepthCurve : ScriptableObject
    {
        [Header("루트 tier 가중치 (depth 1 기준값과 깊이당 변화량)")]
        public float tier1BaseWeight = 0.7f;
        public float tier1WeightPerDepth = -0.08f;
        public float tier1MinWeight = 0.2f;

        public float tier3BaseWeight = 0.05f;
        public float tier3WeightPerDepth = 0.06f;
        public float tier3MaxWeight = 0.5f;

        [Header("폭탄 밀도")]
        public int bombBaseCount = 6;
        public int bombCountPerDepth = 1;
        public int bombMaxCount = 12;

        [Header("폭탄 기폭 시간 (초). 하한 = 최소 예고 시간")]
        public float fuseBaseSeconds = 1.6f;
        public float fusePerDepth = -0.1f;
        public float fuseMinSeconds = 0.9f;

        [Header("폭발 반경. 상한 < 복도 반폭 유지 (전 레인 봉쇄 금지)")]
        public float blastBaseRadius = 1.7f;
        public float blastPerDepth = 0.08f;
        public float blastMaxRadius = 2.4f;

        [Header("땅 꺼짐 트랩 수 (M3-2)")]
        public float sinkTrapBaseCount = 1f;
        public float sinkTrapPerDepth = 0.5f;
        public int sinkTrapMaxCount = 3;

        [Header("밀기 트랩 수 (M3-3). 기본값은 depth 2부터 등장")]
        public float pushTrapBaseCount = 0.5f;
        public float pushTrapPerDepth = 0.5f;
        public int pushTrapMaxCount = 2;

        /// <summary>tier 1..3 가중치 배열 (합 1)을 돌려준다.</summary>
        public float[] EvaluateTierWeights(int depth)
        {
            int steps = depth - 1;

            float tier1 = Mathf.Max(tier1MinWeight, tier1BaseWeight + tier1WeightPerDepth * steps);
            float tier3 = Mathf.Min(tier3MaxWeight, tier3BaseWeight + tier3WeightPerDepth * steps);
            float tier2 = Mathf.Max(0f, 1f - tier1 - tier3);

            return new[] { tier1, tier2, tier3 };
        }

        public int EvaluateBombCount(int depth)
        {
            int count = bombBaseCount + bombCountPerDepth * (depth - 1);
            return Mathf.Min(count, bombMaxCount);
        }

        public float EvaluateFuseSeconds(int depth)
        {
            float fuse = fuseBaseSeconds + fusePerDepth * (depth - 1);
            return Mathf.Max(fuse, fuseMinSeconds);
        }

        public float EvaluateBlastRadius(int depth)
        {
            float radius = blastBaseRadius + blastPerDepth * (depth - 1);
            return Mathf.Min(radius, blastMaxRadius);
        }

        public int EvaluateSinkTrapCount(int depth)
        {
            int count = Mathf.FloorToInt(sinkTrapBaseCount + sinkTrapPerDepth * (depth - 1));
            return Mathf.Clamp(count, 0, sinkTrapMaxCount);
        }

        public int EvaluatePushTrapCount(int depth)
        {
            int count = Mathf.FloorToInt(pushTrapBaseCount + pushTrapPerDepth * (depth - 1));
            return Mathf.Clamp(count, 0, pushTrapMaxCount);
        }

        public static DepthCurve CreateDefault()
        {
            DepthCurve curve = CreateInstance<DepthCurve>();
            curve.name = "DepthCurve (Default)";
            return curve;
        }
    }
}
