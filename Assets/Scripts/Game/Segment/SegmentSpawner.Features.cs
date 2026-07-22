using System.Collections.Generic;
using Scavenger.Loot;
using Scavenger.Obstacle;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// SegmentSpawner - 기능 카테고리 (M4-1 요소 4분리).
    /// 루트, 폭탄, 트랩(땅 꺼짐/밀기), 선택지, 거리 신호 등 상호작용 요소 배치.
    /// 길(바닥/단차)은 Path 파일, 장식은 SegmentEnvironment, 코어 조율은 본 파일 참조.
    /// </summary>
    public sealed partial class SegmentSpawner
    {
        [Header("땅 꺼짐 트랩 (M3-2, 프리팹 튜닝 지점)")]
        public float sinkTrapTriggerDistance = 5.5f;
        public float sinkTrapWarnSeconds = 1.1f;
        [Range(0f, 1f)] public float sinkTrapTremorMax = 0.4f;

        [Header("밀기 트랩 (M3-3, 프리팹 튜닝 지점)")]
        public float pushTrapDetectionRadius = 2.2f;
        public float pushTrapTelegraphSeconds = 0.45f;
        public float pushTrapSpeed = 7.5f;
        public float pushTrapCooldownSeconds = 3.5f;

        const int LootSpotsPerSegment = 8;
        const float LootInteractRadius = 1.4f;
        const float BombDetectionRadius = 3.5f;

        // 전 레인 봉쇄 금지: 같은 z 구간에 폭탄이 겹치지 않도록 최소 간격 강제.
        // 폭발 반경 상한(DepthCurve.blastMaxRadius) x 2 < 복도 폭이라 단일 폭탄은
        // 전체를 막을 수 없고, z 간격을 두면 이중 봉쇄도 불가능하다.
        const float BombMinZGap = 6f;

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
            // 탈출 잠금 규칙은 제거됨 - 압박은 바닥 붕괴가 담당 (ADR-0006)

            BuildChoiceVisual(nodeObject.transform, halfWidth);
        }

        void OnAdvanceChosen(ChoiceNode node)
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return;

            run.AdvanceDepth();

            // 플레이어가 들어설 다음 구간(run.Depth)은 룩어헤드로 이미 존재.
            // 그 다음 구간을 미리 지어 선노출을 유지한다 (ADR-0004)
            BuildSegment(run.Depth + 1, TailEndZ);

            // 방금 끝난 구간(플레이어 발밑)은 남기고 그보다 뒤만 제거
            float nextStartZ = node.transform.position.z + 1.5f;
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

            int spawned = 0;
            int attempts = 0;

            while (spawned < LootSpotsPerSegment && attempts < LootSpotsPerSegment * 8)
            {
                attempts += 1;

                // 파밍 요소는 길 가장자리에 배치 - 주우러 가는 좌우 이동이 리스크가 되게
                float side = run.Rng.Next(0, 2) == 0 ? -1f : 1f;
                float edgeMin = halfWidth * 0.45f;
                float edgeMax = halfWidth - 0.6f;
                float x = side * Mathf.Lerp(edgeMin, edgeMax, (float)run.Rng.NextDouble());
                float z = Mathf.Lerp(8f, length - 8f, (float)run.Rng.NextDouble());

                // 단차 내부(바닥 레벨)에 묻히는 배치 방지 - 단차 위 루트는 BuildLedge가 배치
                if (IsInsideLedge(x, z, margin: 0.5f))
                    continue;

                LootDefinition definition = PickLoot(run.Rng, tierWeights);
                LootSpot spot = SpawnLootSpot(parent, definition, new Vector3(x, 0f, z));

                // 바닥이 가라앉으면 위 요소도 함께 - 공중에 뜬 루트가 인접 스트립에서
                // 상호작용 가능해지는 것 방지 (Codex 교차 검토)
                AttachToSupportingStrip(spot.transform);

                spawned += 1;
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

        LootSpot SpawnLootSpot(Transform parent, LootDefinition definition, Vector3 localPosition)
        {
            GameObject spotObject = new GameObject($"LootSpot_{definition.id}");
            spotObject.transform.SetParent(parent, false);
            spotObject.transform.localPosition = localPosition;

            LootSpot spot = spotObject.AddComponent<LootSpot>();
            spot.Initialize(definition, LootInteractRadius);

            BuildLootVisual(spotObject.transform, definition.tier);
            return spot;
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

                // 단차 아래/내부에 숨는 폭탄 방지 + 단차 z구간 전체 제외:
                // 옆 통로 폭 < 폭발 지름이면 사실상 봉쇄가 되므로 (검증 반영)
                if (IsInLedgeZRange(z, margin: 0.8f))
                    continue;

                GameObject bombObject = SpawnBomb(parent, new Vector3(x, 0f, z), fuseSeconds, blastRadius);

                // 루트와 동일 규칙 - 바닥 침몰 시 폭탄도 함께 사라진다 (Codex 교차 검토)
                AttachToSupportingStrip(bombObject.transform);

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

        static GameObject SpawnBomb(Transform parent, Vector3 localPosition, float fuseSeconds, float blastRadius)
        {
            GameObject bombObject = new GameObject("Bomb");
            bombObject.transform.SetParent(parent, false);
            bombObject.transform.localPosition = localPosition;

            Bomb bomb = bombObject.AddComponent<Bomb>();
            bomb.Initialize(BombDetectionRadius, fuseSeconds, blastRadius);

            return bombObject;
        }

        // -- 땅 꺼짐 트랩 배치 (M3-2) ------------------------------------------

        void PopulateSinkTraps(int depth)
        {
            RunManager run = RunManager.Instance;

            if (run == null || run.Rng == null || currentStrips.Count == 0)
                return;

            int trapCount = Curve.EvaluateSinkTrapCount(depth);

            if (trapCount <= 0)
                return;

            float length = Definition.lengthMeters;
            int placed = 0;
            int attempts = 0;

            while (placed < trapCount && attempts < trapCount * 10)
            {
                attempts += 1;

                FloorStrip strip = currentStrips[run.Rng.Next(0, currentStrips.Count)];
                float localZ = strip.transform.localPosition.z;

                // 초입(진입 직후 낙사 방지)과 선택지 앞은 비운다
                if (localZ < 12f || localZ > length - 8f)
                    continue;

                // 단차 진입로를 무너뜨리면 보상 동선이 사실상 봉쇄된다 - 제외
                if (IsInLedgeZRange(localZ, margin: 1f))
                    continue;

                if (strip.GetComponent<SinkTrap>() != null)
                    continue;

                SinkTrap trap = strip.gameObject.AddComponent<SinkTrap>();
                trap.triggerDistance = sinkTrapTriggerDistance;
                trap.warnSeconds = sinkTrapWarnSeconds;
                trap.warnTremorMax = sinkTrapTremorMax;

                placed += 1;
            }
        }

        // -- 밀기 트랩 배치 (M3-3) ---------------------------------------------

        void PopulatePushTraps(Transform parent, int depth)
        {
            RunManager run = RunManager.Instance;

            if (run == null || run.Rng == null)
                return;

            int trapCount = Curve.EvaluatePushTrapCount(depth);

            if (trapCount <= 0)
                return;

            float length = Definition.lengthMeters;
            float halfWidth = Definition.corridorHalfWidth;

            List<float> placedZ = new List<float>();
            int attempts = 0;

            while (placedZ.Count < trapCount && attempts < trapCount * 10)
            {
                attempts += 1;

                float z = Mathf.Lerp(16f, length - 10f, (float)run.Rng.NextDouble());

                // 트랩끼리 겹치면 연속 밀림으로 즉사 콤보가 되므로 간격 강제
                if (!IsZGapValid(placedZ, z, minZGap: 10f))
                    continue;

                if (IsInLedgeZRange(z, margin: 1f))
                    continue;

                float x = Mathf.Lerp(-halfWidth + 0.8f, halfWidth - 0.8f, (float)run.Rng.NextDouble());

                GameObject trapObject = new GameObject("PushTrap");
                trapObject.transform.SetParent(parent, false);
                trapObject.transform.localPosition = new Vector3(x, 0f, z);

                PushTrap trap = trapObject.AddComponent<PushTrap>();
                trap.Initialize(
                    pushTrapDetectionRadius, pushTrapTelegraphSeconds,
                    pushTrapSpeed, pushTrapCooldownSeconds);

                // 바닥과 함께 침몰 (루트/폭탄과 동일 규칙)
                AttachToSupportingStrip(trapObject.transform);

                placedZ.Add(z);
            }
        }
    }
}
