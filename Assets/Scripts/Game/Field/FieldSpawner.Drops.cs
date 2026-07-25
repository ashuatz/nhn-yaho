using System.Collections.Generic;
using Scavenger.Loot;
using Scavenger.Run;
using Scavenger.Segment;
using UnityEngine;

namespace Scavenger.Field
{
    /// <summary>
    /// FieldSpawner - 배치 카테고리. 존 안의 드랍 아이템(드랍 아이템 문서 5장)과
    /// 존 사이 구간의 탈출 지점을 배치한다. 수명 주기/세그먼트 체인은 FieldSpawner.cs 참조.
    /// </summary>
    public sealed partial class FieldSpawner
    {
        // 아이템 획득 거리 폴백 (m). 정본은 플레이어 옵션 (드랍 문서 9.3)
        const float LootCollectRadiusFallback = 1f;

        // 존 가장자리 여유 (m). 아이템이 바닥 밖으로 걸치지 않게
        const float DropEdgeMargin = 0.6f;

        // 배치 시도 상한 - 예산이 남아도 자리를 못 찾으면 중단 (무한 루프 방지)
        const int DropPlacementAttemptsPerItem = 8;

        // 웹 탈출 지점 룩 (ADR-0008): 탈출 = 청록 빛기둥 랜드마크
        static readonly Color ExtractColor = new Color(0.35f, 0.78f, 1f);

        readonly List<Vector3> placedDropPositions = new List<Vector3>();

        /// <summary>
        /// 존 예산(아이템 가치 값 합) 안에서 바닥에 드랍 아이템을 흩뿌린다.
        /// 가치가 높은 아이템일수록 예산을 많이 써서 적게 나온다 (문서 5.2 (3)).
        /// 생성 간격 거리를 지켜 서로 붙지 않게 한다.
        /// </summary>
        void PopulateDrops(Zone zone)
        {
            if (lootCatalog == null || lootCatalog.Count == 0)
                return;

            RunManager run = RunManager.Instance;

            if (run == null || run.Rng == null)
                return;

            System.Random rng = run.Rng;
            int budget = Definition.RollDropBudget(rng);

            float halfWidth = Definition.corridorHalfWidth - DropEdgeMargin;
            float minZ = zone.StartZ + DropEdgeMargin;
            float maxZ = zone.EndZ - DropEdgeMargin;

            if (maxZ <= minZ || halfWidth <= 0f)
                return;

            placedDropPositions.Clear();

            // 등장 존 판정은 스테이지 내 존 순번 기준 (드랍 문서 5.2 (4)).
            // 이 값은 세그먼트를 만드는 중에만 유효하다 - 배치가 생성 시점에 끝나므로 안전
            int zoneIndexInStage = buildStageZoneIndex;

            while (budget > 0)
            {
                LootDefinition definition = PickAffordableLoot(rng, budget, zoneIndexInStage);

                if (definition == null)
                    return;

                if (!TryFindDropPosition(rng, halfWidth, minZ, maxZ, out Vector3 position))
                    return;

                SpawnDrop(zone, definition, position);

                // 가치 값이 예산 소비량 (문서 5.2 (3) 예: 예산 100 / 가치 10 -> 10개)
                budget -= Mathf.Max(1, definition.value);
            }
        }

        // 아이템 획득 거리는 플레이어 옵션이 정본 (드랍 문서 9.3).
        // 플레이어 배선 전(에디터 프리뷰 등)에는 폴백 상수를 쓴다
        float ResolveCollectRadius()
        {
            if (trackedPlayer != null)
                return Mathf.Max(0.1f, trackedPlayer.itemCollectDistance);

            return LootCollectRadiusFallback;
        }

        // 남은 예산으로 감당되고 이 존에 등장할 수 있는 아이템 중 하나를 무작위로 고른다.
        // 감당 가능한 것이 없으면 null - 배치를 끝낸다
        LootDefinition PickAffordableLoot(System.Random rng, int budget, int zoneIndexInStage)
        {
            List<LootDefinition> affordable = new List<LootDefinition>(lootCatalog.Count);

            foreach (LootDefinition definition in lootCatalog)
            {
                if (definition == null)
                    continue;

                // 등장 존 제한 (드랍 문서 5.2 (4)) - 목록이 비어 있으면 모든 존
                if (!definition.CanSpawnInZone(zoneIndexInStage))
                    continue;

                if (Mathf.Max(1, definition.value) <= budget)
                    affordable.Add(definition);
            }

            if (affordable.Count == 0)
                return null;

            return affordable[rng.Next(0, affordable.Count)];
        }

