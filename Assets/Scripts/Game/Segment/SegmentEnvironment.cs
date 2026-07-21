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
                    Object.Destroy(blockCollider);
            }

            return block;
        }

        static void Tint(GameObject block, Color color)
        {
            Renderer blockRenderer = block.GetComponent<Renderer>();

            if (blockRenderer == null)
                return;

            blockRenderer.material.color = color;
        }
    }
}
