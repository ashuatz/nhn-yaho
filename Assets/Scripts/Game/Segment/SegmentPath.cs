using System.Collections.Generic;
using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 길 카테고리 공용 지오메트리 (M4-1 요소 4분리).
    /// 보행 바닥 스트립 생성 - 런타임(SegmentSpawner.Path)과
    /// 사전 배치(SegmentEnvironment.BuildGameObjects, 에디터 윈도우) 공용.
    /// </summary>
    public static class SegmentPath
    {
        const float FloorStripDepth = 2f;

        /// <summary>
        /// 보행로 바닥. 콜라이더가 필요해 GameObject로 만들되, 붕괴 단위인
        /// z 스트립으로 분할한다 (ADR-0006). 스트립 목록을 돌려준다.
        /// </summary>
        public static List<FloorStrip> BuildWalkFloorStrips(Transform parent, SegmentDefinition definition)
        {
            float length = definition.lengthMeters;
            float halfWidth = definition.corridorHalfWidth;

            List<FloorStrip> strips = new List<FloorStrip>(Mathf.CeilToInt(length / FloorStripDepth));

            for (float z = 0f; z < length; z += FloorStripDepth)
            {
                float depth = Mathf.Min(FloorStripDepth, length - z);

                GameObject stripObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stripObject.name = "FloorStrip";
                stripObject.transform.SetParent(parent, false);
                stripObject.transform.localScale = new Vector3(halfWidth * 2f + 1f, 0.2f, depth);
                stripObject.transform.localPosition = new Vector3(0f, -0.1f, z + depth * 0.5f);

                FloorStrip strip = stripObject.AddComponent<FloorStrip>();
                strip.depthMeters = depth;

                SegmentEnvironment.TintGameObject(stripObject, new Color(0.3f, 0.31f, 0.33f));
                strips.Add(strip);
            }

            return strips;
        }
    }
}