        bool TryFindDropPosition(
            System.Random rng, float halfWidth, float minZ, float maxZ, out Vector3 position)
        {
            float spacingSqr = Definition.dropSpacingDistance * Definition.dropSpacingDistance;

            for (int attempt = 0; attempt < DropPlacementAttemptsPerItem; attempt++)
            {
                float x = Mathf.Lerp(-halfWidth, halfWidth, (float)rng.NextDouble());
                float z = Mathf.Lerp(minZ, maxZ, (float)rng.NextDouble());

                Vector3 candidate = new Vector3(x, 0f, z);

                if (IsTooCloseToPlacedDrop(candidate, spacingSqr))
                    continue;

                placedDropPositions.Add(candidate);
                position = candidate;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        bool IsTooCloseToPlacedDrop(Vector3 candidate, float spacingSqr)
        {
            foreach (Vector3 placed in placedDropPositions)
            {
                if ((placed - candidate).sqrMagnitude < spacingSqr)
                    return true;
            }

            return false;
        }

        void SpawnDrop(Zone zone, LootDefinition definition, Vector3 worldPosition)
        {
            GameObject spotObject = new GameObject($"Drop_{definition.id}");
            spotObject.transform.SetParent(zone.transform, true);
            spotObject.transform.position = worldPosition;

            LootSpot spot = spotObject.AddComponent<LootSpot>();
            spot.Initialize(definition, ResolveCollectRadius());

            BuildDropVisual(spotObject.transform, definition.tier);

            // 바닥 행이 낙하하면 아이템도 함께 떨어진다 - 공중에 남아
            // 인접 행에서 수집되는 것 방지
            zone.AttachToRow(spotObject.transform);
        }

        // 웹 룩(ADR-0008): 45도 돌린 발광 큐브 + 부유 연출 (LootVisual)
        static void BuildDropVisual(Transform parent, int tier)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Visual";
            cube.transform.SetParent(parent, false);
            cube.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            cube.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            cube.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);

            // 비주얼 전용 - 수집 판정은 LootSpot이 거리로 담당
            Collider cubeCollider = cube.GetComponent<Collider>();

            if (cubeCollider != null)
                Destroy(cubeCollider);

            Color tierColor = LootDefinition.GradeColor(tier);

            // 공유 머티리얼만 깔아둔다 - LootVisual이 여기서 개체 인스턴스를 떠서
            // 발광/부유를 얹으므로(개체별 위상), 그 원본이 Common.mat이 되게 하는 역할
            GreyboxPalette.Apply(cube, tierColor);

            LootVisual visual = parent.gameObject.AddComponent<LootVisual>();
            visual.Configure(tierColor);
        }

        // -- 존 사이 구간: 탈출 지점 (웹 이식, ADR-0008) ------------------------

        /// <summary>
        /// 구간에 탈출 웨이포인트를 하나 놓는다 (사용자 지시: Extract만 구성).
        /// 진행(Advance)은 오브젝트가 아니다 - 밟지 않고 걸어서 구간을 통과하면
        /// 그것이 곧 다음 스테이지이며, 앞쪽 존은 계속 생성된다.
        ///
        /// 직진 동선에서 벗어난 화면 위쪽(카메라 반대편)에 붙인다 -
        /// 그냥 지나가려는 플레이어가 밟아서 강제 정산되지 않게 하기 위함.
        /// </summary>
        void BuildJunctionWaypoint(Zone zone)
        {
            int cameraSide = SegmentEnvironment.CameraSide(
                BuildSightClearance(viewCamera, Definition));

            float sideX = -cameraSide * Definition.corridorHalfWidth * 0.6f;
            float centerZ = (zone.StartZ + zone.EndZ) * 0.5f;

            BuildWaypoint(zone, new Vector3(sideX, 0f, centerZ), ExtractColor);
        }

        void BuildWaypoint(Zone zone, Vector3 worldPosition, Color color)
        {
            GameObject waypointObject = new GameObject("Waypoint_Extract");
            waypointObject.transform.SetParent(zone.transform, true);
            waypointObject.transform.position = worldPosition;

            BoxCollider trigger = waypointObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(2.4f, 3f, 2f);
            trigger.center = new Vector3(0f, 1.5f, 0f);

            ExtractionWaypoint waypoint = waypointObject.AddComponent<ExtractionWaypoint>();
            waypoint.Initialize(OnExtractReached);

            BuildWaypointVisual(waypointObject.transform, color);

            // 바닥 행이 낙하하면 정산 지점도 함께 떨어진다 - 사라진 바닥 위에
            // 트리거만 공중에 남는 것을 막는다 (드랍 아이템과 같은 규칙)
            zone.AttachToRow(waypointObject.transform);
        }

        static void OnExtractReached(ExtractionWaypoint waypoint)
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return;

            run.CompleteExtraction();
        }

        // 웹 탈출 지점 룩: 발광 바닥 패드 + 빛 기둥 + 포인트라이트 랜드마크
        static void BuildWaypointVisual(Transform parent, Color color)
        {
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "Pad";
            pad.transform.SetParent(parent, false);
            pad.transform.localScale = new Vector3(2.2f, 0.1f, 2.2f);
            pad.transform.localPosition = new Vector3(0f, 0.05f, 0f);

            Collider padCollider = pad.GetComponent<Collider>();

            if (padCollider != null)
                Destroy(padCollider);

            ApplyGlowMaterial(pad, color);

            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "Beam";
            beam.transform.SetParent(parent, false);
            beam.transform.localScale = new Vector3(0.6f, 6f, 0.6f);
            beam.transform.localPosition = new Vector3(0f, 3f, 0f);

            Collider beamCollider = beam.GetComponent<Collider>();

            if (beamCollider != null)
                Destroy(beamCollider);

            ApplyGlowMaterial(beam, color);

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

        // 발광(emissive) 머티리얼 - Common.mat 기반 공유 인스턴스 (색당 1장)
        static void ApplyGlowMaterial(GameObject target, Color color)
        {
            GreyboxPalette.ApplyGlow(target, color, emissionScale: 2f);
        }
    }
}
