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
    /// - 파밍 포인트: 포인트 소켓이 원점(0,0,0), +x 방향으로 뻗는다.
    ///   반대편은 코드가 y축 180도 회전으로 붙인다 (음수 스케일 금지)
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

        static readonly Color[] TileColors =
        {
            new Color(0.30f, 0.31f, 0.33f),
            new Color(0.27f, 0.29f, 0.31f),
            new Color(0.33f, 0.34f, 0.36f),
        };

        // 그레이박스 기준 규격 (ZoneDefinition 기본값과 맞춘다)
        const float ZoneWidth = 7f;
        const float BlockSize = 1f;
        const float FloorThickness = 0.2f;
        const float PointSize = 5f;
        const int SpotCount = 2;

        static readonly Color FloorColor = new Color(0.3f, 0.31f, 0.33f);
        static readonly Color TopPointColor = new Color(0.34f, 0.38f, 0.34f);
        static readonly Color BottomPointColor = new Color(0.32f, 0.35f, 0.38f);

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
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = System.IO.Path.GetFileNameWithoutExtension(TilePaths[index]);
            tile.transform.localScale = new Vector3(BlockSize, FloorThickness, BlockSize);

            Collider tileCollider = tile.GetComponent<Collider>();

            if (tileCollider != null)
                Object.DestroyImmediate(tileCollider);

            AssignCommonMaterial(tile, TileColors[index]);
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
            GameObject row = GameObject.CreatePrimitive(PrimitiveType.Cube);
            row.name = "FloorRow";
            row.transform.localScale = new Vector3(ZoneWidth, FloorThickness, BlockSize);

            AssignCommonMaterial(row, FloorColor);

            row.AddComponent<FloorRow>();
            return row;
        }

        // -- 파밍 포인트 -------------------------------------------------------

        static GameObject BuildFarmingPointTop()
        {
            return BuildFarmingPoint("FarmingPoint_Top", TopPointColor);
        }

        static GameObject BuildFarmingPointBottom()
        {
            return BuildFarmingPoint("FarmingPoint_Bottom", BottomPointColor);
        }

        /// <summary>
        /// 파밍 포인트 그레이박스. 소켓이 원점이고 +x로 뻗는 규격.
        /// 단차(상단 높게 / 하단 낮게)는 아트 R&D 항목이라 지금은 평면이며,
        /// 상단/하단 프리팹의 형태 차이는 아트가 넣는다 (파밍 문서 2.4 (1)).
        /// </summary>
        static GameObject BuildFarmingPoint(string prefabName, Color color)
        {
            GameObject root = new GameObject(prefabName);

            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Platform";
            platform.transform.SetParent(root.transform, false);
            platform.transform.localPosition = new Vector3(PointSize * 0.5f, -FloorThickness * 0.5f, 0f);
            platform.transform.localScale = new Vector3(PointSize, FloorThickness, PointSize);

            AssignCommonMaterial(platform, color);

            // 포인트 소켓: 존과 맞닿은 입구. 프리팹 원점에 둔다 (제거 판정 기준 좌표)
            GameObject socket = new GameObject("PointSocket");
            socket.transform.SetParent(root.transform, false);
            socket.transform.localPosition = Vector3.zero;
            socket.AddComponent<PointSocket>();

            // 오브젝트 스팟: 아이템 오브젝트가 1대1로 생성되는 자리
            for (int i = 0; i < SpotCount; i++)
            {
                float offsetZ = PointSize * (i == 0 ? -0.22f : 0.22f);

                GameObject spot = new GameObject($"ObjectSpot_{i:D2}");
                spot.transform.SetParent(root.transform, false);
                spot.transform.localPosition = new Vector3(PointSize * 0.62f, 0f, offsetZ);
                spot.AddComponent<ObjectSpot>();

                BuildSpotMarker(spot.transform, color);
            }

            root.AddComponent<FarmingPoint>();
            return root;
        }

        // 스팟 자리 표시 (그레이박스 전용 - 아이템 오브젝트가 들어오면 지운다)
        static void BuildSpotMarker(Transform parent, Color color)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "SpotMarker";
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            marker.transform.localScale = new Vector3(0.5f, 0.12f, 0.5f);

            Collider markerCollider = marker.GetComponent<Collider>();

            if (markerCollider != null)
                Object.DestroyImmediate(markerCollider);

            AssignCommonMaterial(marker, color * 1.4f);
        }

        /// <summary>
        /// 런타임 생성물과 같은 기준 머티리얼(Common.mat)을 쓴다.
        /// 색만 다른 경우는 색상 기반 에셋으로 분기해 프리팹 저장 후에도 참조가 안정적이다
        /// (인메모리 머티리얼을 프리팹에 저장하면 리로드 후 마젠타가 된다 - 실제 발생).
        /// </summary>
        static void AssignCommonMaterial(GameObject target, Color color)
        {
            Renderer targetRenderer = target.GetComponent<Renderer>();

            if (targetRenderer == null)
                return;

            targetRenderer.sharedMaterial = GreyboxMaterials.EnsureForColor(color);
        }
    }
}
