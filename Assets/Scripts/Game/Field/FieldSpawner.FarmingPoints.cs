using System.Collections.Generic;
using Scavenger.Run;
using Scavenger.Segment;
using UnityEngine;

namespace Scavenger.Field
{
    /// <summary>
    /// FieldSpawner - 파밍 포인트 배치 (파밍 문서 2.4 / 2.5).
    /// 존당 개수를 min-max로 뽑고, 최소 간격을 지켜 소켓 자리를 잡는다.
    /// 상단/하단 배분은 1번째 상단 고정, 2번째 하단 고정, 3번째부터 50:50 랜덤.
    ///
    /// 현재는 그레이박스 지형을 코드로 생성한다. 아트 프리팹이 들어오면
    /// BuildGreyboxPoint를 프리팹 인스턴스화로 교체하고, 소켓/스팟은
    /// 프리팹 안의 PointSocket / ObjectSpot 마커에서 읽는다 (계약 동일).
    /// </summary>
    public sealed partial class FieldSpawner
    {
        // 그레이박스 파밍 포인트 색 (등급 위상 구분 - 아트 교체 시 사라진다)
        static readonly Color NormalPointColor = new Color(0.34f, 0.38f, 0.34f);
        static readonly Color RarePointColor = new Color(0.28f, 0.38f, 0.5f);
        static readonly Color HeroPointColor = new Color(0.42f, 0.32f, 0.5f);

        // 등급 위상 가중치 (일반 / 희귀 / 영웅). 깊이 스케일링은 후속 작업
        const float RareGradeChance = 0.3f;
        const float HeroGradeChance = 0.1f;

        // 소켓이 존 경계에 너무 붙지 않게 두는 여유 (m)
        const float SocketEdgeMargin = 1f;

        // 그레이박스 오브젝트 스팟 개수
        const int GreyboxSpotCount = 2;

        readonly List<float> placedSocketZ = new List<float>();

        /// <summary>
        /// 존 양옆에 파밍 포인트를 배치한다. 소켓 z는 존 안에서 뽑되
        /// 구역 전체가 존 z 범위를 벗어나지 않도록 여유를 둔다.
        /// </summary>
        void PopulateFarmingPoints(Zone zone)
        {
            RunManager run = RunManager.Instance;

            if (run == null || run.Rng == null)
                return;

            System.Random rng = run.Rng;
            int count = Definition.RollFarmingPointCount(rng);

            if (count <= 0)
                return;

            float size = Definition.FarmingPointSize;
            float halfSize = size * 0.5f;

            float minSocketZ = zone.StartZ + halfSize + SocketEdgeMargin;
            float maxSocketZ = zone.EndZ - halfSize - SocketEdgeMargin;

            // 마지막 존은 탈출 지점 앞을 비운다 (랜드마크 시야 확보)
            if (zone.IsLastZone)
                maxSocketZ -= StageExitClearance;

            if (maxSocketZ <= minSocketZ)
                return;

            placedSocketZ.Clear();

            int placed = 0;
            int attempts = 0;
            int maxAttempts = count * 10;

            while (placed < count && attempts < maxAttempts)
            {
                attempts += 1;

                float socketZ = Mathf.Lerp(minSocketZ, maxSocketZ, (float)rng.NextDouble());

                if (!IsSocketSpacingValid(socketZ))
                    continue;

                FarmingPointType pointType = ResolvePointType(placed, rng);
                FarmingPointGrade grade = ResolveGrade(rng);

                BuildFarmingPoint(zone, pointType, grade, socketZ, size);

                placedSocketZ.Add(socketZ);
                placed += 1;
            }
        }

