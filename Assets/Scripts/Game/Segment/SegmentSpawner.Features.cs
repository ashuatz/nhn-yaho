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

        [Header("땅 꺼짐 우회로 폭 (전폭 함몰 = 봉쇄 금지)")]
        public float sinkTrapSafeLaneWidth = 2.6f;

        [Header("밀기 트랩 (M3-3, 프리팹 튜닝 지점)")]
        public float pushTrapDetectionRadius = 2.2f;
        public float pushTrapTelegraphSeconds = 0.45f;
        public float pushTrapSpeed = 7.5f;
        public float pushTrapCooldownSeconds = 3.5f;

        [Header("낙하물 존 (돌 낙하 + 발판 파괴, 프리팹 튜닝 지점)")]
        public float rockfallTriggerDistance = 7f;
        public float rockfallWarnSeconds = 0.95f;
        public float rockfallImpactRadius = 1.6f;

        [Header("바닥 장판 (지속 피해, 프리팹 튜닝 지점)")]
        public float hazardFloorHalfWidthX = 1.5f;
        public float hazardFloorHalfWidthZ = 1f;
        public float hazardFloorDamagePerSecond = 26f;

        [Header("미사일 폭격 구역 (반복 낙하, 프리팹 튜닝 지점)")]
        public float strikeZoneHalfWidthX = 2.5f;
        public float strikeZoneLengthZ = 4f;
        public float strikeTelegraphSeconds = 1.35f;
        public float strikeDamage = 40f;
        public float strikeActivateDistance = 16f;
        public float strikeReloadMinSeconds = 2.4f;
        public float strikeReloadMaxSeconds = 4f;

        [Header("굴러오는 블록 (프리팹 튜닝 지점)")]
        public float rollingSpawnAheadDistance = 20f;
        public float rollingActivateDistance = 22f;
        public float rollingSpeedMin = 6.5f;
        public float rollingSpeedMax = 9.5f;
        public float rollingDamage = 35f;
        public float rollingIntervalMinSeconds = 3.5f;
        public float rollingIntervalMaxSeconds = 6.5f;

        const int LootSpotsPerSegment = 8;
        // 자동 수집 반경 (웹 프로토타입 sqrt(0.85) 근사 - 밟으면 먹는 손맛)
        const float LootCollectRadius = 1f;
        const float BombDetectionRadius = 3.5f;

        // 전 레인 봉쇄 금지: 같은 z 구간에 폭탄이 겹치지 않도록 최소 간격 강제.
        // 폭발 반경 상한(DepthCurve.blastMaxRadius) x 2 < 복도 폭이라 단일 폭탄은
        // 전체를 막을 수 없고, z 간격을 두면 이중 봉쇄도 불가능하다.
        const float BombMinZGap = 6f;

        // -- 끝 지점 웨이포인트 (웹 이식, ADR-0008 - ChoiceNode 대체) --------------

        // 탈출 지점(왼쪽) + 다음 스테이지 포탈(오른쪽)을 나란히 배치. 밟으면 자동.
        // 웹처럼 청록 빛기둥 랜드마크로 시각화 (탈출=청록, 다음=녹색).
        static readonly Color ExtractColor = new Color(0.35f, 0.78f, 1f);
        static readonly Color AdvanceColor = new Color(0.4f, 0.9f, 0.55f);

        void BuildWaypoints(Transform parent, int depth)
        {
            float length = Definition.lengthMeters;
            float halfWidth = Definition.corridorHalfWidth;

            // 좌우로 벌려 배치 - 어느 쪽을 밟느냐로 결정
            float sideX = halfWidth * 0.5f;

            BuildWaypoint(parent, ExtractionWaypoint.Kind.Extract,
                new Vector3(-sideX, 0f, length - 1.5f), ExtractColor, "탈출 지점");

            BuildWaypoint(parent, ExtractionWaypoint.Kind.Advance,
                new Vector3(sideX, 0f, length - 1.5f), AdvanceColor, "다음 스테이지");
        }

        void BuildWaypoint(
            Transform parent, ExtractionWaypoint.Kind kind, Vector3 localPosition,
            Color color, string label)
        {
            GameObject waypointObject = new GameObject($"Waypoint_{kind}");
            waypointObject.transform.SetParent(parent, false);
            waypointObject.transform.localPosition = localPosition;

            BoxCollider trigger = waypointObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(2.4f, 3f, 2f);
            trigger.center = new Vector3(0f, 1.5f, 0f);

            ExtractionWaypoint waypoint = waypointObject.AddComponent<ExtractionWaypoint>();

            if (kind == ExtractionWaypoint.Kind.Extract)
                waypoint.Initialize(kind, OnExtractReached);
            else
                waypoint.Initialize(kind, OnAdvanceReached);

            BuildWaypointVisual(waypointObject.transform, color);
        }

        void OnAdvanceReached(ExtractionWaypoint waypoint)
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return;

            run.AdvanceDepth();

            // 다음 구간은 룩어헤드로 이미 존재. 그 다음을 미리 지어 선노출 유지 (ADR-0004)
            BuildSegment(run.Depth + 1, TailEndZ);

            // 방금 끝난 구간(플레이어 발밑)은 남기고 그보다 뒤만 제거
            float nextStartZ = waypoint.transform.position.z + 1.5f;
            DespawnBehind(nextStartZ - 0.5f);
        }

        static void OnExtractReached(ExtractionWaypoint waypoint)
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return;

            run.CompleteExtraction();
        }

        // 웹 탈출 지점 룩: 발광 바닥 큐브 + 위로 솟는 빛 기둥(반투명 quad) + 포인트라이트
        static void BuildWaypointVisual(Transform parent, Color color)
        {
            // 바닥 발광 큐브
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "Pad";
            pad.transform.SetParent(parent, false);
            pad.transform.localScale = new Vector3(2.2f, 0.1f, 2.2f);
            pad.transform.localPosition = new Vector3(0f, 0.05f, 0f);

            Collider padCollider = pad.GetComponent<Collider>();

            if (padCollider != null)
                Destroy(padCollider);

            ApplyGlowMaterial(pad, color);

            // 빛 기둥: 세로로 긴 반투명 큐브 (웹 빛기둥 근사)
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "Beam";
            beam.transform.SetParent(parent, false);
            beam.transform.localScale = new Vector3(0.6f, 6f, 0.6f);
            beam.transform.localPosition = new Vector3(0f, 3f, 0f);

            Collider beamCollider = beam.GetComponent<Collider>();

            if (beamCollider != null)
                Destroy(beamCollider);

            ApplyGlowMaterial(beam, color);

            // 포인트라이트 - Bloom과 함께 랜드마크로 눈에 띈다
            GameObject lightObject = new GameObject("Glow");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.5f, 0f);

            Light glow = lightObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = color;
            glow.range = 6f;
            glow.intensity = 2.5f;
            glow.shadows = LightShadows.None;
        }

        // 발광(emissive) 머티리얼 적용 - 인스턴스라 Bloom과 함께 빛난다
        static void ApplyGlowMaterial(GameObject target, Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();

            if (renderer == null)
                return;

            Material material = renderer.material;
            material.color = color;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
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

                // 바닥이 가라앉으면 아이템도 함께 - 공중에 뜬 아이템이 인접 스트립에서
                // 수집되는 것 방지 (Codex 교차 검토)
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
            spot.Initialize(definition, LootCollectRadius);

            BuildLootVisual(spotObject.transform, definition.tier);
            return spot;
        }

        static void BuildLootVisual(Transform parent, int tier)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Visual";
            cube.transform.SetParent(parent, false);
            cube.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            cube.transform.localPosition = new Vector3(0f, 0.4f, 0f);

            // 큐브를 살짝 기울여 아이소 화면에서 각이 서게 (웹 룩 - 마름모 발광)
            cube.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);

            // 비주얼 전용 - 수집 판정은 LootSpot이 거리로 담당
            Collider cubeCollider = cube.GetComponent<Collider>();

            if (cubeCollider != null)
                Destroy(cubeCollider);

            Color tierColor = LootDefinition.TierColor(tier);
            Tint(cube, tierColor);

            // 발광 + 부유 연출 (ADR-0008 웹 이식). 루트에 붙여 자식 "Visual"을 조작
            LootVisual visual = parent.gameObject.AddComponent<LootVisual>();
            visual.Configure(tierColor);
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

                if (strip.GetComponentInChildren<SinkTrap>() != null)
                    continue;

                // 이미 분할된 조각(우회로/침몰편)은 재분할 금지
                if (strip.transform.localScale.x < Definition.corridorHalfWidth * 2f)
                    continue;

                // 이미 루트/폭탄/트랩이 붙은 스트립은 제외 - 분할 시 소속이 꼬인다
                if (strip.GetComponentInChildren<LootSpot>() != null)
                    continue;

                if (strip.GetComponentInChildren<Bomb>() != null)
                    continue;

                if (strip.GetComponentInChildren<PushTrap>() != null)
                    continue;

                // 전폭 함몰 = 우회 불가 봉쇄가 되므로 스트립을 분할해
                // 한쪽에 안전 레인(우회로)을 남긴다 (사용자 지시)
                float sinkSide = run.Rng.Next(0, 2) == 0 ? -1f : 1f;
                FloorStrip sinkStrip = SplitStripForSinkTrap(strip, sinkTrapSafeLaneWidth, sinkSide);

                SinkTrap trap = sinkStrip.gameObject.AddComponent<SinkTrap>();
                trap.triggerDistance = sinkTrapTriggerDistance;
                trap.warnSeconds = sinkTrapWarnSeconds;
                trap.warnTremorMax = sinkTrapTremorMax;

                placed += 1;
            }
        }

        // -- 낙하물 존 배치 (사용자 지시: 떨어지는 돌 + 착탄 지점 발판 파괴) -------

        void PopulateRockfalls(Transform parent, int depth)
        {
            RunManager run = RunManager.Instance;

            if (run == null || run.Rng == null)
                return;

            int zoneCount = Curve.EvaluateRockfallCount(depth);

            if (zoneCount <= 0)
                return;

            float length = Definition.lengthMeters;
            List<float> placedZ = new List<float>();
            int attempts = 0;

            while (placedZ.Count < zoneCount && attempts < zoneCount * 10)
            {
                attempts += 1;

                // 초입/선택지 앞은 비운다 (다른 위협과 동일 규칙)
                float z = Mathf.Lerp(16f, length - 10f, (float)run.Rng.NextDouble());

                // 연속 낙하 콤보 방지 간격
                if (!IsZGapValid(placedZ, z, minZGap: 9f))
                    continue;

                // 단차 진입로 파괴 = 보상 동선 봉쇄 - 제외
                if (IsInLedgeZRange(z, margin: 1f))
                    continue;

                GameObject zoneObject = new GameObject("RockfallZone");
                zoneObject.transform.SetParent(parent, false);
                zoneObject.transform.localPosition = new Vector3(0f, 0f, z);

                RockfallZone zone = zoneObject.AddComponent<RockfallZone>();
                zone.triggerDistance = rockfallTriggerDistance;
                zone.warnSeconds = rockfallWarnSeconds;
                zone.impactRadius = rockfallImpactRadius;

                // 착탄 산포는 사망/발판 파괴를 결정하는 게임 결과 - 배치 스트림에서
                // 시드 배정 (시드 재현성 규약). 안전 레인 폭은 땅 꺼짐과 공유
                zone.scatterSeed = run.Rng.Next(1, int.MaxValue);
                zone.safeLaneWidth = sinkTrapSafeLaneWidth;

                placedZ.Add(z);
            }
        }

        // -- 바닥 장판 배치 (지속 피해, 웹 이식) -------------------------------

        void PopulateHazardFloors(Transform parent, int depth)
        {
            RunManager run = RunManager.Instance;

            if (run == null || run.Rng == null)
                return;

            int zoneCount = Curve.EvaluateHazardFloorCount(depth);

            if (zoneCount <= 0)
                return;

            float length = Definition.lengthMeters;
            float halfWidth = Definition.corridorHalfWidth;

            List<float> placedZ = new List<float>();
            int attempts = 0;

            while (placedZ.Count < zoneCount && attempts < zoneCount * 10)
            {
                attempts += 1;

                // 초입/선택지 앞은 비운다 (다른 위협과 동일 규칙)
                float z = Mathf.Lerp(14f, length - 8f, (float)run.Rng.NextDouble());

                if (!IsZGapValid(placedZ, z, minZGap: 8f))
                    continue;

                // 단차 진입로를 장판으로 덮으면 보상 동선이 사실상 봉쇄된다 - 제외
                if (IsInLedgeZRange(z, margin: 1f))
                    continue;

                // 복도 한쪽에만 깔아 우회 여지를 남긴다 (전폭 장판 = 강제 피해 봉쇄 금지)
                float side = run.Rng.Next(0, 2) == 0 ? -1f : 1f;
                float x = side * Mathf.Lerp(0.4f, halfWidth - hazardFloorHalfWidthX - 0.2f, (float)run.Rng.NextDouble());

                GameObject zoneObject = new GameObject("HazardFloor");
                zoneObject.transform.SetParent(parent, false);
                zoneObject.transform.localPosition = new Vector3(x, 0f, z);

                HazardFloor hazard = zoneObject.AddComponent<HazardFloor>();
                hazard.halfWidthX = hazardFloorHalfWidthX;
                hazard.halfWidthZ = hazardFloorHalfWidthZ;
                hazard.damagePerSecond = hazardFloorDamagePerSecond;

                // 바닥과 함께 침몰 (루트/폭탄과 동일 규칙)
                AttachToSupportingStrip(zoneObject.transform);

                placedZ.Add(z);
            }
        }

        // -- 미사일 폭격 구역 배치 (반복 낙하, 웹 이식) ------------------------

        void PopulateStrikeZones(Transform parent, int depth)
        {
            RunManager run = RunManager.Instance;

            if (run == null || run.Rng == null)
                return;

            int zoneCount = Curve.EvaluateStrikeZoneCount(depth);

            if (zoneCount <= 0)
                return;

            float length = Definition.lengthMeters;

            List<float> placedZ = new List<float>();
            int attempts = 0;

            while (placedZ.Count < zoneCount && attempts < zoneCount * 10)
            {
                attempts += 1;

                // 초입/선택지 앞은 비운다 (진입 직후 폭격 방지)
                float z = Mathf.Lerp(22f, length - 12f, (float)run.Rng.NextDouble());

                // 구역끼리 겹치면 폭격 콤보로 회피 불가 - 간격 강제
                if (!IsZGapValid(placedZ, z, minZGap: 12f))
                    continue;

                if (IsInLedgeZRange(z, margin: 1f))
                    continue;

                GameObject zoneObject = new GameObject("StrikeZone");
                zoneObject.transform.SetParent(parent, false);
                zoneObject.transform.localPosition = new Vector3(0f, 0f, z);

                StrikeZone zone = zoneObject.AddComponent<StrikeZone>();
                zone.halfWidthX = strikeZoneHalfWidthX;
                zone.lengthZ = strikeZoneLengthZ;
                zone.telegraphSeconds = strikeTelegraphSeconds;
                zone.damage = strikeDamage;
                zone.activateDistance = strikeActivateDistance;
                zone.reloadMinSeconds = strikeReloadMinSeconds;
                zone.reloadMaxSeconds = strikeReloadMaxSeconds;

                // 셀 선택 산포는 사망을 결정하는 게임 결과 - 배치 스트림에서 시드 배정
                zone.scatterSeed = run.Rng.Next(1, int.MaxValue);

                placedZ.Add(z);
            }
        }

        // -- 굴러오는 블록 스포너 배치 (웹 이식) -------------------------------

        void PopulateRollingBlocks(Transform parent, int depth)
        {
            RunManager run = RunManager.Instance;

            if (run == null || run.Rng == null)
                return;

            int spawnerCount = Curve.EvaluateRollingBlockCount(depth);

            if (spawnerCount <= 0)
                return;

            float length = Definition.lengthMeters;
            float halfWidth = Definition.corridorHalfWidth;

            List<float> placedZ = new List<float>();
            int attempts = 0;

            while (placedZ.Count < spawnerCount && attempts < spawnerCount * 10)
            {
                attempts += 1;

                // 스포너는 구역 중심 z - 스폰은 플레이어 정면이라 위치가 곧 활성 구간
                float z = Mathf.Lerp(20f, length - 16f, (float)run.Rng.NextDouble());

                if (!IsZGapValid(placedZ, z, minZGap: 16f))
                    continue;

                if (IsInLedgeZRange(z, margin: 1f))
                    continue;

                GameObject spawnerObject = new GameObject("RollingBlockSpawner");
                spawnerObject.transform.SetParent(parent, false);
                spawnerObject.transform.localPosition = new Vector3(0f, 0f, z);

                RollingBlockSpawner spawner = spawnerObject.AddComponent<RollingBlockSpawner>();
                spawner.spawnAheadDistance = rollingSpawnAheadDistance;
                spawner.activateDistance = rollingActivateDistance;
                spawner.speedMin = rollingSpeedMin;
                spawner.speedMax = rollingSpeedMax;
                spawner.damage = rollingDamage;
                spawner.intervalMinSeconds = rollingIntervalMinSeconds;
                spawner.intervalMaxSeconds = rollingIntervalMaxSeconds;
                spawner.spawnHalfWidth = halfWidth;

                // 스폰 x 산포는 회피 동선을 좌우하는 게임 결과 - 배치 스트림에서 시드 배정
                spawner.scatterSeed = run.Rng.Next(1, int.MaxValue);

                placedZ.Add(z);
            }
        }

        // -- 밀기 트랩 배치 (M3-3) ---------------------------------------------

        void PopulatePushTraps(Transform parent, int depth)
        {
            RunManager run = RunManager.Instance;

            if (run == null || run.Rng == null)
                return;

            // 사전 배치 커버 구간(부착 대상 스트립 없음)에는 배치하지 않는다 -
            // 바닥이 가라앉은 뒤 공중에 떠서 계속 발동하는 트랩 방지 (Codex 검토)
            if (currentStrips.Count == 0)
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
