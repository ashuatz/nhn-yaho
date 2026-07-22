using System.Collections.Generic;
using Scavenger.Loot;
using Scavenger.Obstacle;
using Scavenger.Player;
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
            public int EnvChunkId;
        }

        readonly List<SegmentRecord> aliveSegments = new List<SegmentRecord>();
        List<LootDefinition> lootCatalog;
        EnvironmentAuthoring preplacedEnvironment;
        bool preplacedSearched;
        EnvironmentRenderer environmentRenderer;
        FollowCamera viewCamera;

        /// <summary>지금까지 생성된 구간 체인의 끝 z. 룩어헤드 생성 기준점.</summary>
        public float TailEndZ { get; private set; }

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

        /// <summary>시야 클리어런스 기준 카메라. GameFlow가 배선.</summary>
        public void SetViewCamera(FollowCamera camera)
        {
            viewCamera = camera;
        }

        CollapseFront EnsureCollapseFront()
        {
            CollapseFront collapse = GetComponent<CollapseFront>();

            if (collapse == null)
                collapse = gameObject.AddComponent<CollapseFront>();

            return collapse;
        }

        void RegisterPreplacedStrips(float startZ, float endZ)
        {
            if (preplacedEnvironment == null)
                return;

            FloorStrip[] allStrips = preplacedEnvironment.GetComponentsInChildren<FloorStrip>();
            List<FloorStrip> inRange = new List<FloorStrip>();

            foreach (FloorStrip strip in allStrips)
            {
                if (strip.EndZ > startZ - 0.1f && strip.EndZ <= endZ + 0.1f)
                    inRange.Add(strip);
            }

            // 중복 등록은 CollapseFront가 걸러낸다
            EnsureCollapseFront().RegisterStrips(inRange);
        }

        /// <summary>
        /// 카메라 리그 수치로 시야 라인 클리어런스를 만든다.
        /// 먼 끝점은 복도 반대편 바닥 (플레이어가 -x 끝에 있어도 가리지 않게 보수적).
        /// </summary>
        public static SightClearance BuildSightClearance(FollowCamera camera, SegmentDefinition definition)
        {
            if (camera == null || definition == null)
                return default;

            Vector2 cameraPoint = new Vector2(
                camera.lookAtOffset.x + camera.positionOffsetWorld.x,
                camera.lookAtOffset.y + camera.positionOffsetWorld.y);

            Vector2 farPoint = new Vector2(-definition.corridorHalfWidth, 0f);

            return new SightClearance
            {
                Enabled = true,
                NearPoint = cameraPoint,
                FarPoint = farPoint,
                Margin = 1f,
            };
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

            int envChunkId = BuildShell(root.transform);
            PopulateLedges(root.transform, depth);
            PopulateLoot(root.transform, depth);
            PopulateBombs(root.transform, depth);
            BuildChoiceNode(root.transform, depth);
            BuildSignalEmitters(root.transform);

            float endZ = startZ + Definition.lengthMeters;

            aliveSegments.Add(new SegmentRecord
            {
                Root = root,
                EndZ = endZ,
                EnvChunkId = envChunkId,
            });

            TailEndZ = Mathf.Max(TailEndZ, endZ);
            return root;
        }

        /// <summary>
        /// 라운드 시작용 체인: 현재 구간 + 다음 구간을 함께 생성한다.
        /// 다음 스테이지가 항상 시야에 확정 노출되어 끊김이 없다 (ADR-0004).
        /// </summary>
        public void BuildInitialChain(int depth, float startZ)
        {
            BuildSegment(depth, startZ);
            BuildSegment(depth + 1, TailEndZ);
        }

        public void DespawnAll()
        {
            foreach (SegmentRecord segment in aliveSegments)
                DespawnSegment(segment);

            aliveSegments.Clear();
            TailEndZ = 0f;
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

        void DespawnSegment(SegmentRecord segment)
        {
            if (segment.EnvChunkId != 0 && environmentRenderer != null)
                environmentRenderer.RemoveChunk(segment.EnvChunkId);

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
            // 탈출 잠금 규칙은 제거됨 - 압박은 바닥 붕괴가 담당 (ADR-0006)

            BuildChoiceVisual(nodeObject.transform, halfWidth);
        }

        // -- 수직 요소: 단차 (M1-1, ADR-0006 낙사 연계) -------------------------

        sealed class LedgeRecord
        {
            public float MinX, MaxX, MinZ, MaxZ;
        }

        // 현재 빌드 중인 구간의 단차 점유 영역 (루트/폭탄 배치 제외용)
        readonly List<LedgeRecord> currentLedges = new List<LedgeRecord>();

        const float LedgeRampSlopeRatio = 2.6f;

        void PopulateLedges(Transform parent, int depth)
        {
            currentLedges.Clear();

            RunManager run = RunManager.Instance;

            if (run == null || run.Rng == null)
                return;

            System.Random rng = run.Rng;
            float length = Definition.lengthMeters;
            float halfWidth = Definition.corridorHalfWidth;
            SightClearance clearance = BuildSightClearance(viewCamera, Definition);

            int ledgeCount = rng.NextDouble() < 0.5 ? 2 : 1;
            int attempts = 0;

            while (currentLedges.Count < ledgeCount && attempts < ledgeCount * 8)
            {
                attempts += 1;

                float ledgeWidth = Lerp(rng, 2.4f, 3.4f);
                float ledgeLength = Lerp(rng, 4f, 7f);
                float ledgeHeight = Lerp(rng, 0.7f, 1.1f);
                float rampLength = ledgeHeight * LedgeRampSlopeRatio;

                float side = rng.Next(0, 2) == 0 ? -1f : 1f;
                float centerX = side * (halfWidth - ledgeWidth * 0.5f - 0.2f);
                float startZ = Lerp(rng, 12f + rampLength, length - 14f - ledgeLength);

                Vector3 boxCenter = new Vector3(centerX, ledgeHeight * 0.5f, startZ + ledgeLength * 0.5f);
                Vector3 boxScale = new Vector3(ledgeWidth, ledgeHeight, ledgeLength);

                // 카메라 시야 라인과 겹치면 생성 거부 (배경과 동일 규칙).
                // 상판 위 보상 루트(약 1m 비주얼)까지 포함해 검사 (검증 반영)
                Vector3 lootProbeCenter = new Vector3(centerX, ledgeHeight + 0.5f, boxCenter.z);
                Vector3 lootProbeScale = new Vector3(1f, 1f, 1f);

                if (clearance.Rejects(boxCenter, boxScale) || clearance.Rejects(lootProbeCenter, lootProbeScale))
                    continue;

                if (OverlapsExistingLedgeZ(startZ - rampLength, startZ + ledgeLength, gap: 4f))
                    continue;

                BuildLedge(parent, rng, centerX, startZ, ledgeWidth, ledgeLength, ledgeHeight, rampLength);
            }
        }

        bool OverlapsExistingLedgeZ(float minZ, float maxZ, float gap)
        {
            foreach (LedgeRecord ledge in currentLedges)
            {
                if (maxZ + gap >= ledge.MinZ && minZ - gap <= ledge.MaxZ)
                    return true;
            }

            return false;
        }

        void BuildLedge(
            Transform parent, System.Random rng, float centerX, float startZ,
            float width, float length, float height, float rampLength)
        {
            float totalLength = rampLength + length;
            float featureCenterZ = startZ - rampLength + totalLength * 0.5f;

            GameObject ledgeRoot = new GameObject("Ledge");
            ledgeRoot.transform.SetParent(parent, false);
            ledgeRoot.transform.localPosition = new Vector3(centerX, 0f, featureCenterZ);

            // 상판 (보행 가능)
            GameObject top = CreateBlock(ledgeRoot.transform, "LedgeTop");
            top.transform.localScale = new Vector3(width, height, length);
            top.transform.localPosition = new Vector3(
                0f, height * 0.5f, startZ + length * 0.5f - featureCenterZ);
            Tint(top, new Color(0.36f, 0.36f, 0.39f));

            // 진입 경사로 (-z 쪽에서 올라온다). 기울기 약 21도 - CC 기본 slopeLimit 이내
            float rampAngle = Mathf.Atan2(height, rampLength) * Mathf.Rad2Deg;
            float rampSurfaceLength = Mathf.Sqrt(height * height + rampLength * rampLength);

            GameObject ramp = CreateBlock(ledgeRoot.transform, "LedgeRamp");
            ramp.transform.localScale = new Vector3(width, 0.18f, rampSurfaceLength);
            ramp.transform.localPosition = new Vector3(
                0f, height * 0.5f - 0.05f, startZ - rampLength * 0.5f - featureCenterZ);
            ramp.transform.localRotation = Quaternion.Euler(-rampAngle, 0f, 0f);
            Tint(ramp, new Color(0.33f, 0.33f, 0.36f));

            // 상판 위 고가치 루트 - 올라가는 수고에 대한 보상
            if (lootCatalog != null && lootCatalog.Count > 0)
            {
                int rewardTier = rng.NextDouble() < 0.6 ? 2 : 3;
                LootDefinition reward = FindByTier(rewardTier);

                LootSpot rewardSpot = SpawnLootSpot(
                    ledgeRoot.transform, reward,
                    new Vector3(0f, height, startZ + length * 0.5f - featureCenterZ));

                // 올라와야 딴다 - 바닥 옆에서 트리거만 겹쳐도 루팅 불가 (검증 반영)
                rewardSpot.requiredMinPlayerY = height - 0.3f;
            }

            // 붕괴 연계: 전선이 지나가면 단차도 가라앉는다 (낙사)
            FloorStrip strip = ledgeRoot.AddComponent<FloorStrip>();
            strip.depthMeters = totalLength;

            CollapseFront collapse = GetComponent<CollapseFront>();

            if (collapse != null)
                collapse.RegisterStrips(new List<FloorStrip> { strip });

            currentLedges.Add(new LedgeRecord
            {
                MinX = centerX - width * 0.5f,
                MaxX = centerX + width * 0.5f,
                MinZ = startZ - rampLength,
                MaxZ = startZ + length,
            });
        }

        bool IsInLedgeZRange(float z, float margin)
        {
            foreach (LedgeRecord ledge in currentLedges)
            {
                if (z >= ledge.MinZ - margin && z <= ledge.MaxZ + margin)
                    return true;
            }

            return false;
        }

        bool IsInsideLedge(float x, float z, float margin)
        {
            foreach (LedgeRecord ledge in currentLedges)
            {
                bool insideX = x >= ledge.MinX - margin && x <= ledge.MaxX + margin;
                bool insideZ = z >= ledge.MinZ - margin && z <= ledge.MaxZ + margin;

                if (insideX && insideZ)
                    return true;
            }

            return false;
        }

        static float Lerp(System.Random rng, float min, float max)
        {
            return Mathf.Lerp(min, max, (float)rng.NextDouble());
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
                SpawnLootSpot(parent, definition, new Vector3(x, 0f, z));
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

        /// <summary>배경 생성. 등록된 인스턴스 청크 id를 돌려준다 (0 = 없음).</summary>
        int BuildShell(Transform parent)
        {
            // 사전 배치 배경(EnvironmentAuthoring)이 이 구간 범위를 커버하면
            // 런타임 배경 생성을 건너뛴다 - 손으로 다듬은 배경 보존 (ADR-0004)
            if (!preplacedSearched)
            {
                preplacedEnvironment = FindFirstObjectByType<EnvironmentAuthoring>();
                preplacedSearched = true;
            }

            float startZ = parent.position.z;
            float endZ = startZ + Definition.lengthMeters;

            if (preplacedEnvironment != null && preplacedEnvironment.Covers(startZ, endZ))
            {
                // 사전 배치 바닥도 붕괴 대상이다 - 범위 내 스트립을 등록 (검증 반영,
                // 미등록 시 사전 배치 구간에서 붕괴 압박이 무효화된다)
                RegisterPreplacedStrips(startZ, endZ);
                return 0;
            }

            RunManager run = RunManager.Instance;
            System.Random rng = run != null && run.Rng != null ? run.Rng : new System.Random(0);

            // 바닥만 GameObject (콜라이더), 배경 블록은 인스턴스 렌더링 (ADR-0005).
            // 바닥은 붕괴 단위 스트립으로 분할하고 CollapseFront에 등록 (ADR-0006)
            List<FloorStrip> strips = SegmentEnvironment.BuildWalkFloorStrips(parent, Definition);
            EnsureCollapseFront().RegisterStrips(strips);

            if (environmentRenderer == null)
            {
                environmentRenderer = GetComponent<EnvironmentRenderer>();

                if (environmentRenderer == null)
                    environmentRenderer = gameObject.AddComponent<EnvironmentRenderer>();
            }

            SightClearance clearance = BuildSightClearance(viewCamera, Definition);
            List<EnvironmentBlock> blocks = SegmentEnvironment.GenerateBlocks(Definition, rng, clearance);
            return environmentRenderer.AddChunk(blocks, parent.position);
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
