using System.Collections.Generic;
using Scavenger.Loot;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 구간 그레이박스 생성/제거의 단일 경계. 생성 방식(Instantiate/Destroy)을
    /// 이 클래스 뒤에 숨겨 추후 풀링 교체가 가능하게 한다 (구현계획 v0.0.2).
    /// S2: 바닥+벽 1구간. S3+: 루트/폭탄/신호 배치, S5: 체인/제거.
    /// </summary>
    public sealed class SegmentSpawner : MonoBehaviour
    {
        public SegmentDefinition Definition { get; private set; }

        readonly List<GameObject> aliveSegments = new List<GameObject>();
        List<LootDefinition> lootCatalog;

        // S3 임시 고정값. S5에서 DepthCurve로 대체
        const int LootSpotsPerSegment = 8;
        const float LootInteractRadius = 1.4f;
        static readonly float[] TierWeights = { 0.7f, 0.25f, 0.05f };

        public void Configure(SegmentDefinition definition, List<LootDefinition> catalog)
        {
            Definition = definition;
            lootCatalog = catalog;
        }

        /// <summary>startZ부터 시작하는 구간 하나를 만들고 루트를 돌려준다.</summary>
        public GameObject BuildSegment(int depth, float startZ)
        {
            if (Definition == null)
                Definition = SegmentDefinition.CreateDefault();

            GameObject root = new GameObject($"Segment_depth{depth}");
            root.transform.SetParent(transform);
            root.transform.position = new Vector3(0f, 0f, startZ);

            BuildShell(root.transform);
            PopulateLoot(root.transform, depth);

            aliveSegments.Add(root);
            return root;
        }

        public void DespawnAll()
        {
            foreach (GameObject segment in aliveSegments)
            {
                if (segment != null)
                    Destroy(segment);
            }

            aliveSegments.Clear();
        }

        // -- 그레이박스 셸 -------------------------------------------------

        void BuildShell(Transform parent)
        {
            float length = Definition.lengthMeters;
            float halfWidth = Definition.corridorHalfWidth;
            float wallHeight = Definition.wallHeight;

            // 바닥: 윗면이 y=0에 오도록
            GameObject floor = CreateBlock(parent, "Floor");
            floor.transform.localScale = new Vector3(halfWidth * 2f + 1f, 0.2f, length);
            floor.transform.localPosition = new Vector3(0f, -0.1f, length * 0.5f);
            Tint(floor, new Color(0.35f, 0.35f, 0.38f));

            // 좌우 가벽
            GameObject leftWall = CreateBlock(parent, "WallLeft");
            leftWall.transform.localScale = new Vector3(0.5f, wallHeight, length);
            leftWall.transform.localPosition = new Vector3(-(halfWidth + 0.25f), wallHeight * 0.5f, length * 0.5f);
            Tint(leftWall, new Color(0.25f, 0.25f, 0.3f));

            GameObject rightWall = CreateBlock(parent, "WallRight");
            rightWall.transform.localScale = new Vector3(0.5f, wallHeight, length);
            rightWall.transform.localPosition = new Vector3(halfWidth + 0.25f, wallHeight * 0.5f, length * 0.5f);
            Tint(rightWall, new Color(0.25f, 0.25f, 0.3f));
        }

        // -- 루트 배치 -------------------------------------------------------

        void PopulateLoot(Transform parent, int depth)
        {
            if (lootCatalog == null || lootCatalog.Count == 0)
                return;

            RunManager run = RunManager.Instance;

            if (run == null || run.Rng == null)
                return;

            float length = Definition.lengthMeters;
            float halfWidth = Definition.corridorHalfWidth;

            for (int i = 0; i < LootSpotsPerSegment; i++)
            {
                LootDefinition definition = PickLoot(run.Rng);

                // 파밍 요소는 길 가장자리에 배치 - 주우러 가는 좌우 이동이 리스크가 되게
                float side = run.Rng.Next(0, 2) == 0 ? -1f : 1f;
                float edgeMin = halfWidth * 0.45f;
                float edgeMax = halfWidth - 0.6f;
                float x = side * Mathf.Lerp(edgeMin, edgeMax, (float)run.Rng.NextDouble());
                float z = Mathf.Lerp(8f, length - 8f, (float)run.Rng.NextDouble());

                SpawnLootSpot(parent, definition, new Vector3(x, 0f, z));
            }
        }

        LootDefinition PickLoot(System.Random rng)
        {
            // S3: 고정 tier 가중치. S5에서 깊이 기반 DepthCurve로 대체
            float roll = (float)rng.NextDouble();
            float accumulated = 0f;

            for (int tierIndex = 0; tierIndex < TierWeights.Length; tierIndex++)
            {
                accumulated += TierWeights[tierIndex];

                if (roll <= accumulated)
                    return FindByTier(tierIndex + 1);
            }

            return lootCatalog[0];
        }

        LootDefinition FindByTier(int tier)
        {
            foreach (LootDefinition definition in lootCatalog)
            {
                if (definition.tier == tier)
                    return definition;
            }

            return lootCatalog[0];
        }

        void SpawnLootSpot(Transform parent, LootDefinition definition, Vector3 localPosition)
        {
            GameObject spotObject = new GameObject($"LootSpot_{definition.id}");
            spotObject.transform.SetParent(parent, false);
            spotObject.transform.localPosition = localPosition;

            LootSpot spot = spotObject.AddComponent<LootSpot>();
            spot.Initialize(definition, LootInteractRadius);

            BuildLootVisual(spotObject.transform, definition.tier);
        }

        static void BuildLootVisual(Transform parent, int tier)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Visual";
            cube.transform.SetParent(parent, false);
            cube.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            cube.transform.localPosition = new Vector3(0f, 0.3f, 0f);

            // 비주얼 전용 - 상호작용 판정은 루트의 SphereCollider 트리거가 담당
            Collider cubeCollider = cube.GetComponent<Collider>();

            if (cubeCollider != null)
                Destroy(cubeCollider);

            Tint(cube, TierColor(tier));
        }

        static Color TierColor(int tier)
        {
            if (tier >= 3)
                return new Color(0.95f, 0.8f, 0.2f);

            if (tier == 2)
                return new Color(0.6f, 0.7f, 0.85f);

            return new Color(0.7f, 0.55f, 0.3f);
        }

        static GameObject CreateBlock(Transform parent, string blockName)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = blockName;
            block.transform.SetParent(parent, false);
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
