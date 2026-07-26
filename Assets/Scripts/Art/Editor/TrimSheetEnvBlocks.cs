using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Scavenger.ArtTools
{
    /// <summary>
    /// 배경 사전 배치가 쓰는 트림시트 블록 공급자.
    ///
    /// 배경 블록은 스케일이 임의값(3.7 x 12.4 x 2.1 등)이라 프리팹 하나를 그대로
    /// 스케일하면 텍셀 밀도가 블록마다 달라진다 - 트림시트를 쓰는 이유가 사라진다.
    /// 그래서 크기를 셀 격자에 스냅하고 스냅된 크기별로 메시를 구워 재사용한다.
    /// 같은 크기 블록은 같은 메시를 공유하므로 에셋 수는 등장한 크기 종류만큼만 늘어난다.
    /// </summary>
    public static class TrimSheetEnvBlocks
    {
        public const string PrefabPath = "Assets/Prefabs/Env/TrimSheetBlock.prefab";
        public const string EnvMeshFolder = "Assets/Art/Meshes/TrimSheet/Env";

        /// <summary>
        /// 배경 기본 셀 크기(m). 히어로 블록(0.5m)보다 거칠게 잡는다 -
        /// 배경은 멀리 있고, 셀이 작으면 큰 블록 하나가 수천 쿼드가 된다.
        /// </summary>
        public const float DefaultEnvCellSpan = 1f;

        /// <summary>
        /// (셀 수, 셀 크기) -> 구운 메시. 도메인 리로드에서 비워진다 (에셋은 남는다).
        /// **셀 크기를 키에 넣어야 한다** - 셀 1m의 1x1x1과 셀 2m의 1x1x1은 크기가
        /// 다른 메시인데, 셀 수만으로 키를 잡으면 같은 에셋을 공유해 서로를 덮어쓴다
        /// (Codex 교차 검토: 사전 배치는 1m, 런타임 블록 세트는 2m를 쓴다).
        /// </summary>
        static readonly Dictionary<string, Mesh> MeshCache = new Dictionary<string, Mesh>();

        static readonly Dictionary<int, Material> PaletteMaterialCache = new Dictionary<int, Material>();

        /// <summary>이번 세션에 새로 구운 메시 수. 빌드 후 보고용.</summary>
        public static int BakedThisSession { get; private set; }

        /// <summary>
        /// 배경 블록 프리팹을 보장한다. 사용자가 프리팹을 직접 수정해 튜닝할 수 있도록
        /// 씬에는 프리팹 인스턴스로 들어간다 (프로젝트 규약: 시스템은 프리팹으로 관리).
        ///
        /// 콜라이더는 붙이지 않는다 - 배경은 비주얼 전용 (Segment 규칙).
        /// </summary>
        public static GameObject EnsurePrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (existing != null)
                return existing;

            TrimSheetDefinition definition = TrimSheetAssets.EnsureStoneDefinition();

            if (definition == null)
                return null;

            Mesh unitMesh = GetOrBakeSnappedMesh(
                definition,
                Vector3.one,
                DefaultEnvCellSpan,
                out Vector3 _);

            if (unitMesh == null)
                return null;

            GameObject temp = new GameObject("TrimSheetBlock");

            MeshFilter filter = temp.AddComponent<MeshFilter>();
            filter.sharedMesh = unitMesh;

            MeshRenderer renderer = temp.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = definition.material;

            TrimSheetAssets.EnsureFolder("Assets/Prefabs/Env");

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
            Object.DestroyImmediate(temp);

            if (prefab == null)
                Debug.LogError($"[TrimSheet] 프리팹 저장 실패: {PrefabPath}");

            return prefab;
        }

        /// <summary>
        /// 크기를 셀 격자에 스냅하고 그 크기의 메시를 돌려준다 (없으면 굽는다).
        /// </summary>
        /// <param name="snappedSize">스냅된 실제 크기. 호출자가 위치 보정에 쓴다.</param>
        public static Mesh GetOrBakeSnappedMesh(
            TrimSheetDefinition definition,
            Vector3 size,
            float cellSpan,
            out Vector3 snappedSize)
        {
            snappedSize = Vector3.one;

            if (definition == null)
                return null;

            float span = Mathf.Max(TrimSheetDefinition.MinWorldUnitsPerCell, cellSpan);
            Vector3Int cells = ResolveCellCounts(size, span);

            snappedSize = new Vector3(cells.x * span, cells.y * span, cells.z * span);

            // 이름에 셀 크기를 포함한다 - 같은 셀 수라도 셀 크기가 다르면 다른 메시다
            string meshName = $"Env_Block_{cells.x}x{cells.y}x{cells.z}_cell{Mathf.RoundToInt(span * 100f)}";

            if (MeshCache.TryGetValue(meshName, out Mesh cached) && cached != null)
                return cached;

            string path = $"{EnvMeshFolder}/{meshName}.asset";

            Mesh onDisk = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (onDisk != null)
            {
                MeshCache[meshName] = onDisk;
                return onDisk;
            }

            Mesh baked = BakeSnappedMesh(definition, snappedSize, span, cells);

            if (baked == null)
                return null;

            Mesh saved = TrimSheetAssets.SaveMesh(baked, meshName, EnvMeshFolder);

            if (saved == null)
                return null;

            MeshCache[meshName] = saved;
            BakedThisSession++;

            return saved;
        }

        static Mesh BakeSnappedMesh(
            TrimSheetDefinition definition,
            Vector3 snappedSize,
            float span,
            Vector3Int cells)
        {
            TrimSheetCubeOptions options = new TrimSheetCubeOptions
            {
                size = snappedSize,
                worldUnitsPerCell = span,
                cellMode = TrimSheetCellMode.MixPerTile,
                randomRotation = true,
                shapeJitter = TrimSheetCubeBuilder.DefaultShapeJitter,
                shapePress = TrimSheetCubeBuilder.DefaultShapePress,

                // 크기에서 시드를 뽑는다 - 같은 크기는 같은 모양(메시 공유),
                // 다른 크기는 다른 모양이 나온다
                seed = ResolveSizeSeed(cells),
            };

            return TrimSheetCubeMesh.Build(definition, options);
        }

        /// <summary>
        /// 축별 셀 수. 최소 1셀, 상한은 빌더와 같은 축당 상한이라
        /// 아주 큰 배경 블록도 정점 수가 폭발하지 않는다 (그만큼 타일이 늘어난다).
        /// </summary>
        static Vector3Int ResolveCellCounts(Vector3 size, float span)
        {
            return new Vector3Int(
                ResolveAxisCells(size.x, span),
                ResolveAxisCells(size.y, span),
                ResolveAxisCells(size.z, span));
        }

        static int ResolveAxisCells(float length, float span)
        {
            int count = Mathf.RoundToInt(Mathf.Abs(length) / span);

            return Mathf.Clamp(count, 1, TrimSheetCubeMesh.MaxTilesPerAxis);
        }

        static int ResolveSizeSeed(Vector3Int cells)
        {
            return cells.x * 73856093 ^ cells.y * 19349663 ^ cells.z * 83492791;
        }

        /// <summary>
        /// 팔레트 인덱스에 해당하는 틴트 머티리얼. 배경의 깊이 구분이 팔레트 색이라
        /// 트림시트로 바꿔도 색 단계는 유지해야 한다.
        /// </summary>
        public static Material GetPaletteMaterial(
            TrimSheetDefinition definition, int paletteIndex, string roleName, Color color)
        {
            if (definition == null)
                return null;

            if (PaletteMaterialCache.TryGetValue(paletteIndex, out Material cached) && cached != null)
                return cached;

            Material material = TrimSheetAssets.EnsurePaletteMaterial(
                definition.material, paletteIndex, roleName, color);

            if (material == null)
                return definition.material;

            PaletteMaterialCache[paletteIndex] = material;

            return material;
        }

        /// <summary>빌드 1회 단위로 통계를 초기화한다.</summary>
        public static void BeginBuild()
        {
            BakedThisSession = 0;
        }

        /// <summary>구운 메시 에셋을 디스크에 반영한다. 빌드 끝에 1회 호출.</summary>
        public static void FlushBuild()
        {
            if (BakedThisSession <= 0)
                return;

            AssetDatabase.SaveAssets();
        }
    }
}
