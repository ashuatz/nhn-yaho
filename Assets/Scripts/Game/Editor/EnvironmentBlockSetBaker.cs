using System.Collections.Generic;
using Scavenger.ArtTools;
using Scavenger.Field;
using Scavenger.Segment;
using UnityEditor;
using UnityEngine;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// 런타임 배경(EnvironmentRenderer)이 쓸 트림시트 블록 세트를 굽는다
    /// (사용자 지적 2026-07-26: 인스턴싱 배경만 트림시트를 안 쓰고 있었다).
    ///
    /// 배경 블록은 크기가 임의값이라 프리팹 하나를 스케일하면 텍셀 밀도가 블록마다
    /// 달라진다. 그래서 실제 생성기(SegmentEnvironment)를 여러 시드로 돌려
    /// **어떤 크기가 실제로 나오는지 표본을 뜨고**, 셀 격자에 스냅한 크기별로 메시를 굽는다.
    /// 런타임은 이 세트에서 크기를 조회해 같은 메시끼리 인스턴싱으로 묶는다 -
    /// 사전 배치로 되돌아가지 않고도 존이 이어지는 동안 계속 트림시트로 나온다.
    ///
    /// 표본에 없던 크기는 가장 가까운 항목으로 대체된다 (EnvironmentBlockSet.ResolveEntry).
    /// </summary>
    public static class EnvironmentBlockSetBaker
    {
        public const string BlockSetPath = "Assets/Settings/EnvironmentBlockSet.asset";

        /// <summary>표본 시드 수 x 존 수. 실제로 나오는 크기를 넉넉히 덮는다.</summary>
        const int SampleSeeds = 8;
        const int SampleZonesPerSeed = 6;

        /// <summary>구울 크기 종류 상한. 넘치면 자주 나오는 것부터 남긴다.</summary>
        const int MaxEntries = 96;

        [MenuItem("Scavenger/Field Trim Sheet/Bake Background Blocks")]
        public static void BakeBackgroundBlocks()
        {
            EnvironmentBlockSet blockSet = BakeBackgroundBlocksNow();

            if (blockSet == null)
                return;

            Selection.activeObject = blockSet;
        }

        /// <summary>확인 대화 없이 굽는다 (자동화 경로). 실패하면 null.</summary>
        public static EnvironmentBlockSet BakeBackgroundBlocksNow()
        {
            TrimSheetDefinition definition = TrimSheetAssets.EnsureStoneDefinition();

            if (definition == null || !definition.IsReady())
            {
                UnityEngine.Debug.LogError(
                    "[TrimSheet] 규격 에셋이 준비되지 않았다. "
                    + "Scavenger > Trim Sheet > Setup Stone Atlas Assets을 먼저 실행한다.");

                return null;
            }

            EnvironmentBlockSet blockSet = EnsureBlockSet();

            List<Vector3Int> sizes = SampleSizes(blockSet);

            if (sizes.Count == 0)
            {
                UnityEngine.Debug.LogError("[TrimSheet] 배경 블록 크기 표본이 비어 있다.");
                return null;
            }

            TrimSheetEnvBlocks.BeginBuild();

            List<EnvironmentBlockSet.Entry> entries =
                new List<EnvironmentBlockSet.Entry>(sizes.Count);

            long vertexCount = 0;

            try
            {
                for (int i = 0; i < sizes.Count; i++)
                {
                    ReportProgress(i, sizes.Count);

                    Vector3Int cells = sizes[i];
                    Vector3 size = blockSet.CellsToSize(cells);

                    Mesh mesh = TrimSheetEnvBlocks.GetOrBakeSnappedMesh(
                        definition, size, blockSet.cellSpan, out Vector3 _);

                    if (mesh == null)
                        continue;

                    entries.Add(new EnvironmentBlockSet.Entry { cells = cells, mesh = mesh });
                    vertexCount += mesh.vertexCount;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            blockSet.entries = entries;
            blockSet.paletteMaterials = BakePaletteMaterials(definition);

            EditorUtility.SetDirty(blockSet);

            TrimSheetEnvBlocks.FlushBuild();
            AssetDatabase.SaveAssets();

            WireRenderer(blockSet);

            // 크기 종류가 곧 드로우콜 갈래다 - 조용히 늘어나면 안 된다
            UnityEngine.Debug.Log(
                $"[TrimSheet] 배경 블록 세트: 크기 {entries.Count}종, 정점 {vertexCount:N0}개, "
                + $"팔레트 {blockSet.paletteMaterials.Count}장 (셀 {blockSet.cellSpan:0.##}m). "
                + "종류가 많으면 셀 크기를 키운다.");

            return blockSet;
        }

        static EnvironmentBlockSet EnsureBlockSet()
        {
            EnvironmentBlockSet existing =
                AssetDatabase.LoadAssetAtPath<EnvironmentBlockSet>(BlockSetPath);

            if (existing != null)
                return existing;

            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                AssetDatabase.CreateFolder("Assets", "Settings");

            EnvironmentBlockSet created = ScriptableObject.CreateInstance<EnvironmentBlockSet>();
            AssetDatabase.CreateAsset(created, BlockSetPath);

            UnityEngine.Debug.Log($"[TrimSheet] 배경 블록 세트 생성: {BlockSetPath}");

            return created;
        }

        /// <summary>
        /// 실제 생성기를 돌려 나오는 크기를 표본으로 모은다. 자주 나오는 순으로 정렬해
        /// 상한까지 남긴다 - 드물게 한 번 나오는 크기 때문에 메시가 늘어나지 않게.
        /// </summary>
        static List<Vector3Int> SampleSizes(EnvironmentBlockSet blockSet)
        {
            ZoneDefinition definition = LoadZoneDefinition();
            Dictionary<Vector3Int, int> counts = new Dictionary<Vector3Int, int>();

            for (int seed = 0; seed < SampleSeeds; seed++)
            {
                System.Random rng = new System.Random(seed);

                for (int zone = 0; zone < SampleZonesPerSeed; zone++)
                {
                    List<EnvironmentBlock> blocks =
                        SegmentEnvironment.GenerateBlocks(definition, rng);

                    foreach (EnvironmentBlock block in blocks)
                    {
                        Vector3Int cells = blockSet.ResolveCells(block.LocalMatrix.lossyScale);

                        counts.TryGetValue(cells, out int count);
                        counts[cells] = count + 1;
                    }
                }
            }

            List<KeyValuePair<Vector3Int, int>> ordered =
                new List<KeyValuePair<Vector3Int, int>>(counts);

            ordered.Sort((a, b) => b.Value.CompareTo(a.Value));

            List<Vector3Int> sizes = new List<Vector3Int>(Mathf.Min(MaxEntries, ordered.Count));

            for (int i = 0; i < ordered.Count && i < MaxEntries; i++)
                sizes.Add(ordered[i].Key);

            if (ordered.Count > MaxEntries)
            {
                UnityEngine.Debug.LogWarning(
                    $"[TrimSheet] 크기 종류가 {ordered.Count}종이라 상한 {MaxEntries}종까지만 굽는다. "
                    + "나머지는 가장 가까운 크기로 대체된다 (셀 크기를 키우면 종류가 줄어든다).");
            }

            return sizes;
        }

        // 씬/프로젝트의 규격 에셋이 있으면 그것을 쓴다 - 존 폭이 다르면 크기 분포도 달라진다
        static ZoneDefinition LoadZoneDefinition()
        {
            string[] guids = AssetDatabase.FindAssets("t:ZoneDefinition");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ZoneDefinition loaded = AssetDatabase.LoadAssetAtPath<ZoneDefinition>(path);

                if (loaded != null)
                    return loaded;
            }

            return ZoneDefinition.CreateDefault();
        }

        static List<Material> BakePaletteMaterials(TrimSheetDefinition definition)
        {
            Color[] palette = SegmentEnvironment.Palette;
            List<Material> materials = new List<Material>(palette.Length);

            for (int i = 0; i < palette.Length; i++)
            {
                // 팔레트는 기준색 - 밝기 보정은 배경 갈래로 건다 (설정: GreyboxTheme)
                Color themed = GreyboxThemeAccess.Tint(palette[i], Field.GreyboxTone.Background);

                Material material = TrimSheetEnvBlocks.GetPaletteMaterial(
                    definition, i, SegmentEnvironment.PaletteName(i), themed);

                materials.Add(material);
            }

            return materials;
        }

        /// <summary>
        /// 스포너 프리팹의 렌더러에 세트를 물린다. 비어 있을 때만 - 사용자가 다른 세트를
        /// 물려 두었으면 존중한다.
        /// </summary>
        static void WireRenderer(EnvironmentBlockSet blockSet)
        {
            const string spawnerPath = "Assets/Prefabs/SegmentSpawner.prefab";

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spawnerPath);

            if (prefab == null)
                return;

            GameObject contents = PrefabUtility.LoadPrefabContents(spawnerPath);

            // 예외가 나도 반드시 언로드 - 프리팹 스테이지 잔존 방지
            try
            {
                EnvironmentRenderer renderer = contents.GetComponent<EnvironmentRenderer>();

                if (renderer == null)
                    return;

                SerializedObject serialized = new SerializedObject(renderer);
                SerializedProperty property = serialized.FindProperty("blockSet");

                if (property == null || property.objectReferenceValue != null)
                    return;

                property.objectReferenceValue = blockSet;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, spawnerPath);
                UnityEngine.Debug.Log($"[TrimSheet] 배경 블록 세트 배선: {spawnerPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static void ReportProgress(int index, int total)
        {
            EditorUtility.DisplayProgressBar(
                "배경 블록 굽기", $"크기 {index + 1} / {total}", (float)index / total);
        }
    }
}
