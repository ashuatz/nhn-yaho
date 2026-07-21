using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 구간 그레이박스 환경 PCG (ADR-0003 - 공간감 검증용).
    /// 평탄한 보행로 양옆으로 복셀풍 럽블 매스를 쌓아 골목/폐허 협곡감을 만든다.
    /// 보행 영역은 항상 y=0 평면 유지 (CharacterController 단순성).
    /// 모든 난수는 주입된 rng(런 시드) 사용 - 같은 시드 = 같은 지형.
    /// </summary>
    public static class SegmentEnvironment
    {
        const float RubbleSliceDepth = 2.2f;

        public static void Build(Transform parent, SegmentDefinition definition, System.Random rng)
        {
            BuildWalkFloor(parent, definition);
            BuildRubbleSides(parent, definition, rng);
            BuildUpperStory(parent, definition, rng);
            BuildDebris(parent, definition, rng);
        }

        // -- 보행로 ---------------------------------------------------------

        static void BuildWalkFloor(Transform parent, SegmentDefinition definition)
        {
            float length = definition.lengthMeters;
            float halfWidth = definition.corridorHalfWidth;

            GameObject floor = CreateBlock(parent, "Floor", withCollider: true);
            floor.transform.localScale = new Vector3(halfWidth * 2f + 1f, 0.2f, length);
            floor.transform.localPosition = new Vector3(0f, -0.1f, length * 0.5f);
            Tint(floor, new Color(0.3f, 0.31f, 0.33f));
        }

        // -- 측면 럽블 매스 ---------------------------------------------------

        static void BuildRubbleSides(Transform parent, SegmentDefinition definition, System.Random rng)
        {
            float length = definition.lengthMeters;
            float halfWidth = definition.corridorHalfWidth;

            GameObject rubbleRoot = new GameObject("Rubble");
            rubbleRoot.transform.SetParent(parent, false);

            for (int side = -1; side <= 1; side += 2)
            {
                // 높이를 이전 슬라이스와 보간해 들쭉날쭉하되 연속적인 능선을 만든다
                float previousHeight = NextRange(rng, 1.2f, 2.8f);

                for (float z = 0f; z < length; z += RubbleSliceDepth)
                {
                    float targetHeight = NextRange(rng, 0.6f, 3.6f);
                    float height = Mathf.Lerp(previousHeight, targetHeight, 0.55f);
                    previousHeight = height;

                    SpawnRubbleBlock(rubbleRoot.transform, rng, side, halfWidth, z, height, rowOffset: 0f);

                    // 바깥 두 번째 열 - 더 높게 쌓아 협곡 실루엣 강조
                    if (rng.NextDouble() < 0.45)
                    {
                        float backHeight = height + NextRange(rng, 0.8f, 2.4f);
                        SpawnRubbleBlock(rubbleRoot.transform, rng, side, halfWidth, z, backHeight, rowOffset: 2.1f);
                    }

                    // 드물게 랜드마크 기둥 - 원경에서 진행 방향 가늠용
                    if (rng.NextDouble() < 0.06)
                    {
                        float pillarHeight = NextRange(rng, 4.5f, 7f);
                        SpawnRubbleBlock(rubbleRoot.transform, rng, side, halfWidth, z, pillarHeight, rowOffset: 1f);
                    }
                }
            }
        }

        static void SpawnRubbleBlock(
            Transform parent, System.Random rng, int side, float halfWidth, float z, float height, float rowOffset)
        {
            float width = NextRange(rng, 1.5f, 2.5f);
            float x = side * (halfWidth + width * 0.5f + 0.15f + rowOffset);

            GameObject block = CreateBlock(parent, "RubbleBlock", withCollider: true);
            block.transform.localScale = new Vector3(width, height, RubbleSliceDepth * NextRange(rng, 0.85f, 1.05f));
            block.transform.localPosition = new Vector3(x, height * 0.5f - 0.1f, z + RubbleSliceDepth * 0.5f);

            float jitter = (float)rng.NextDouble() * 0.06f;
            Tint(block, new Color(0.16f + jitter, 0.18f + jitter, 0.22f + jitter));
        }

        // -- 상부층 (복층 느낌) ------------------------------------------------

        static void BuildUpperStory(Transform parent, SegmentDefinition definition, System.Random rng)
        {
            float length = definition.lengthMeters;
            float halfWidth = definition.corridorHalfWidth;

            GameObject upperRoot = new GameObject("UpperStory");
            upperRoot.transform.SetParent(parent, false);

            // 측면 상부 플랫폼: 복도 안쪽으로 오버행 - 2층 발코니/통로 느낌
            for (int side = -1; side <= 1; side += 2)
            {
                float z = NextRange(rng, 4f, 10f);

                while (z < length - 9f)
                {
                    if (rng.NextDouble() < 0.6)
                        SpawnPlatform(upperRoot.transform, rng, side, halfWidth, z);

                    z += NextRange(rng, 9f, 16f);
                }
            }

            // 브릿지: 복도를 가로지르는 상부 통로 - 플레이어 머리 위를 지나간다
            float bridgeZ = NextRange(rng, 15f, 30f);

            while (bridgeZ < length - 10f)
            {
                if (rng.NextDouble() < 0.55)
                    SpawnBridge(upperRoot.transform, rng, halfWidth, bridgeZ);

                bridgeZ += NextRange(rng, 22f, 40f);
            }
        }

        static void SpawnPlatform(Transform parent, System.Random rng, int side, float halfWidth, float z)
        {
            float width = NextRange(rng, 3f, 4.5f);
            float depth = NextRange(rng, 5f, 9f);
            float height = NextRange(rng, 3.1f, 3.9f);
            float overhang = NextRange(rng, 0.8f, 1.6f);

            float innerX = halfWidth - overhang;
            float centerX = side * (innerX + width * 0.5f);
            float centerZ = z + depth * 0.5f;

            GameObject slab = CreateBlock(parent, "PlatformSlab", withCollider: true);
            slab.transform.localScale = new Vector3(width, 0.35f, depth);
            slab.transform.localPosition = new Vector3(centerX, height, centerZ);
            Tint(slab, UpperTone(rng));

            // 안쪽 모서리 지지 기둥
            GameObject pillar = CreateBlock(parent, "PlatformPillar", withCollider: true);
            pillar.transform.localScale = new Vector3(0.35f, height, 0.35f);
            pillar.transform.localPosition = new Vector3(side * (innerX + 0.2f), height * 0.5f, centerZ);
            Tint(pillar, UpperTone(rng));

            // 플랫폼 위 잡동사니 실루엣
            if (rng.NextDouble() < 0.6)
            {
                float propSize = NextRange(rng, 0.5f, 1.1f);
                GameObject prop = CreateBlock(parent, "PlatformProp", withCollider: false);
                prop.transform.localScale = new Vector3(propSize, propSize, propSize);
                prop.transform.localPosition = new Vector3(
                    centerX + NextRange(rng, -width * 0.3f, width * 0.3f),
                    height + 0.175f + propSize * 0.5f,
                    centerZ + NextRange(rng, -depth * 0.3f, depth * 0.3f));
                Tint(prop, UpperTone(rng));
            }
        }

        static void SpawnBridge(Transform parent, System.Random rng, float halfWidth, float z)
        {
            float height = NextRange(rng, 3.4f, 4f);
            float depth = NextRange(rng, 2.2f, 3.2f);

            GameObject bridge = CreateBlock(parent, "Bridge", withCollider: true);
            bridge.transform.localScale = new Vector3(halfWidth * 2f + 5f, 0.4f, depth);
            bridge.transform.localPosition = new Vector3(0f, height, z + depth * 0.5f);
            Tint(bridge, UpperTone(rng));
        }

        static Color UpperTone(System.Random rng)
        {
            float jitter = (float)rng.NextDouble() * 0.05f;
            return new Color(0.2f + jitter, 0.22f + jitter, 0.27f + jitter);
        }

        // -- 보행로 내 데브리 (비주얼 전용) -----------------------------------

        static void BuildDebris(Transform parent, SegmentDefinition definition, System.Random rng)
        {
            float length = definition.lengthMeters;
            float halfWidth = definition.corridorHalfWidth;

            GameObject debrisRoot = new GameObject("Debris");
            debrisRoot.transform.SetParent(parent, false);

            int count = Mathf.RoundToInt(length / 6f);

            for (int i = 0; i < count; i++)
            {
                float side = rng.Next(0, 2) == 0 ? -1f : 1f;
                float x = side * NextRange(rng, halfWidth * 0.55f, halfWidth * 0.95f);
                float z = NextRange(rng, 3f, length - 3f);
                float size = NextRange(rng, 0.25f, 0.55f);

                GameObject debris = CreateBlock(debrisRoot.transform, "Debris", withCollider: false);
                debris.transform.localScale = new Vector3(size, size, size);
                debris.transform.localPosition = new Vector3(x, size * 0.5f, z);
                debris.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 90f, 0f);

                float jitter = (float)rng.NextDouble() * 0.05f;
                Tint(debris, new Color(0.28f + jitter, 0.28f + jitter, 0.3f + jitter));
            }
        }

        // -- 공통 -----------------------------------------------------------

        static float NextRange(System.Random rng, float min, float max)
        {
            return Mathf.Lerp(min, max, (float)rng.NextDouble());
        }

        static GameObject CreateBlock(Transform parent, string blockName, bool withCollider)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = blockName;
            block.transform.SetParent(parent, false);

            if (!withCollider)
            {
                Collider blockCollider = block.GetComponent<Collider>();

                if (blockCollider != null)
                    RemoveObject(blockCollider);
            }

            return block;
        }

        // 에디트 모드(사전 배치 윈도우)에서도 호출되므로 Destroy/DestroyImmediate 분기
        static void RemoveObject(Object target)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(target);
                return;
            }

            Object.DestroyImmediate(target);
        }

        static void Tint(GameObject block, Color color)
        {
            Renderer blockRenderer = block.GetComponent<Renderer>();

            if (blockRenderer == null)
                return;

            // 에디트 모드에서 .material 접근은 에러 - 인스턴스를 만들어 교체 (양쪽 모드 공용)
            Material material = new Material(blockRenderer.sharedMaterial);
            material.color = color;
            blockRenderer.sharedMaterial = material;
        }
    }
}
