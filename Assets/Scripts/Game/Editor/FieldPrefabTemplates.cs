using Scavenger.Field;
using UnityEditor;
using UnityEngine;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// 필드 리소스 프리팹 템플릿 (사용자 지시: 존/파밍 포인트를 프리팹화해 아트가 다듬는다).
    /// 최초 1회 생성만 담당하고, 이미 있으면 절대 덮어쓰지 않는다 - 아트가 다듬은
    /// 형태/머티리얼이 메뉴 재실행으로 사라지지 않게 하는 것이 이 규약의 목적.
    ///
    /// 프리팹 규격 (정본: Docs/agent-temp/아트_협업_스케줄_v0.0.1.md 3장)
    /// - 1블록 = 1유닛, 스케일 1 고정, 보행면 y = 0
    /// - 파밍 포인트: 원점(0,0,0)은 존 가장자리의 구역 중심 (z 중앙).
    ///   기본 아이소 리그(카메라 +x) 기준으로 상단은 -x / 하단은 +x로 뻗고,
    ///   입구 소켓은 z-, 출구 소켓은 z+ 쪽에 둔다 (3층 구조 + 입구/출구 분리).
    ///   리그가 뒤집힌 경우에만 코드가 y축 180도로 돌려 붙인다 (음수 스케일 금지)
    /// - 보행 지형은 콜라이더 필요, 마커(PointSocket / ObjectSpot)는 메시 없음
    /// </summary>
    public static class FieldPrefabTemplates
    {
        const string FieldFolder = "Assets/Prefabs/Field";

        public const string FloorRowPath = "Assets/Prefabs/Field/FloorRow.prefab";
        public const string FarmingPointTopPath = "Assets/Prefabs/Field/FarmingPoint_Top.prefab";
        public const string FarmingPointBottomPath = "Assets/Prefabs/Field/FarmingPoint_Bottom.prefab";
        public const string TileSetPath = "Assets/Settings/FieldTileSet.asset";

        // 바닥 타일 배리에이션 (노이즈로 섞인다). 아트가 메시/머티리얼을 교체한다
        static readonly string[] TilePaths =
        {
            "Assets/Prefabs/Field/Tiles/FloorTile_A.prefab",
            "Assets/Prefabs/Field/Tiles/FloorTile_B.prefab",
            "Assets/Prefabs/Field/Tiles/FloorTile_C.prefab",
        };

        // 기준색 (색상 + 채도). 밝기는 GreyboxTheme이 정한다 -
        // 여기 값을 직접 올리면 재생성 때마다 밝기 창의 조절값을 덮어쓴다
        internal static readonly Color[] TileColors =
        {
            new Color(0.30f, 0.31f, 0.33f),
            new Color(0.27f, 0.29f, 0.31f),
            new Color(0.33f, 0.34f, 0.36f),
        };

        // 그레이박스 기준 규격 (ZoneDefinition 기본값과 맞춘다)
        const float ZoneWidth = 7f;
        const float BlockSize = 1f;
        const float FloorThickness = 0.2f;
        const int SpotCount = 2;

        // 파밍 포인트 3층 규격 (ZoneDefinition의 파밍 포인트 컬럼과 같은 값)
        const float PointPlatformDepth = 5f;
        const float PointLength = 9f;
        const float PointGateLength = 2f;
        const float PointStairLength = 2f;
        const float PointRise = 1f;
        const float MaxStairRiser = 0.25f;
        const float StairRampThickness = 0.4f;
        const float EdgeBlockerHeight = 1.8f;

        internal static readonly Color FloorColor = new Color(0.3f, 0.31f, 0.33f);
        internal static readonly Color TopPointColor = new Color(0.34f, 0.38f, 0.34f);
        internal static readonly Color BottomPointColor = new Color(0.32f, 0.35f, 0.38f);

        [MenuItem("Scavenger/Ensure Field Prefabs")]
        public static void EnsureFieldPrefabs()
        {
            EnsureFolder();

            EnsurePrefab(FloorRowPath, BuildFloorRow);
            EnsurePrefab(FarmingPointTopPath, BuildFarmingPointTop);
            EnsurePrefab(FarmingPointBottomPath, BuildFarmingPointBottom);

            EnsureTilePrefabs();
            EnsureTileSet();

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 파밍 포인트 프리팹을 현재 템플릿(3층 구조)으로 다시 만든다.
        /// EnsureFieldPrefabs는 있는 프리팹을 절대 덮어쓰지 않으므로, 규격이 개정된
        /// 뒤에 갱신하려면 이 명시적 경로가 필요하다 (되돌릴 수 없어 확인을 받는다).
        /// 경로가 같아 GUID가 유지되므로 스포너 프리팹의 참조는 끊기지 않는다.
        /// </summary>
        [MenuItem("Scavenger/Rebuild Farming Point Prefabs")]
        public static void RebuildFarmingPointPrefabs()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "파밍 포인트 프리팹 재생성",
                "FarmingPoint_Top / _Bottom 을 3층 구조 템플릿으로 다시 만든다.\n" +
                "아트가 프리팹에서 직접 다듬은 내용이 있으면 사라진다.",
                "재생성", "취소");

            if (!confirmed)
                return;

            RebuildFarmingPointPrefabsNow();
        }

        /// <summary>
        /// 확인 대화 없이 재생성한다 (자동화/스크립트 경로).
        /// 사람이 누르는 경로는 RebuildFarmingPointPrefabs가 확인을 받는다.
        /// </summary>
        public static void RebuildFarmingPointPrefabsNow()
        {
            EnsureFolder();

            ReplacePrefab(FarmingPointTopPath, BuildFarmingPointTop);
            ReplacePrefab(FarmingPointBottomPath, BuildFarmingPointBottom);

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 바닥 프리팹(행 + 타일 3종)을 현재 템플릿으로 다시 만든다.
        /// 트림시트 경로가 메시를 갈아끼울 때 쓰는 입구이기도 하다
        /// (무엇을 만들지는 GreyboxBlockFactory가 정한다 - 형태 코드는 한 벌).
        /// </summary>
        public static void RebuildFloorPrefabsNow()
        {
            EnsureFolder();

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Field/Tiles"))
                AssetDatabase.CreateFolder(FieldFolder, "Tiles");

            ReplacePrefab(FloorRowPath, BuildFloorRow);

            for (int i = 0; i < TilePaths.Length; i++)
            {
                int index = i;
                ReplacePrefab(TilePaths[index], () => BuildFloorTile(index));
            }

            EnsureTileSet();

            AssetDatabase.SaveAssets();
        }

        [MenuItem("Scavenger/Rebuild Floor Prefabs")]
        public static void RebuildFloorPrefabs()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "바닥 프리팹 재생성",
                "FloorRow / FloorTile_A~C 를 기본 템플릿으로 다시 만든다.\n"
                + "프리팹에서 직접 다듬은 내용이 있으면 사라진다.",
                "재생성", "취소");

            if (!confirmed)
                return;

            RebuildFloorPrefabsNow();
        }

        static void ReplacePrefab(string path, System.Func<GameObject> buildTemplate)
        {
            GameObject template = buildTemplate();
            PrefabUtility.SaveAsPrefabAsset(template, path);
            Object.DestroyImmediate(template);

            UnityEngine.Debug.Log($"[Setup] 필드 프리팹 재생성: {path}");
        }

        // -- 바닥 타일 -------------------------------------------------------

        static void EnsureTilePrefabs()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Field/Tiles"))
                AssetDatabase.CreateFolder(FieldFolder, "Tiles");

            for (int i = 0; i < TilePaths.Length; i++)
            {
                int index = i;
                EnsurePrefab(TilePaths[index], () => BuildFloorTile(index));
            }
        }

        /// <summary>
        /// 1블록 바닥 타일. 원점은 큐브 중심이고, 보행면 보정은 FieldTileSet의
        /// tileSurfaceOffsetY가 담당한다. 콜라이더는 행이 대표하므로 여기서는 뺀다.
        /// </summary>
        static GameObject BuildFloorTile(int index)
        {
            GameObject tile = GreyboxBlockFactory.Create(
                System.IO.Path.GetFileNameWithoutExtension(TilePaths[index]),
                new Vector3(BlockSize, FloorThickness, BlockSize),
                TileColors[index],
                withCollider: false);

            return tile;
        }

        /// <summary>
        /// 타일 구성 데이터. 없을 때만 만들고, 타일 목록이 비어 있으면 채워준다
        /// (사용자가 타일을 추가/교체한 뒤에는 건드리지 않는다).
        /// </summary>
        static void EnsureTileSet()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                AssetDatabase.CreateFolder("Assets", "Settings");

            FieldTileSet tileSet = AssetDatabase.LoadAssetAtPath<FieldTileSet>(TileSetPath);

            if (tileSet == null)
            {
                tileSet = ScriptableObject.CreateInstance<FieldTileSet>();
                AssetDatabase.CreateAsset(tileSet, TileSetPath);
                UnityEngine.Debug.Log($"[Setup] 타일 구성 데이터 생성: {TileSetPath}");
            }

            if (tileSet.tiles.Count > 0)
                return;

            foreach (string tilePath in TilePaths)
            {
                GameObject tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(tilePath);

                if (tilePrefab == null)
                    continue;

                tileSet.tiles.Add(new FieldTileSet.TileEntry { prefab = tilePrefab, weight = 1f });
            }

            EditorUtility.SetDirty(tileSet);
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            if (!AssetDatabase.IsValidFolder(FieldFolder))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Field");
        }

        static void EnsurePrefab(string path, System.Func<GameObject> buildTemplate)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return;

            GameObject template = buildTemplate();
            PrefabUtility.SaveAsPrefabAsset(template, path);
            Object.DestroyImmediate(template);

            UnityEngine.Debug.Log($"[Setup] 필드 프리팹 생성: {path}");
        }

        // -- 존 바닥 행 -------------------------------------------------------

        /// <summary>
        /// 바닥 제거 단위 (존 너비 x 1블록). 코드가 존 길이만큼 복제해 깐다.
        /// 아트는 이 프리팹의 메시/머티리얼을 교체하면 된다 (스케일 규격 유지).
        /// </summary>
        static GameObject BuildFloorRow()
        {
            GameObject row = GreyboxBlockFactory.Create(
                "FloorRow",
                new Vector3(ZoneWidth, FloorThickness, BlockSize),
                FloorColor,
                withCollider: true);

            row.AddComponent<FloorRow>();
            return row;
        }

        // -- 파밍 포인트 -------------------------------------------------------

        static GameObject BuildFarmingPointTop()
        {
            // 상단: 화면 위쪽(-x)으로 뻗고 본선보다 높다
            return BuildFarmingPoint("FarmingPoint_Top", TopPointColor, -1f, PointRise);
        }

        static GameObject BuildFarmingPointBottom()
        {
            // 하단: 화면 아래쪽(+x)으로 뻗고 본선보다 낮다 - 카메라측에 솟는 구조물을
            // 두면 본선 발판이 가려진다 (3층 구조 계획 2.2)
            return BuildFarmingPoint("FarmingPoint_Bottom", BottomPointColor, 1f, -PointRise);
        }

        /// <summary>
        /// 파밍 포인트 그레이박스 (3층 구조 + 입구/출구 분리).
        /// 원점은 존 가장자리의 구역 중심이고, sideSign 방향으로 계단 - 플랫폼이 이어진다.
        /// 입구 계단은 z-, 출구 계단은 z+ 게이트에 놓이며 그 사이는 매스로 막는다.
        /// </summary>
        static GameObject BuildFarmingPoint(
            string prefabName, Color color, float sideSign, float platformY)
        {
            // 생성 에셋 이름은 "프리팹 이름 + 부위" (FarmingPoint_Top_Platform 등)
            using GreyboxBlockFactory.Scope scope = GreyboxBlockFactory.UseContext(prefabName);

            GameObject root = new GameObject(prefabName);

            float platformCenterX = sideSign * (PointStairLength + PointPlatformDepth * 0.5f);
            float innerLength = PointLength - PointGateLength * 2f;

            // 평면 구간 - 상판이 단차 높이에 맞는다
            CreateBlock(
                root.transform, "Platform",
                new Vector3(platformCenterX, platformY - FloorThickness * 0.5f, 0f),
                new Vector3(PointPlatformDepth, FloorThickness, PointLength),
                color);

            // 게이트 사이 차단 매스. 상단은 벽이 되고 하단은 본선과 같은 높이로 채운다
            // (카메라측에 본선보다 솟는 구조물을 두면 발판이 가려진다)
            CreateBlock(
                root.transform, "EdgeBarrier",
                new Vector3(sideSign * PointStairLength * 0.5f, platformY * 0.5f, 0f),
                new Vector3(PointStairLength, Mathf.Abs(platformY), innerLength),
                color * 0.9f);

            // 시각 매스만으로는 위층에서 아래층으로 뛰어내려 계단을 우회할 수 있다.
            // 보이지 않는 차단 콜라이더를 상판 위로 캐릭터 키 이상 세운다
            BuildEdgeBlocker(root.transform, sideSign, platformY, innerLength);

            float gateZ = (PointLength - PointGateLength) * 0.5f;

            BuildStairs(root.transform, "StairsEntry", color, sideSign, platformY, -gateZ);
            BuildStairs(root.transform, "StairsExit", color, sideSign, platformY, gateZ);

            // 포인트 소켓: 존과 맞닿아 드나드는 자리 (제거 판정 기준 좌표)
            CreateSocket(root.transform, "PointSocket_Entry", PointSocketRole.Entry, -gateZ);
            CreateSocket(root.transform, "PointSocket_Exit", PointSocketRole.Exit, gateZ);

            // 오브젝트 스팟: 아이템 오브젝트가 1대1로 생성되는 자리
            for (int i = 0; i < SpotCount; i++)
            {
                float offsetZ = innerLength * (i == 0 ? -0.25f : 0.25f);

                GameObject spot = new GameObject($"ObjectSpot_{i:D2}");
                spot.transform.SetParent(root.transform, false);
                spot.transform.localPosition = new Vector3(platformCenterX, platformY, offsetZ);
                spot.AddComponent<ObjectSpot>();

                BuildSpotMarker(spot.transform, color);
            }

            // 뻗는 방향을 프리팹이 선언한다 - 코드가 타입으로 가정하지 않는다
            FarmingPoint point = root.AddComponent<FarmingPoint>();
            point.authoredSideSign = sideSign;

            return root;
        }

        // 게이트 사이를 막는 보이지 않는 콜라이더 (메시 없음)
        static void BuildEdgeBlocker(
            Transform parent, float sideSign, float platformY, float innerLength)
        {
            float floorY = Mathf.Min(0f, platformY);
            float ceilingY = Mathf.Max(0f, platformY) + EdgeBlockerHeight;

            GameObject blocker = new GameObject("EdgeBlocker");
            blocker.transform.SetParent(parent, false);
            blocker.transform.localPosition = new Vector3(
                sideSign * PointStairLength * 0.5f, (floorY + ceilingY) * 0.5f, 0f);
            blocker.transform.localScale = new Vector3(
                PointStairLength, ceilingY - floorY, innerLength);

            blocker.AddComponent<BoxCollider>();
        }

        /// <summary>
        /// 계단 하나. 시각은 단 큐브들이고 충돌은 대각선을 덮는 램프 하나다
        /// (3층 구조 계획 3.3 - 단마다 CharacterController가 튀는 것을 막는다).
        /// </summary>
        static void BuildStairs(
            Transform parent, string name, Color color,
            float sideSign, float platformY, float gateZ)
        {
            GameObject stairs = new GameObject(name);
            stairs.transform.SetParent(parent, false);
            stairs.transform.localPosition = Vector3.zero;

            int stepCount = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(platformY) / MaxStairRiser));

            float riser = platformY / stepCount;
            float tread = PointStairLength / stepCount;
            float baseY = Mathf.Min(0f, platformY) - FloorThickness;

            for (int step = 0; step < stepCount; step++)
            {
                // 단 상판을 단 중앙의 램프 높이에 맞춘다 - 실제로 걷는 면은 램프이므로
                // (step + 1)로 두면 발이 단 안으로 riser만큼 파묻혀 보인다
                float topY = riser * (step + 0.5f);
                float centerX = sideSign * (tread * step + tread * 0.5f);

                // 에셋 이름은 단 번호 없이 "Step" - 단마다 색 머티리얼을 따로 만들 이유가 없다
                // (메시는 단 높이가 달라 크기별로 갈린다)
                GameObject stepBlock = CreateBlock(
                    stairs.transform, "Step",
                    new Vector3(centerX, (baseY + topY) * 0.5f, gateZ),
                    new Vector3(tread, topY - baseY, PointGateLength),
                    color);

                stepBlock.name = $"Step_{step:D2}";

                // 충돌은 램프가 대표한다 - 단 콜라이더는 걷어낸다
                Collider stepCollider = stepBlock.GetComponent<Collider>();

                if (stepCollider != null)
                    Object.DestroyImmediate(stepCollider);
            }

            BuildStairRamp(stairs.transform, sideSign, platformY, gateZ);
        }

        // 계단 대각선을 덮는 경사 콜라이더 (메시 없음 - 시각은 단 큐브가 담당)
        static void BuildStairRamp(Transform parent, float sideSign, float platformY, float gateZ)
        {
            GameObject ramp = new GameObject("RampCollider");
            ramp.transform.SetParent(parent, false);

            Vector3 slope = new Vector3(sideSign * PointStairLength, platformY, 0f);

            // 상판이 계단 대각선을 정확히 지나도록 중심을 수직으로 내린다
            // (두께 절반을 y로 환산: 1/cos = |slope| / 계단 길이)
            float verticalDrop = StairRampThickness * 0.5f * slope.magnitude / PointStairLength;

            ramp.transform.localPosition = new Vector3(
                sideSign * PointStairLength * 0.5f, platformY * 0.5f - verticalDrop, gateZ);
            ramp.transform.localRotation =
                Quaternion.FromToRotation(Vector3.right, slope.normalized);
            ramp.transform.localScale = new Vector3(
                slope.magnitude, StairRampThickness, PointGateLength);

            ramp.AddComponent<BoxCollider>();
        }

        static void CreateSocket(
            Transform parent, string name, PointSocketRole role, float localZ)
        {
            GameObject socket = new GameObject(name);
            socket.transform.SetParent(parent, false);
            socket.transform.localPosition = new Vector3(0f, 0f, localZ);

            PointSocket marker = socket.AddComponent<PointSocket>();
            marker.role = role;
        }

        static GameObject CreateBlock(
            Transform parent, string name, Vector3 localPosition, Vector3 size, Color color)
        {
            GameObject block = GreyboxBlockFactory.Create(name, size, color, withCollider: true);

            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;

            return block;
        }

        // 스팟 자리 표시 (그레이박스 전용 - 아이템 오브젝트가 배치되면 코드가 지운다)
        static void BuildSpotMarker(Transform parent, Color color)
        {
            GameObject marker = GreyboxBlockFactory.Create(
                "SpotMarker", new Vector3(0.5f, 0.12f, 0.5f), color * 1.4f, withCollider: false);

            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        }

        // 머티리얼 배정은 GreyboxBlockFactory가 담당한다 (색상 기반 디스크 에셋).
        // 인메모리 머티리얼을 프리팹에 저장하면 리로드 후 마젠타가 된다 - 실제 발생
    }
}
