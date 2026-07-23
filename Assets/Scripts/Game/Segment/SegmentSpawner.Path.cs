using System.Collections.Generic;
using Scavenger.Loot;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// SegmentSpawner - 길 카테고리 (M4-1 요소 4분리).
    /// 보행 바닥(런타임 스트립/사전 배치 복구), 단차(수직 요소), 발밑 부착 규칙.
    /// 장식은 SegmentEnvironment, 기능(루트/위험)은 Features 파일, 코어 조율은 본 파일 참조.
    /// </summary>
    public sealed partial class SegmentSpawner
    {
        // 현재 빌드 중인 구간의 단차 점유 영역 (루트/폭탄 배치 제외용)
        readonly List<LedgeRecord> currentLedges = new List<LedgeRecord>();

        // 현재 빌드 중인 구간의 런타임 바닥 스트립 (루트/폭탄 부착용).
        // 사전 배치 커버 구간은 비어 있다 - 복구형 스트립에 부착하면
        // 획득된 루트까지 다음 런에 되살아나므로 제외 (문서화된 한계)
        readonly List<FloorStrip> currentStrips = new List<FloorStrip>();

        const float LedgeRampSlopeRatio = 2.6f;

        sealed class LedgeRecord
        {
            public float MinX, MaxX, MinZ, MaxZ;
        }

        // -- 바닥 (셸) -------------------------------------------------------

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

            currentStrips.Clear();
            hazardZoneReservations.Clear();

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
            List<FloorStrip> strips = SegmentPath.BuildWalkFloorStrips(parent, Definition);
            EnsureCollapseFront().RegisterStrips(strips);
            currentStrips.AddRange(strips);

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

        void RegisterPreplacedStrips(float startZ, float endZ)
        {
            if (preplacedEnvironment == null)
                return;

            // 비활성(침몰 보존) 스트립 포함 - 지난 런에서 가라앉은 바닥을 복구해야
            // 재시작 지점에 바닥이 존재한다 (Codex 교차 검토 P1: 낙사 루프 방지)
            FloorStrip[] allStrips = preplacedEnvironment.GetComponentsInChildren<FloorStrip>(true);
            List<FloorStrip> inRange = new List<FloorStrip>();

            foreach (FloorStrip strip in allStrips)
            {
                strip.preserveOnSink = true;
                strip.Restore();

                if (strip.EndZ > startZ - 0.1f && strip.EndZ <= endZ + 0.1f)
                    inRange.Add(strip);
            }

            // 중복 등록은 CollapseFront가 걸러낸다
            EnsureCollapseFront().RegisterStrips(inRange);
        }

        // 발밑 스트립의 자식으로 붙인다 - Sink가 자식 콜라이더를 함께 꺼서
        // 침몰한 바닥 위 요소의 상호작용이 남지 않는다. 월드 위치는 유지.
        // 사전 배치 커버 구간(currentStrips 비어 있음)은 부착하지 않는다
        void AttachToSupportingStrip(Transform feature)
        {
            float z = feature.position.z;
            float x = feature.position.x;

            foreach (FloorStrip strip in currentStrips)
            {
                float endZ = strip.EndZ;
                float startZ = endZ - strip.depthMeters;

                if (z < startZ || z > endZ)
                    continue;

                // 분할 스트립(땅 꺼짐 우회로) 대응 - x 범위도 일치해야 발밑이다
                float halfWidth = strip.transform.localScale.x * 0.5f;

                if (Mathf.Abs(x - strip.transform.position.x) > halfWidth)
                    continue;

                feature.SetParent(strip.transform, true);
                return;
            }
        }

        /// <summary>
        /// 땅 꺼짐 트랩용 스트립 분할 (검증 반영: 전폭 함몰 = 우회 불가 봉쇄).
        /// 원본 스트립을 안전 레인(우회로)으로 축소하고, 침몰 조각을 새로 만들어
        /// 돌려준다. 두 조각 모두 붕괴 전선에 등록된다.
        /// </summary>
        FloorStrip SplitStripForSinkTrap(FloorStrip strip, float safeLaneWidth, float sinkSide)
        {
            Transform stripTransform = strip.transform;
            Vector3 scale = stripTransform.localScale;
            Vector3 localPosition = stripTransform.localPosition;

            float fullWidth = scale.x;
            float laneWidth = Mathf.Clamp(safeLaneWidth, 1f, fullWidth - 1f);
            float sinkWidth = fullWidth - laneWidth;

            // 침몰 조각 (신규) - 지정된 쪽 가장자리
            float sinkCenterX = sinkSide * (fullWidth * 0.5f - sinkWidth * 0.5f);

            GameObject sinkObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sinkObject.name = "FloorStrip (SinkTrap)";
            sinkObject.transform.SetParent(stripTransform.parent, false);
            sinkObject.transform.localScale = new Vector3(sinkWidth, scale.y, scale.z);
            sinkObject.transform.localPosition = new Vector3(
                localPosition.x + sinkCenterX, localPosition.y, localPosition.z);

            FloorStrip sinkStrip = sinkObject.AddComponent<FloorStrip>();
            sinkStrip.depthMeters = strip.depthMeters;
            SegmentEnvironment.TintGameObject(sinkObject, new Color(0.3f, 0.31f, 0.33f));

            // 원본 = 안전 레인 (우회로, 반대쪽 가장자리)
            float laneCenterX = -sinkSide * (fullWidth * 0.5f - laneWidth * 0.5f);
            stripTransform.localScale = new Vector3(laneWidth, scale.y, scale.z);
            stripTransform.localPosition = new Vector3(
                localPosition.x + laneCenterX, localPosition.y, localPosition.z);

            EnsureCollapseFront().RegisterStrips(new List<FloorStrip> { sinkStrip });
            currentStrips.Add(sinkStrip);

            return sinkStrip;
        }

        // -- 수직 요소: 단차 (M1-1, ADR-0006 낙사 연계) -------------------------

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

                // 카메라 쪽이 시야 밴드에 막히면 반대편으로 재시도 - 시드에 따라
                // 단차가 0개가 되는 것을 방지 (Codex 교차 검토, rng 추가 소비 없음)
                if (LedgeRejectedByClearance(clearance, centerX, startZ, ledgeWidth, ledgeLength, ledgeHeight))
                {
                    centerX = -centerX;

                    if (LedgeRejectedByClearance(clearance, centerX, startZ, ledgeWidth, ledgeLength, ledgeHeight))
                        continue;
                }

                if (OverlapsExistingLedgeZ(startZ - rampLength, startZ + ledgeLength, gap: 4f))
                    continue;

                BuildLedge(parent, rng, centerX, startZ, ledgeWidth, ledgeLength, ledgeHeight, rampLength);
            }
        }

        // 카메라 시야 라인과 겹치면 생성 거부 (배경과 동일 규칙).
        // 상판 위 보상 루트(약 1m 비주얼)까지 포함해 검사 (검증 반영)
        static bool LedgeRejectedByClearance(
            SightClearance clearance, float centerX, float startZ,
            float width, float length, float height)
        {
            Vector3 boxCenter = new Vector3(centerX, height * 0.5f, startZ + length * 0.5f);
            Vector3 boxScale = new Vector3(width, height, length);

            Vector3 lootProbeCenter = new Vector3(centerX, height + 0.5f, boxCenter.z);
            Vector3 lootProbeScale = new Vector3(1f, 1f, 1f);

            return clearance.Rejects(boxCenter, boxScale) || clearance.Rejects(lootProbeCenter, lootProbeScale);
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

                // 올라와야 딴다 - 바닥에서 자동수집 반경이 겹쳐도 이 높이 미만이면 수집 불가
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
    }
}
