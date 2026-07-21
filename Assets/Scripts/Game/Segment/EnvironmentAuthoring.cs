using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 사전 배치된 배경의 마커. 이 컴포넌트가 커버하는 z 범위에서는
    /// SegmentSpawner가 런타임 배경 생성을 건너뛴다 - 손으로 다듬은 배경 보존.
    /// 생성/재생성은 에디터 윈도우 (Scavenger > Environment Authoring).
    /// </summary>
    public sealed class EnvironmentAuthoring : MonoBehaviour
    {
        [Header("이 배경이 커버하는 z 범위 (윈도우가 기록)")]
        public float coveredFromZ;
        public float coveredToZ;

        public bool Covers(float startZ, float endZ)
        {
            return startZ >= coveredFromZ - 0.01f && endZ <= coveredToZ + 0.01f;
        }
    }
}
