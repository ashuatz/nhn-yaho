using System.Collections.Generic;
using Scavenger.Loot;
using Scavenger.Run;
using Scavenger.Segment;
using UnityEngine;

namespace Scavenger.Field
{
    /// <summary>
    /// FieldSpawner - 배치 카테고리. 존 안의 드랍 아이템(드랍 아이템 문서 5장)과
    /// 스테이지 끝 탈출 지점을 배치한다. 수명 주기/존 체인은 FieldSpawner.cs 참조.
    /// </summary>
    public sealed partial class FieldSpawner
    {
        const float LootCollectRadius = 1f;

        // 존 가장자리 여유 (m). 아이템이 바닥 밖으로 걸치지 않게
        const float DropEdgeMargin = 0.6f;

        // 탈출 지점 주변은 비운다 - 랜드마크 시야 확보
        const float StageExitClearance = 3f;

        // 배치 시도 상한 - 예산이 남아도 자리를 못 찾으면 중단 (무한 루프 방지)
        const int DropPlacementAttemptsPerItem = 8;

        // 웹 탈출 지점 룩 (ADR-0008): 탈출=청록, 다음 스테이지=녹색
        static readonly Color ExtractColor = new Color(0.35f, 0.78f, 1f);
        static readonly Color AdvanceColor = new Color(0.4f, 0.9f, 0.55f);

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

            // 마지막 존은 탈출 지점 앞을 비운다
            if (zone.IsLastZone)
                maxZ -= StageExitClearance;

            if (maxZ <= minZ || halfWidth <= 0f)
                return;

            placedDropPositions.Clear();

            while (budget > 0)
            {
                LootDefinition definition = PickAffordableLoot(rng, budget);

                if (definition == null)
                    return;

                if (!TryFindDropPosition(rng, halfWidth, minZ, maxZ, out Vector3 position))
                    return;

                SpawnDrop(zone, definition, position);

                // 가치 값이 예산 소비량 (문서 5.2 (3) 예: 예산 100 / 가치 10 -> 10개)
                budget -= Mathf.Max(1, definition.value);
            }
        }

        // 남은 예산으로 감당되는 아이템 중 하나를 무작위로 고른다.
        // 감당 가능한 것이 없으면 null - 배치를 끝낸다
        LootDefinition PickAffordableLoot(System.Random rng, int budget)
        {
            List<LootDefinition> affordable = new List<LootDefinition>(lootCatalog.Count);

            foreach (LootDefinition definition in lootCatalog)
            {
                if (definition == null)
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
            spot.Initialize(definition, LootCollectRadius);

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

            Color tierColor = LootDefinition.TierColor(tier);

            // 공유 머티리얼만 깔아둔다 - LootVisual이 여기서 개체 인스턴스를 떠서
            // 발광/부유를 얹으므로(개체별 위상), 그 원본이 Common.mat이 되게 하는 역할
            GreyboxPalette.Apply(cube, tierColor);

            LootVisual visual = parent.gameObject.AddComponent<LootVisual>();
            visual.Configure(tierColor);
        }

        // -- 스테이지 끝: 탈출 지점 / 다음 스테이지 (웹 이식, ADR-0008) ------------

        void BuildStageExit(Zone zone)
        {
            float exitZ = zone.EndZ - 1.5f;
            float sideX = Definition.corridorHalfWidth * 0.5f;

            BuildWaypoint(zone, ExtractionWaypoint.Kind.Extract,
                new Vector3(-sideX, 0f, exitZ), ExtractColor);

            BuildWaypoint(zone, ExtractionWaypoint.Kind.Advance,
                new Vector3(sideX, 0f, exitZ), AdvanceColor);
        }

        void BuildWaypoint(
            Zone zone, ExtractionWaypoint.Kind kind, Vector3 worldPosition, Color color)
        {
            GameObject waypointObject = new GameObject($"Waypoint_{kind}");
            waypointObject.transform.SetParent(zone.transform, true);
            waypointObject.transform.position = worldPosition;

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

        // 다음 스테이지: 깊이를 올리고 이 지점부터 새 필드를 생성한다 (심리스)
        void OnAdvanceReached(ExtractionWaypoint waypoint)
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return;

            run.AdvanceDepth();

            // 밟은 지점 앞부터 새 스테이지 - 기존 존은 StartStage가 정리한다
            StartStage(waypoint.transform.position.z);
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
