using System.Collections.Generic;
using Scavenger.Run;
using Scavenger.Segment;
using UnityEngine;

namespace Scavenger.Field
{
    /// <summary>
    /// FieldSpawner - 파밍 포인트 배치 (파밍 문서 2.4 / 2.5 + 3층 구조 계획).
    /// 존당 개수를 min-max로 뽑고, 최소 간격을 지켜 자리를 잡는다.
    /// 상단/하단 배분은 1번째 상단 고정, 2번째 하단 고정, 3번째부터 50:50.
    ///
    /// 구조 (3층): 상단은 본선보다 높고 하단은 낮으며 계단으로 오르내린다.
    /// z 방향으로 입구(z-)와 출구(z+)가 나뉘고, 그 사이 존 가장자리는 매스로 막혀
    /// 계단이 유일한 동선이 된다 - 들어간 곳으로 되돌아 나오지 않는다.
    ///
    /// 현재는 그레이박스 지형을 코드로 생성한다. 아트 프리팹이 들어오면
    /// 프리팹 인스턴스가 쓰이고, 소켓/스팟은 프리팹 안의 PointSocket / ObjectSpot
    /// 마커에서 읽는다 (계약 동일).
    /// </summary>
    public sealed partial class FieldSpawner
    {
        // 그레이박스 파밍 포인트 색 (등급 위상 구분 - 아트 교체 시 사라진다).
        // 2026-07-26 밝기 보정 (V x1.7) - 프리팹 템플릿 색과 같은 값을 유지한다
        static readonly Color NormalPointColor = new Color(0.58f, 0.65f, 0.58f);
        static readonly Color RarePointColor = new Color(0.48f, 0.65f, 0.85f);
        static readonly Color HeroPointColor = new Color(0.71f, 0.54f, 0.85f);

        // 등급 위상 가중치 (일반 / 희귀 / 영웅). 깊이 스케일링은 후속 작업
        const float RareGradeChance = 0.3f;
        const float HeroGradeChance = 0.1f;

        // 구역이 존 경계에 너무 붙지 않게 두는 여유 (m)
        const float SocketEdgeMargin = 1f;

        // 그레이박스 오브젝트 스팟 개수
        const int GreyboxSpotCount = 2;

        // 그레이박스 바닥 두께 (m). 존 바닥 행과 같은 값
        const float PointFloorThickness = 0.2f;

        // 계단 한 단의 최대 높이 (m). CharacterController Step Offset 0.3에 여유 0.05.
        // 이 값을 넘으면 점프가 없는 이 게임에서 통과 불가 지형이 된다 (3층 구조 계획 3.1)
        const float MaxStairRiser = 0.25f;

        // 단차가 이 값 이하면 3층 구조를 만들지 않는다 (평면 폴백)
        const float FlatRiseThreshold = 0.05f;

        // 계단 대신 충돌을 담당하는 램프 두께 (m). 단마다 CC가 튀는 것을 막는다
        // (3층 구조 계획 3.3: 계단 시각 + 경사로 콜라이더)
        const float StairRampThickness = 0.4f;

        // 게이트 사이를 막는 보이지 않는 콜라이더 높이 (m). 캐릭터 키(1.6) 이상 -
        // 이보다 낮으면 위쪽 층에서 아래층으로 뛰어내려 계단을 우회할 수 있다
        const float EdgeBlockerHeight = 1.8f;

        readonly List<float> placedSocketZ = new List<float>();

        // 구버전 프리팹 경고는 세션당 1회 (존마다 반복되면 콘솔이 묻힌다)
        static bool legacyPrefabWarned;

        /// <summary>파밍 포인트가 차지하는 월드 XZ 영역 (배경 블록 제외에 사용).</summary>
        public struct Footprint
        {
            public float MinX, MaxX, MinZ, MaxZ;

            public bool Overlaps(float minX, float maxX, float minZ, float maxZ)
            {
                if (maxX < MinX || minX > MaxX)
                    return false;

                if (maxZ < MinZ || minZ > MaxZ)
                    return false;

                return true;
            }
        }

