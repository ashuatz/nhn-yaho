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

        // 바닥 그리드 머티리얼 (웹 이식). Resources/FloorGrid에서 1회 로드해 공유.
        // 스트립마다 인스턴스를 만들어 타일링(1m=1셀)을 스트립 크기에 맞춘다.
        static Material sharedGridMaterial;
        static bool gridLoadAttempted;

        static readonly Color FloorTint = new Color(0.32f, 0.36f, 0.3f);

        // 바닥에 그리드 머티리얼을 입힌다. 셀 1m 유지를 위해 타일링 = (폭, 깊이).
        // 그리드 머티리얼이 없으면(생성 전) 기존 단색 tint로 폴백한다.
        static void ApplyFloorGrid(GameObject stripObject, float width, float depth)
        {
            Material grid = ResolveGridMaterial();

            if (grid == null)
            {
                SegmentEnvironment.TintGameObject(stripObject, FloorTint);
                return;
            }

            Renderer renderer = stripObject.GetComponent<Renderer>();

            if (renderer == null)
                return;

            // 인스턴스 머티리얼 - 스트립별 타일링. 셀 1m = 텍스처 1반복
            Material instance = new Material(grid);
            instance.mainTextureScale = new Vector2(width, depth);

            renderer.sharedMaterial = instance;
        }

        static Material ResolveGridMaterial()
        {
            if (gridLoadAttempted)
                return sharedGridMaterial;

            gridLoadAttempted = true;
            sharedGridMaterial = Resources.Load<Material>("FloorGrid");

            return sharedGridMaterial;
        }

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

                ApplyFloorGrid(stripObject, halfWidth * 2f + 1f, depth);
                strips.Add(strip);
            }

            return strips;
        }
    }
}
