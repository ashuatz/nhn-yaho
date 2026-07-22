using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 구간 형태 데이터. 2-3분 라운드 템포는 길이 x 전진 속도로 결정되므로
    /// 템포 튜닝은 이 에셋에서 한다 (구현계획 v0.0.2 섹션 2.3 템포 계측).
    /// </summary>
    [CreateAssetMenu(menuName = "Scavenger/Segment Definition", fileName = "SegmentDefinition")]
    public sealed class SegmentDefinition : ScriptableObject
    {
        [Header("구간 형태")]
        public float lengthMeters = 80f;

        // 3.5 -> 5.25: 플레이 영역 1.5배 확장 (사용자 지시, 2026-07-22)
        public float corridorHalfWidth = 5.25f;

        public float wallHeight = 2f;

        public static SegmentDefinition CreateDefault()
        {
            SegmentDefinition definition = CreateInstance<SegmentDefinition>();
            definition.name = "SegmentDefinition (Default)";
            return definition;
        }
    }
}