        // 현재 빌드 중인 존의 파밍 포인트 영역. BuildSegment가 파밍 포인트를 먼저 만들고
        // 배경을 나중에 만들므로, 배경 생성이 이 목록을 보고 겹치는 블록을 버린다
        readonly List<Footprint> currentFootprints = new List<Footprint>();

        /// <summary>
        /// 파밍 포인트 한 구역의 치수. 존 가장자리(x)와 구역 중심(z)에서 유도된다.
        /// </summary>
        struct PointMetrics
        {
            /// <summary>구역이 붙는 방향 (+1 = +x, -1 = -x).</summary>
            public float SideSign;

            public float ZoneHalfWidth;

            /// <summary>구역 중심 z (두 소켓의 중점).</summary>
            public float CenterZ;

            /// <summary>z 방향 전체 길이.</summary>
            public float Length;

            /// <summary>입구/출구 게이트 z 길이 (계단이 놓이는 구간).</summary>
            public float GateLength;

            /// <summary>계단 x 길이 (존 가장자리에서 플랫폼까지).</summary>
            public float StairLength;

            /// <summary>플랫폼 x 깊이 (평면 구간).</summary>
            public float PlatformDepth;

            /// <summary>플랫폼 높이 (상단 +, 하단 -).</summary>
            public float PlatformY;

            /// <summary>존 밖으로 뻗는 전체 깊이 (계단 + 플랫폼).</summary>
            public float TotalDepth
            {
                get { return StairLength + PlatformDepth; }
            }

            public float MinZ
            {
                get { return CenterZ - Length * 0.5f; }
            }

            public float MaxZ
            {
                get { return CenterZ + Length * 0.5f; }
            }

            /// <summary>입구 게이트 중심 z (z- 쪽).</summary>
            public float EntryZ
            {
                get { return MinZ + GateLength * 0.5f; }
            }

            /// <summary>출구 게이트 중심 z (z+ 쪽).</summary>
            public float ExitZ
            {
                get { return MaxZ - GateLength * 0.5f; }
            }

            /// <summary>존 가장자리 x (드나드는 경계).</summary>
            public float EdgeX
            {
                get { return SideSign * ZoneHalfWidth; }
            }

            /// <summary>단차가 있어 3층 구조를 만드는가.</summary>
            public bool HasRise
            {
                get { return Mathf.Abs(PlatformY) > FlatRiseThreshold; }
            }

            /// <summary>존 가장자리에서 바깥으로 distance만큼 떨어진 x.</summary>
            public float OuterX(float distance)
            {
                return SideSign * (ZoneHalfWidth + distance);
            }
        }

        /// <summary>
        /// 존 양옆에 파밍 포인트를 배치한다. 구역 중심 z는 존 안에서 뽑되
        /// 구역 전체가 존 z 범위를 벗어나지 않도록 여유를 둔다.
        /// </summary>
        void PopulateFarmingPoints(Zone zone)
        {
            // 존마다 초기화가 먼저 - 조기 반환 뒤에 두면 파밍 포인트가 없는 존에서
            // 이전 존의 영역 기록이 남아 배경 필터가 엉뚱한 블록을 지운다
            placedSocketZ.Clear();
            currentFootprints.Clear();

            RunManager run = RunManager.Instance;

            if (run == null || run.Rng == null)
                return;

            System.Random rng = run.Rng;
            int count = Definition.RollFarmingPointCount(rng);

            if (count <= 0)
                return;

            float halfLength = Definition.FarmingPointLength * 0.5f;

            float minCenterZ = zone.StartZ + halfLength + SocketEdgeMargin;
            float maxCenterZ = zone.EndZ - halfLength - SocketEdgeMargin;

            if (maxCenterZ <= minCenterZ)
                return;

            int placed = 0;
            int attempts = 0;
            int maxAttempts = count * 10;

            while (placed < count && attempts < maxAttempts)
            {
                attempts += 1;

                float centerZ = Mathf.Lerp(minCenterZ, maxCenterZ, (float)rng.NextDouble());

                if (!IsSocketSpacingValid(centerZ))
                    continue;

                FarmingPointType pointType = ResolvePointType(placed, rng);
                FarmingPointGrade grade = ResolveGrade(rng);

                BuildFarmingPoint(zone, pointType, grade, centerZ);

                placedSocketZ.Add(centerZ);
                placed += 1;
            }
        }