        bool IsSocketSpacingValid(float socketZ)
        {
            float spacing = Definition.farmingPointSpacingBlocks * Definition.blockSize;

            foreach (float placed in placedSocketZ)
            {
                if (Mathf.Abs(placed - socketZ) < spacing)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 상단/하단 배분 (파밍 문서 2.5): 1번째 상단 고정, 2번째 하단 고정,
        /// 3번째부터 상단 50 : 하단 50 랜덤.
        /// </summary>
        static FarmingPointType ResolvePointType(int placedIndex, System.Random rng)
        {
            if (placedIndex == 0)
                return FarmingPointType.Top;

            if (placedIndex == 1)
                return FarmingPointType.Bottom;

            return rng.Next(0, 2) == 0 ? FarmingPointType.Top : FarmingPointType.Bottom;
        }

        static FarmingPointGrade ResolveGrade(System.Random rng)
        {
            float roll = (float)rng.NextDouble();

            if (roll < HeroGradeChance)
                return FarmingPointGrade.Hero;

            if (roll < HeroGradeChance + RareGradeChance)
                return FarmingPointGrade.Rare;

            return FarmingPointGrade.Normal;
        }

        void BuildFarmingPoint(
            Zone zone, FarmingPointType pointType, FarmingPointGrade grade,
            float socketZ, float size)
        {
            // 상단은 화면 위쪽(카메라 반대편), 하단은 화면 아래쪽(카메라 쪽)에 붙는다.
            // 카메라측에 높은 지형을 두면 발판을 가리므로 방향 판정을 배경과 공유한다
            int cameraSide = SegmentEnvironment.CameraSide(
                BuildSightClearance(viewCamera, Definition));

            float sideSign = pointType == FarmingPointType.Top ? -cameraSide : cameraSide;
            float zoneHalfWidth = Definition.corridorHalfWidth;

            Vector3 socketPosition = new Vector3(sideSign * zoneHalfWidth, 0f, socketZ);

            FarmingPoint prefab = pointType == FarmingPointType.Top
                ? farmingPointTopPrefab
                : farmingPointBottomPrefab;

            FarmingPoint point = prefab != null
                ? InstantiatePointPrefab(prefab, zone, socketPosition, sideSign, pointType, grade)
                : BuildGreyboxFarmingPoint(zone, socketPosition, sideSign, zoneHalfWidth, socketZ, size, pointType, grade);

            // 아트 프리팹이 자기 규격을 선언하면 그것을 안전지대 범위로 쓴다 -
            // 프리팹 크기와 판정 범위가 어긋나면 플랫폼 끝 전에 막히거나 허공을 걷는다
            float effectiveSize = point.authoredSizeBlocks > 0f
                ? point.authoredSizeBlocks * Definition.blockSize
                : size;

            point.Initialize(
                pointType, grade, socketPosition, sideSign, zoneHalfWidth,
                depth: effectiveSize, length: effectiveSize,
                shakeStartDistance: Definition.farmingPointShakeStartBlocks * Definition.blockSize);
        }

        /// <summary>
        /// 아트 프리팹 배치. 프리팹 규격은 소켓이 원점(0,0,0)이고 +x 방향으로 뻗는 형태.
        /// 반대편(-x)에는 y축 180도 회전으로 붙인다 - 음수 스케일은 콜라이더/노멀이
        /// 뒤집히므로 쓰지 않는다.
        /// </summary>
        static FarmingPoint InstantiatePointPrefab(
            FarmingPoint prefab, Zone zone, Vector3 socketPosition, float sideSign,
            FarmingPointType pointType, FarmingPointGrade grade)
        {
            Quaternion rotation = sideSign > 0f
                ? Quaternion.identity
                : Quaternion.Euler(0f, 180f, 0f);

            FarmingPoint point = Instantiate(prefab, socketPosition, rotation, zone.transform);
            point.name = $"FarmingPoint_{pointType}_{grade}";

            return point;
        }

        // 프리팹 미배선 폴백 - 코드로 그레이박스 지형을 만든다
        FarmingPoint BuildGreyboxFarmingPoint(
            Zone zone, Vector3 socketPosition, float sideSign, float zoneHalfWidth,
            float socketZ, float size, FarmingPointType pointType, FarmingPointGrade grade)
        {
            GameObject pointObject = new GameObject($"FarmingPoint_{pointType}_{grade}");
            pointObject.transform.SetParent(zone.transform, true);
            pointObject.transform.position = Vector3.zero;

            BuildGreyboxPoint(pointObject.transform, grade, sideSign, zoneHalfWidth, socketZ, size);

            return pointObject.AddComponent<FarmingPoint>();
        }

        /// <summary>
        /// 그레이박스 지형 생성. 단차는 아트 프리팹 R&D 항목이라
        /// 지금은 보행면과 같은 높이(flush)로 만든다 (파밍 문서 2.4 (1)).
        /// </summary>
        void BuildGreyboxPoint(
            Transform parent, FarmingPointGrade grade, float sideSign,
            float zoneHalfWidth, float socketZ, float size)
        {
            float centerX = sideSign * (zoneHalfWidth + size * 0.5f);

            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Platform";
            platform.transform.SetParent(parent, true);
            platform.transform.position = new Vector3(centerX, -0.1f, socketZ);
            platform.transform.localScale = new Vector3(size, 0.2f, size);

            GreyboxPalette.Apply(platform, ResolveGradeColor(grade));

            // 포인트 소켓 마커 - 존과 맞닿은 입구. 제거 판정 기준 좌표
            GameObject socket = new GameObject("PointSocket");
            socket.transform.SetParent(parent, true);
            socket.transform.position = new Vector3(sideSign * zoneHalfWidth, 0f, socketZ);
            socket.AddComponent<PointSocket>();

            // 오브젝트 스팟 마커 - 아이템 오브젝트가 생성될 자리 (3단계 작업)
            for (int i = 0; i < GreyboxSpotCount; i++)
            {
                float offset = size * (i == 0 ? -0.22f : 0.22f);

                GameObject spot = new GameObject($"ObjectSpot_{i:D2}");
                spot.transform.SetParent(parent, true);
                spot.transform.position = new Vector3(
                    centerX + size * 0.18f * sideSign, 0f, socketZ + offset);
                spot.AddComponent<ObjectSpot>();

                BuildSpotMarkerVisual(spot.transform, grade);
            }
        }

        // 스팟 자리 표시 (그레이박스 전용 - 아이템 오브젝트가 들어오면 제거된다)
        static void BuildSpotMarkerVisual(Transform parent, FarmingPointGrade grade)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "SpotMarker";
            marker.transform.SetParent(parent, false);
            marker.transform.localScale = new Vector3(0.5f, 0.12f, 0.5f);
            marker.transform.localPosition = new Vector3(0f, 0.06f, 0f);

            Collider markerCollider = marker.GetComponent<Collider>();

            if (markerCollider != null)
                Destroy(markerCollider);

            GreyboxPalette.Apply(marker, ResolveGradeColor(grade) * 1.4f);
        }

        static Color ResolveGradeColor(FarmingPointGrade grade)
        {
            if (grade == FarmingPointGrade.Hero)
                return HeroPointColor;

            if (grade == FarmingPointGrade.Rare)
                return RarePointColor;

            return NormalPointColor;
        }
    }
}
