using System.Collections.Generic;
using Scavenger.Loot;
using Scavenger.Obstacle;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 구간 그레이박스 생성/제거의 단일 경계. 생성 방식(Instantiate/Destroy)을
    /// 이 클래스 뒤에 숨겨 추후 풀링 교체가 가능하게 한다 (구현계획 v0.0.2).
    /// 구간 끝의 ChoiceNode에서 전진을 고르면 다음 구간을 이어 붙이고
    /// 뒤쪽 구간을 제거한다 (동시 생존 최대 2개).
    /// </summary>
    public sealed class SegmentSpawner : MonoBehaviour
    {
        public SegmentDefinition Definition { get; private set; }
        public DepthCurve Curve { get; private set; }

        sealed class SegmentRecord
        {
            public GameObject Root;
            public float EndZ;
        }

        readonly List<SegmentRecord> aliveSegments = new List<SegmentRecord>();
        List<LootDefinition> lootCatalog;

        const int LootSpotsPerSegment = 8;
        const float LootInteractRadius = 1.4f;
        const float BombDetectionRadius = 3.5f;

        // 전 레인 봉쇄 금지: 같은 z 구간에 폭탄이 겹치지 않도록 최소 간격 강제.
        // 폭발 반경 상한(DepthCurve.blastMaxRadius) x 2 < 복도 폭이라 단일 폭탄은
        // 전체를 막을 수 없고, z 간격을 두면 이중 봉쇄도 불가능하다.
        const float BombMinZGap = 6f;

        public void Configure(SegmentDefinition definition, List<LootDefinition> catalog, DepthCurve curve)
        {
            Definition = definition;
            lootCatalog = catalog;
            Curve = curve;
        }

        /// <summary>startZ부터 시작하는 구간 하나를 만들고 루트를 돌려준다.</summary>
        public GameObject BuildSegment(int depth, float startZ)
        {
            if (Definition == null)
                Definition = SegmentDefinition.CreateDefault();

            if (Curve == null)
                Curve = DepthCurve.CreateDefault();

            GameObject root = new GameObject($"Segment_depth{depth}");
            root.transform.SetParent(transform);
            root.transform.position = new Vector3(0f, 0f, startZ);

            BuildShell(root.transform);
            PopulateLoot(root.transform, depth);
            PopulateBombs(root.transform, depth);
            BuildChoiceNode(root.transform, depth);
            BuildSignalEmitters(root.transform);

            aliveSegments.Add(new SegmentRecord
            {
                Root = root,
                EndZ = startZ + Definition.lengthMeters,
            });

            return root;
        }

        public void DespawnAll()
        {
            foreach (SegmentRecord segment in aliveSegments)
                DespawnSegment(segment);

            aliveSegments.Clear();
        }

        /// <summary>endZ가 기준보다 뒤인 구간을 제거한다 (지나간 구간 정리).</summary>
        public void DespawnBehind(float z)
        {
            for (int i = aliveSegments.Count - 1; i >= 0; i--)
            {
                SegmentRecord segment = aliveSegments[i];

                if (segment.EndZ >= z)
                    continue;

                DespawnSegment(segment);
                aliveSegments.RemoveAt(i);
            }
        }

        static void DespawnSegment(SegmentRecord segment)
        {
            if (segment.Root == null)
                return;

            // Destroy는 프레임 끝까지 지연되므로 먼저 비활성화해
            // 이전 런의 LootSpot/ChoiceNode가 같은 프레임에 동작하지 못하게 한다 (Codex 검토 반영)
            segment.Root.SetActive(false);
            Destroy(segment.Root);
        }

        // -- 선택지 노드 -----------------------------------------------------

        void BuildChoiceNode(Transform parent, int depth)
        {
            float length = Definition.lengthMeters;
            float halfWidth = Definition.corridorHalfWidth;

            GameObject nodeObject = new GameObject("ChoiceNode");
            nodeObject.transform.SetParent(parent, false);
            nodeObject.transform.localPosition = new Vector3(0f, 0f, length - 1.5f);

            BoxCollider trigger = nodeObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(halfWidth * 2f, 3f, 1.5f);
            trigger.center = new Vector3(0f, 1.5f, 0f);

            ChoiceNode node = nodeObject.AddComponent<ChoiceNode>();
            node.Initialize(OnAdvanceChosen, OnExtractChosen);

            // 시간 초과 = 탈출 잠금 (즉사 아님, 구현계획 v0.0.2 섹션 0.2)
            node.IsExtractionLocked = IsExtractionLocked;

            BuildChoiceVisual(nodeObject.transform, halfWidth);
        }

        static bool IsExtractionLocked()
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return false;

            return run.Timer.IsExpired;
        }

        // -- 거리 신호 -------------------------------------------------------

        void BuildSignalEmitters(Transform parent)
        {
            float length = Definition.lengthMeters;
            float nodeZ = length - 1.5f;

            // 구간의 30% / 60% / 85% 지점 통과 시 신호 발행
            float[] fractions = { 0.3f, 0.6f, 0.85f };

            foreach (float fraction in fractions)
            {
                float z = length * fraction;

                GameObject emitterObject = new GameObject($"SignalEmitter_{fraction:F2}");
                emitterObject.transform.SetParent(parent, false);
                emitterObject.transform.localPosition = new Vector3(0f, 0f, z);

                SignalEmitter emitter = emitterObject.AddComponent<SignalEmitter>();
                emitter.Initialize(nodeZ - z, Definition.corridorHalfWidth * 2f);
            }
        }

        void OnAdvanceChosen(ChoiceNode node)
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return;

            run.AdvanceDepth();

            float nextStartZ = node.transform.position.z + 1.5f;
            BuildSegment(run.Depth, nextStartZ);

            // 방금 끝난 구간(플레이어 발밑)은 남기고 그보다 뒤만 제거
            DespawnBehind(nextStartZ - 0.5f);
        }

        static void OnExtractChosen(ChoiceNode node)
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return;

            run.CompleteExtraction();
        }

        static void BuildChoiceVisual(Transform parent, float halfWidth)
        {
            // 바닥 스트립: 선택 지점 표시
            GameObject strip = CreateBlock(parent, "Strip");
            strip.transform.localScale = new Vector3(halfWidth * 2f, 0.05f, 1.2f);
            strip.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            Tint(strip, new Color(0.9f, 0.85f, 0.3f));

            Collider stripCollider = strip.GetComponent<Collider>();

            if (stripCollider != null)
                Destroy(stripCollider);

            // 좌우 기둥
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject pillar = CreateBlock(parent, side < 0 ? "PillarLeft" : "PillarRight");
                pillar.transform.localScale = new Vector3(0.4f, 2.6f, 0.4f);
                pillar.transform.localPosition = new Vector3(side * (halfWidth - 0.3f), 1.3f, 0f);
                Tint(pillar, new Color(0.9f, 0.85f, 0.3f));

                Collider pillarCollider = pillar.GetComponent<Collider>();

                if (pillarCollider != null)
                    Destroy(pillarCollider);
            }
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
            float[] tierWeights = Curve.EvaluateTierWeights(depth);

            for (int i = 0; i < LootSpotsPerSegment; i++)
            {
                LootDefinition definition = PickLoot(run.Rng, tierWeights);

                // 파밍 요소는 길 가장자리에 배치 - 주우러 가는 좌우 이동이 리스크가 되게
                float side = run.Rng.Next(0, 2) == 0 ? -1f : 1f;
                float edgeMin = halfWidth * 0.45f;
                float edgeMax = halfWidth - 0.6f;
                float x = side * Mathf.Lerp(edgeMin, edgeMax, (float)run.Rng.NextDouble());
                float z = Mathf.Lerp(8f, length - 8f, (float)run.Rng.NextDouble());

                SpawnLootSpot(parent, definition, new Vector3(x, 0f, z));
            }
        }

        LootDefinition PickLoot(System.Random rng, float[] tierWeights)
        {
            float roll = (float)rng.NextDouble();
            float accumulated = 0f;

            for (int tierIndex = 0; tierIndex < tierWeights.Length; tierIndex++)
            {
                accumulated += tierWeights[tierIndex];

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

        // -- 폭탄 배치 -------------------------------------------------------

        void PopulateBombs(Transform parent, int depth)
        {
            RunManager run = RunManager.Instance;

            if (run == null || run.Rng == null)
                return;

            float length = Definition.lengthMeters;
            float halfWidth = Definition.corridorHalfWidth;

            int bombCount = Curve.EvaluateBombCount(depth);
            float fuseSeconds = Curve.EvaluateFuseSeconds(depth);
            float blastRadius = Curve.EvaluateBlastRadius(depth);

            // 전 레인 봉쇄 금지를 데이터 조합과 무관하게 강제:
            // 폭발 지름이 복도 폭을 넘지 못하게 런타임 클램프 (Codex 검토 반영)
            float maxSafeRadius = halfWidth - 0.6f;

            if (blastRadius > maxSafeRadius)
            {
                UnityEngine.Debug.LogWarning(
                    $"[Segment] Blast radius {blastRadius:F2} clamped to {maxSafeRadius:F2} (corridor safety)");
                blastRadius = maxSafeRadius;
            }

            // z 간격도 반경에 비례해 동적으로 - 인접 폭탄의 이중 봉쇄 방지
            float minZGap = Mathf.Max(BombMinZGap, blastRadius * 2f + 1.5f);

            List<float> placedZ = new List<float>();
            int attempts = 0;
            int maxAttempts = bombCount * 10;

            while (placedZ.Count < bombCount && attempts < maxAttempts)
            {
                attempts += 1;

                // 초입은 비워서 스폰/진입 직후 즉사 방지
                float z = Mathf.Lerp(14f, length - 6f, (float)run.Rng.NextDouble());

                if (!IsZGapValid(placedZ, z, minZGap))
                    continue;

                float x = Mathf.Lerp(-halfWidth + 0.8f, halfWidth - 0.8f, (float)run.Rng.NextDouble());

                SpawnBomb(parent, new Vector3(x, 0f, z), fuseSeconds, blastRadius);
                placedZ.Add(z);
            }
        }

        static bool IsZGapValid(List<float> placedZ, float z, float minZGap)
        {
            foreach (float existing in placedZ)
            {
                if (Mathf.Abs(existing - z) < minZGap)
                    return false;
            }

            return true;
        }

        static void SpawnBomb(Transform parent, Vector3 localPosition, float fuseSeconds, float blastRadius)
        {
            GameObject bombObject = new GameObject("Bomb");
            bombObject.transform.SetParent(parent, false);
            bombObject.transform.localPosition = localPosition;

            Bomb bomb = bombObject.AddComponent<Bomb>();
            bomb.Initialize(BombDetectionRadius, fuseSeconds, blastRadius);
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