        bool IsSocketSpacingValid(float centerZ)
        {
            float spacing = Definition.farmingPointSpacingBlocks * Definition.blockSize;

            foreach (float placed in placedSocketZ)
            {
                if (Mathf.Abs(placed - centerZ) < spacing)
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
            Zone zone, FarmingPointType pointType, FarmingPointGrade grade, float centerZ)
        {
            PointMetrics metrics = BuildMetrics(pointType, centerZ);

            FarmingPoint prefab = pointType == FarmingPointType.Top
                ? farmingPointTopPrefab
                : farmingPointBottomPrefab;

            FarmingPoint point = prefab != null
                ? InstantiatePointPrefab(prefab, zone, metrics, pointType, grade)
                : BuildGreyboxFarmingPoint(zone, metrics, pointType, grade);

            // 아트 프리팹이 자기 규격을 선언하면 그것을 안전지대 범위로 쓴다 -
            // 프리팹 크기와 판정 범위가 어긋나면 플랫폼 끝 전에 막히거나 허공을 걷는다
            if (point.authoredDepthBlocks > 0f)
            {
                float authoredDepth = point.authoredDepthBlocks * Definition.blockSize;
                metrics.PlatformDepth = Mathf.Max(0f, authoredDepth - metrics.StairLength);
            }

            if (point.authoredLengthBlocks > 0f)
                metrics.Length = point.authoredLengthBlocks * Definition.blockSize;

            Vector3 entrySocket = new Vector3(metrics.EdgeX, 0f, metrics.EntryZ);
            Vector3 exitSocket = new Vector3(metrics.EdgeX, 0f, metrics.ExitZ);

            // 프리팹 안 소켓 마커가 정본 - 없으면 계산값 폴백
            ResolveSocketMarkers(point, ref entrySocket, ref exitSocket);

            point.Initialize(new FarmingPoint.Layout
            {
                PointType = pointType,
                Grade = grade,
                EntrySocket = entrySocket,
                ExitSocket = exitSocket,
                SideSign = metrics.SideSign,
                ZoneHalfWidth = metrics.ZoneHalfWidth,
                TotalDepth = metrics.TotalDepth,
                Length = metrics.Length,
                PlatformY = metrics.PlatformY,
                ShakeStartDistance =
                    Definition.farmingPointShakeStartBlocks * Definition.blockSize,
            });

            // 스팟 좌표는 Initialize가 수집한 뒤라야 확정된다 (프리팹 마커 기준)
            PopulateObjectSpots(point, grade);

            RecordFootprint(metrics);
        }

        // -- 아이템 오브젝트 (파밍 문서 3장) -----------------------------------

        /// <summary>
        /// 구역 안 오브젝트 스팟마다 아이템 오브젝트를 1대1로 생성한다 (문서 2.2 (3)).
        /// 어떤 등급 오브젝트가 놓일지는 파밍 포인트 등급이 확률로 정한다 (문서 3.2).
        /// </summary>
        void PopulateObjectSpots(FarmingPoint point, FarmingPointGrade pointGrade)
        {
            RunManager run = RunManager.Instance;

            if (run == null || run.Rng == null)
                return;

            System.Random rng = run.Rng;

            foreach (ObjectSpot spot in point.Spots)
            {
                if (spot == null || spot.IsOccupied)
                    continue;

                FarmingPointGrade objectGrade = RollObjectGrade(pointGrade, rng);

                // 그레이박스 자리 표식은 오브젝트가 대신한다 - 겹쳐 두면 두 겹으로 보인다
                ClearSpotMarkers(spot);

                FarmingObject placed = SpawnFarmingObject(spot, objectGrade, rng);

                if (placed == null)
                    continue;

                placed.Initialize(farmingItemCatalog, rng.Next(), ResolveCollectRadius());
                spot.MarkOccupied();
            }
        }

        /// <summary>
        /// 파밍 포인트 등급별 오브젝트 등급 확률 (문서 3.2 "오브젝트 생성 확률").
        /// 표가 컬럼으로 들어오면 이 배열이 데이터 조회로 바뀐다.
        /// </summary>
        static readonly float[][] ObjectGradeChance =
        {
            new[] { 0.75f, 0.22f, 0.03f },
            new[] { 0.40f, 0.45f, 0.15f },
            new[] { 0.15f, 0.40f, 0.45f },
        };

        static FarmingPointGrade RollObjectGrade(FarmingPointGrade pointGrade, System.Random rng)
        {
            float[] chance = ObjectGradeChance[(int)pointGrade];
            float roll = (float)rng.NextDouble();
            float accumulated = 0f;

            for (int i = 0; i < chance.Length; i++)
            {
                accumulated += chance[i];

                if (roll <= accumulated)
                    return (FarmingPointGrade)i;
            }

            return FarmingPointGrade.Normal;
        }

        FarmingObject SpawnFarmingObject(
            ObjectSpot spot, FarmingPointGrade objectGrade, System.Random rng)
        {
            // 진행 방향과 무관하게 놓여 보이도록 요 회전만 흩뿌린다 (시드 기반)
            Quaternion rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

            FarmingObject prefab = PickFarmingObjectPrefab(objectGrade, rng);

            if (prefab != null)
            {
                FarmingObject instance = Instantiate(
                    prefab, spot.transform.position, rotation, spot.transform);

                instance.name = $"ItemObject_{instance.kind}_{objectGrade}";

                return instance;
            }

            return BuildGreyboxFarmingObject(spot, objectGrade, rotation);
        }

        // 등급이 맞는 프리팹 중 하나. 그 등급 프리팹이 없으면 아무 것이나 쓴다 -
        // 등급 하나가 비었다고 스팟이 통째로 비면 파밍할 것이 사라진다
        FarmingObject PickFarmingObjectPrefab(FarmingPointGrade objectGrade, System.Random rng)
        {
            if (farmingObjectPrefabs == null || farmingObjectPrefabs.Count == 0)
                return null;

            List<FarmingObject> matching = new List<FarmingObject>(farmingObjectPrefabs.Count);
            List<FarmingObject> available = new List<FarmingObject>(farmingObjectPrefabs.Count);

            foreach (FarmingObject prefab in farmingObjectPrefabs)
            {
                if (prefab == null)
                    continue;

                available.Add(prefab);

                if (prefab.grade == objectGrade)
                    matching.Add(prefab);
            }

            if (matching.Count > 0)
                return matching[rng.Next(0, matching.Count)];

            if (available.Count > 0)
                return available[rng.Next(0, available.Count)];

            return null;
        }

        // 프리팹 미배선 폴백 - 코드로 상자 하나를 만든다 (아트 프리팹이 오면 대체된다)
        FarmingObject BuildGreyboxFarmingObject(
            ObjectSpot spot, FarmingPointGrade objectGrade, Quaternion rotation)
        {
            GameObject root = new GameObject($"ItemObject_Box_{objectGrade}");
            root.transform.SetParent(spot.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = rotation;

            Color color = ResolveGradeColor(objectGrade);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.7f, 0.5f, 0.7f);
            body.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            GreyboxPalette.Apply(body, color);

            GameObject lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lid.name = "Lid";
            lid.transform.SetParent(root.transform, false);
            lid.transform.localScale = new Vector3(0.76f, 0.1f, 0.76f);
            lid.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            GreyboxPalette.Apply(lid, color * 1.25f);

            FarmingObject farmingObject = root.AddComponent<FarmingObject>();
            farmingObject.kind = FarmingObjectKind.Box;
            farmingObject.grade = objectGrade;
            farmingObject.interactSeconds = FarmingObject.DefaultInteractSeconds(objectGrade);
            farmingObject.lid = lid.transform;

            return farmingObject;
        }

        // 스팟 아래의 그레이박스 표식(SpotMarker)을 걷어낸다.
        // 오브젝트를 붙이기 전에 비워야 새로 만든 오브젝트까지 함께 지우지 않는다
        static void ClearSpotMarkers(ObjectSpot spot)
        {
            for (int i = spot.transform.childCount - 1; i >= 0; i--)
                Destroy(spot.transform.GetChild(i).gameObject);
        }

        /// <summary>
        /// 구역 치수 계산. 상단은 화면 위쪽(카메라 반대편)에 +단차, 하단은 화면
        /// 아래쪽(카메라 쪽)에 -단차. 카메라측에 솟는 지형을 두면 본선 발판이
        /// 가려지므로 방향 판정을 배경과 공유한다 (3층 구조 계획 2.2).
        /// </summary>
        PointMetrics BuildMetrics(FarmingPointType pointType, float centerZ)
        {
            int cameraSide = SegmentEnvironment.CameraSide(
                BuildSightClearance(viewCamera, Definition));

            float sideSign = pointType == FarmingPointType.Top ? -cameraSide : cameraSide;
            float rise = Definition.FarmingPointRise;

            return new PointMetrics
            {
                SideSign = sideSign,
                ZoneHalfWidth = Definition.corridorHalfWidth,
                CenterZ = centerZ,
                Length = Definition.FarmingPointLength,
                GateLength = Definition.FarmingPointGateLength,
                StairLength = Definition.FarmingPointStairLength,
                PlatformDepth = Definition.FarmingPointDepth,
                PlatformY = pointType == FarmingPointType.Top ? rise : -rise,
            };
        }

        // 소켓 마커를 월드 z 순으로 읽는다 (작은 쪽 = 입구, 큰 쪽 = 출구).
        // 역할 필드가 아니라 z를 기준으로 삼는 이유는 리그가 뒤집힌 경우
        // 프리팹을 y축 180도로 돌려 붙이며 z까지 뒤집히기 때문
        static void ResolveSocketMarkers(FarmingPoint point, ref Vector3 entry, ref Vector3 exit)
        {
            PointSocket[] sockets = point.GetComponentsInChildren<PointSocket>(true);

            if (sockets.Length == 0)
                return;

            Vector3 nearest = sockets[0].transform.position;
            Vector3 farthest = sockets[0].transform.position;

            foreach (PointSocket socket in sockets)
            {
                Vector3 position = socket.transform.position;

                if (position.z < nearest.z)
                    nearest = position;

                if (position.z > farthest.z)
                    farthest = position;
            }

            entry = nearest;
            exit = farthest;
        }

        // 배경 블록이 파밍 포인트를 관통하지 않도록 점유 영역을 기록한다.
        // 존 가장자리부터 바깥으로 뻗는 사각형 (계단 길이 포함) + 약간의 여유
        void RecordFootprint(PointMetrics metrics)
        {
            const float FootprintMargin = 0.5f;

            float edgeX = metrics.EdgeX;
            float outerX = metrics.OuterX(metrics.TotalDepth);

            currentFootprints.Add(new Footprint
            {
                MinX = Mathf.Min(edgeX, outerX) - FootprintMargin,
                MaxX = Mathf.Max(edgeX, outerX) + FootprintMargin,
                MinZ = metrics.MinZ - FootprintMargin,
                MaxZ = metrics.MaxZ + FootprintMargin,
            });
        }

        /// <summary>
        /// 아트 프리팹 배치. 프리팹이 뻗는 방향을 authoredSideSign으로 선언받고,
        /// 붙일 방향과 다르면 y축 180도로 돌린다 (음수 스케일은 콜라이더/노멀이
        /// 뒤집히므로 금지) - 이때 z도 함께 뒤집히므로 입구/출구는 마커의 월드 z로 판별한다.
        ///
        /// 방향을 타입으로 가정하지 않는 이유: 구버전(ver01) 프리팹은 상단/하단 모두
        /// +x 규격이라, 타입으로 가정하면 상단이 회전 없이 복도를 침범한다 (실제 발생).
        /// </summary>
        static FarmingPoint InstantiatePointPrefab(
            FarmingPoint prefab, Zone zone, PointMetrics metrics,
            FarmingPointType pointType, FarmingPointGrade grade)
        {
            float authoredSide = prefab.authoredSideSign >= 0f ? 1f : -1f;

            Quaternion rotation = Quaternion.identity;

            if (!Mathf.Approximately(metrics.SideSign, authoredSide))
                rotation = Quaternion.Euler(0f, 180f, 0f);

            Vector3 origin = new Vector3(metrics.EdgeX, 0f, metrics.CenterZ);

            FarmingPoint point = Instantiate(prefab, origin, rotation, zone.transform);
            point.name = $"FarmingPoint_{pointType}_{grade}";

            WarnOnLegacyPrefab(point);

            return point;
        }

        // 구버전 규격 감지 - 소켓이 1개면 입구/출구가 나뉘지 않은 평면 프리팹이다.
        // 방향은 authoredSideSign으로 맞춰지므로 겹치지는 않지만 3층 구조가 없다.
        // 존마다 반복되므로 세션당 1회만 알린다
        static void WarnOnLegacyPrefab(FarmingPoint point)
        {
            if (legacyPrefabWarned)
                return;

            if (point.GetComponentsInChildren<PointSocket>(true).Length >= 2)
                return;

            legacyPrefabWarned = true;

            UnityEngine.Debug.LogWarning(
                $"[Field] {point.name}: 소켓이 1개인 구버전(ver01) 파밍 포인트 프리팹이다. " +
                "3층 구조와 입구/출구가 적용되지 않는다 - " +
                "메뉴 Scavenger > Rebuild Farming Point Prefabs로 재생성할 것.");
        }

        // 프리팹 미배선 폴백 - 코드로 그레이박스 지형을 만든다
        FarmingPoint BuildGreyboxFarmingPoint(
            Zone zone, PointMetrics metrics, FarmingPointType pointType, FarmingPointGrade grade)
        {
            GameObject pointObject = new GameObject($"FarmingPoint_{pointType}_{grade}");
            pointObject.transform.SetParent(zone.transform, true);
            pointObject.transform.position = Vector3.zero;

            BuildGreyboxPoint(pointObject.transform, metrics, grade);

            return pointObject.AddComponent<FarmingPoint>();
        }

        /// <summary>
        /// 그레이박스 지형 생성 (3층 구조). 구성 요소는 다음과 같다.
        /// - 플랫폼: 단차 높이의 평면. 아이템 오브젝트가 놓인다
        /// - 입구/출구 계단: z- / z+ 게이트에 각각. 시각은 계단, 충돌은 램프
        /// - 차단 매스: 게이트 사이 존 가장자리를 막아 계단이 유일한 동선이 되게 한다
        /// 단차가 0에 가까우면 평면 한 장으로 폴백한다 (수치 비교용).
        /// </summary>
        void BuildGreyboxPoint(Transform parent, PointMetrics metrics, FarmingPointGrade grade)
        {
            Color color = ResolveGradeColor(grade);

            if (!metrics.HasRise)
            {
                BuildFlatPlatform(parent, metrics, color);
                BuildSocketMarkers(parent, metrics);
                BuildSpotMarkers(parent, metrics, grade);
                return;
            }

            BuildPlatform(parent, metrics, color);
            BuildEdgeBarrier(parent, metrics, color);

            BuildStairs(parent, metrics, color, metrics.EntryZ, "StairsEntry");
            BuildStairs(parent, metrics, color, metrics.ExitZ, "StairsExit");

            BuildSocketMarkers(parent, metrics);
            BuildSpotMarkers(parent, metrics, grade);
        }

        // 단차 없는 폴백 - 계단 구간까지 한 장으로 덮는다
        void BuildFlatPlatform(Transform parent, PointMetrics metrics, Color color)
        {
            float centerX = metrics.OuterX(metrics.TotalDepth * 0.5f);

            CreateBlock(
                parent, "Platform",
                new Vector3(centerX, -PointFloorThickness * 0.5f, metrics.CenterZ),
                new Vector3(metrics.TotalDepth, PointFloorThickness, metrics.Length),
                color);
        }

        // 평면 구간 - 상판이 단차 높이에 맞는다
        void BuildPlatform(Transform parent, PointMetrics metrics, Color color)
        {
            float centerX = metrics.OuterX(metrics.StairLength + metrics.PlatformDepth * 0.5f);
            float centerY = metrics.PlatformY - PointFloorThickness * 0.5f;

            CreateBlock(
                parent, "Platform",
                new Vector3(centerX, centerY, metrics.CenterZ),
                new Vector3(metrics.PlatformDepth, PointFloorThickness, metrics.Length),
                color);
        }

        /// <summary>
        /// 게이트 사이 차단 매스. 존 가장자리와 플랫폼 사이를 메운다.
        /// 상단은 본선(0)에서 플랫폼까지 솟아 벽이 되고, 하단은 플랫폼에서 본선까지
        /// 채워 상판이 본선과 같은 높이가 된다 - 카메라측(하단)에 본선보다 솟는
        /// 구조물을 두면 본선 발판을 가리기 때문이다.
        ///
        /// 다만 그 형태만으로는 양쪽 모두 넘어갈 수 있다 (하단은 본선에서 상판으로
        /// 걸어 나가 플랫폼으로 뛰어내리고, 상단은 플랫폼에서 본선으로 뛰어내린다).
        /// 그래서 시각 매스 위에 보이지 않는 차단 콜라이더를 함께 세운다 -
        /// 계단이 유일한 동선이 되고, 카메라측 시야도 막지 않는다.
        /// </summary>
        void BuildEdgeBarrier(Transform parent, PointMetrics metrics, Color color)
        {
            float innerLength = metrics.Length - metrics.GateLength * 2f;

            if (innerLength <= 0f)
                return;

            float centerX = metrics.OuterX(metrics.StairLength * 0.5f);
            float height = Mathf.Abs(metrics.PlatformY);

            CreateBlock(
                parent, "EdgeBarrier",
                new Vector3(centerX, metrics.PlatformY * 0.5f, metrics.CenterZ),
                new Vector3(metrics.StairLength, height, innerLength),
                color * 0.9f);

            BuildEdgeBlocker(parent, metrics, centerX, innerLength);
        }

        // 보이지 않는 차단 콜라이더 - 시각 매스의 상판 위로 캐릭터 키 이상 세운다
        void BuildEdgeBlocker(
            Transform parent, PointMetrics metrics, float centerX, float innerLength)
        {
            float floorY = Mathf.Min(0f, metrics.PlatformY);
            float ceilingY = Mathf.Max(0f, metrics.PlatformY) + EdgeBlockerHeight;

            GameObject blocker = new GameObject("EdgeBlocker");
            blocker.transform.SetParent(parent, true);
            blocker.transform.position = new Vector3(
                centerX, (floorY + ceilingY) * 0.5f, metrics.CenterZ);
            blocker.transform.localScale = new Vector3(
                metrics.StairLength, ceilingY - floorY, innerLength);

            blocker.AddComponent<BoxCollider>();
        }

        /// <summary>
        /// 계단 하나. 시각은 단(step) 큐브들이고 충돌은 대각선을 덮는 램프 하나다
        /// (3층 구조 계획 3.3 - 단마다 CharacterController가 튀는 것을 막는다).
        /// 단 높이는 MaxStairRiser 이하로 나눈다 (점프 없는 게임의 통과 조건).
        /// </summary>
        void BuildStairs(
            Transform parent, PointMetrics metrics, Color color, float gateCenterZ, string name)
        {
            GameObject stairs = new GameObject(name);
            stairs.transform.SetParent(parent, true);
            stairs.transform.position = Vector3.zero;

            int stepCount = Mathf.Max(
                1, Mathf.CeilToInt(Mathf.Abs(metrics.PlatformY) / MaxStairRiser));

            float riser = metrics.PlatformY / stepCount;
            float tread = metrics.StairLength / stepCount;

            // 단 아래를 채우는 기준면 - 계단이 공중에 떠 보이지 않게
            float baseY = Mathf.Min(0f, metrics.PlatformY) - PointFloorThickness;

            for (int step = 0; step < stepCount; step++)
            {
                // 단 상판을 단 중앙의 램프 높이에 맞춘다 - 실제로 걷는 면은 램프이므로
                // (step + 1)로 두면 발이 단 안으로 riser만큼 파묻혀 보인다
                float topY = riser * (step + 0.5f);
                float centerX = metrics.OuterX(tread * step + tread * 0.5f);

                GameObject stepBlock = CreateBlock(
                    stairs.transform, $"Step_{step:D2}",
                    new Vector3(centerX, (baseY + topY) * 0.5f, gateCenterZ),
                    new Vector3(tread, topY - baseY, metrics.GateLength),
                    color);

                // 충돌은 램프가 대표한다 - 단 콜라이더는 걷어낸다
                Collider stepCollider = stepBlock.GetComponent<Collider>();

                if (stepCollider != null)
                    Destroy(stepCollider);
            }

            BuildStairRamp(stairs.transform, metrics, gateCenterZ);
        }

        // 계단 대각선을 덮는 경사 콜라이더 (메시 없음 - 시각은 단 큐브가 담당)
        void BuildStairRamp(Transform parent, PointMetrics metrics, float gateCenterZ)
        {
            GameObject ramp = new GameObject("RampCollider");
            ramp.transform.SetParent(parent, true);

            Vector3 slope = new Vector3(
                metrics.SideSign * metrics.StairLength, metrics.PlatformY, 0f);

            Quaternion rotation = Quaternion.FromToRotation(Vector3.right, slope.normalized);

            Vector3 midPoint = new Vector3(
                metrics.OuterX(metrics.StairLength * 0.5f), metrics.PlatformY * 0.5f, gateCenterZ);

            // 상판이 계단 대각선을 정확히 지나도록 중심을 수직으로 내린다.
            // 두께의 절반을 경사면 법선이 아니라 y로 환산한 값 (1/cos = |slope| / 길이) -
            // 좌우 어느 방향으로 뻗어도 부호가 뒤집히지 않는다
            float verticalDrop =
                StairRampThickness * 0.5f * slope.magnitude / metrics.StairLength;

            ramp.transform.position = midPoint + Vector3.down * verticalDrop;
            ramp.transform.rotation = rotation;
            ramp.transform.localScale = new Vector3(
                slope.magnitude, StairRampThickness, metrics.GateLength);

            ramp.AddComponent<BoxCollider>();
        }

        // 포인트 소켓 마커 - 존과 맞닿은 입구(z-) / 출구(z+). 제거 판정 기준 좌표
        void BuildSocketMarkers(Transform parent, PointMetrics metrics)
        {
            CreateSocket(parent, "PointSocket_Entry", PointSocketRole.Entry,
                new Vector3(metrics.EdgeX, 0f, metrics.EntryZ));

            CreateSocket(parent, "PointSocket_Exit", PointSocketRole.Exit,
                new Vector3(metrics.EdgeX, 0f, metrics.ExitZ));
        }

        static void CreateSocket(
            Transform parent, string name, PointSocketRole role, Vector3 worldPosition)
        {
            GameObject socket = new GameObject(name);
            socket.transform.SetParent(parent, true);
            socket.transform.position = worldPosition;

            PointSocket marker = socket.AddComponent<PointSocket>();
            marker.role = role;
        }

        // 오브젝트 스팟 마커 - 아이템 오브젝트가 생성될 자리 (3단계 작업).
        // 게이트를 피해 플랫폼 안쪽에 둔다
        void BuildSpotMarkers(Transform parent, PointMetrics metrics, FarmingPointGrade grade)
        {
            float platformCenterX = metrics.HasRise
                ? metrics.OuterX(metrics.StairLength + metrics.PlatformDepth * 0.5f)
                : metrics.OuterX(metrics.TotalDepth * 0.5f);

            float innerLength = Mathf.Max(0f, metrics.Length - metrics.GateLength * 2f);

            for (int i = 0; i < GreyboxSpotCount; i++)
            {
                float offsetZ = innerLength * (i == 0 ? -0.25f : 0.25f);

                GameObject spot = new GameObject($"ObjectSpot_{i:D2}");
                spot.transform.SetParent(parent, true);
                spot.transform.position = new Vector3(
                    platformCenterX, metrics.PlatformY, metrics.CenterZ + offsetZ);
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

        // 그레이박스 블록 하나 (월드 좌표/크기 지정)
        static GameObject CreateBlock(
            Transform parent, string name, Vector3 worldPosition, Vector3 size, Color color)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, true);
            block.transform.position = worldPosition;
            block.transform.localScale = size;

            GreyboxPalette.Apply(block, color);

            return block;
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
