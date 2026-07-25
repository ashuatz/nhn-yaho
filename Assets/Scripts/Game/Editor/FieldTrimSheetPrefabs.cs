using System.Collections.Generic;
using Scavenger.ArtTools;
using UnityEditor;
using UnityEngine;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// 필드 프리팹을 트림시트 메시로 다시 굽는 경로 (사용자 지적 2026-07-26:
    /// 필드 스포너가 트림시트로 만든 에셋을 쓰지 않았다).
    ///
    /// 스포너는 런타임에 프리팹을 인스턴스화할 뿐이라, 아트를 갈아끼우는 지점은
    /// 프리팹이다. 그래서 런타임 코드는 건드리지 않고 프리팹의 메시/머티리얼만
    /// 트림시트 산출물로 바꾼다 - 배경 사전 배치(TrimSheetEnvBlocks)와 같은 방식이다.
    ///
    /// 대상: 바닥 타일 3종 / 바닥 행 / 파밍 포인트 2종 / 아이템 오브젝트 6종.
    /// 형태를 정의하는 코드는 그대로 두고 <see cref="GreyboxBlockFactory.Source"/>만
    /// 갈아끼우므로, 규격이 바뀌어도 두 벌을 따로 고칠 필요가 없다.
    ///
    /// 다만 **직육면체 블록만** 트림시트로 바뀐다 - 빌더가 큐브 메시 생성기라
    /// 항아리의 원기둥 몸통/뚜껑은 프리미티브로 남는다 (박스로 구우면 항아리가 상자가 된다).
    ///
    /// 배경 인스턴싱(EnvironmentRenderer)은 여기서 제외한다 - 블록 크기가 임의값이라
    /// 크기별 메시를 만들면 인스턴싱 배치가 크기 수만큼 쪼개져 드로우콜이 폭증한다.
    /// 배경을 트림시트로 보려면 사전 배치 경로(Environment Authoring)를 쓴다.
    /// </summary>
    public static class FieldTrimSheetPrefabs
    {
        /// <summary>구운 필드 메시 폴더. 크기+색 조합마다 한 장.</summary>
        public const string MeshFolder = "Assets/Art/Meshes/TrimSheet/Field";

        /// <summary>색만 다른 트림시트 머티리얼 폴더.</summary>
        public const string MaterialFolder = "Assets/Materials/Greybox/TrimSheetField";

        /// <summary>셀 크기(m). 기준 규격 = 1m 면에 2x2 타일 (아트 CLAUDE.md).</summary>
        const float CellSpan = 0.5f;

        // 같은 (크기, 색) 조합은 같은 메시를 공유한다. 도메인 리로드에서 비워지지만
        // 에셋은 디스크에 남아 다음 실행에서 다시 읽힌다
        static readonly Dictionary<string, Mesh> MeshCache = new Dictionary<string, Mesh>();

        static int bakedCount;
        static long vertexCount;
        static int blockCount;

        [MenuItem("Scavenger/Field Trim Sheet/Rebuild Field Prefabs")]
        public static void RebuildFieldPrefabs()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "필드 프리팹 트림시트 재생성",
                "바닥 타일/행, 파밍 포인트, 아이템 오브젝트 프리팹을 트림시트 메시로 다시 만든다.\n"
                + "프리팹에서 직접 다듬은 내용이 있으면 사라진다.",
                "재생성", "취소");

            if (!confirmed)
                return;

            RebuildFieldPrefabsNow();
        }

        /// <summary>확인 대화 없이 재생성한다 (자동화 경로).</summary>
        public static void RebuildFieldPrefabsNow()
        {
            TrimSheetDefinition definition = TrimSheetAssets.EnsureStoneDefinition();

            if (definition == null || !definition.IsReady())
            {
                UnityEngine.Debug.LogError(
                    "[TrimSheet] 규격 에셋이 준비되지 않았다. "
                    + "Scavenger > Trim Sheet > Setup Stone Atlas Assets을 먼저 실행한다.");

                return;
            }

            bakedCount = 0;
            vertexCount = 0;
            blockCount = 0;

            // 재생성 1회를 한 세션으로 본다 - 같은 (크기, 색)은 여기서만 공유하고
            // 다음 재생성 때는 다시 굽는다
            MeshCache.Clear();

            // 교체는 반드시 되돌린다 - 남으면 이후의 다른 프리팹까지 트림시트로 나온다.
            // 이전 값을 저장했다 되돌리는 이유는 중첩 호출 대비 (Codex 교차 검토 지적)
            System.Func<Vector3, Color, GameObject> previousSource = GreyboxBlockFactory.Source;

            try
            {
                GreyboxBlockFactory.Source = (size, color) => CreateBlock(definition, size, color);

                FieldPrefabTemplates.RebuildFloorPrefabsNow();
                FieldPrefabTemplates.RebuildFarmingPointPrefabsNow();
                FarmingObjectTemplates.RebuildFarmingObjectPrefabsNow();
            }
            finally
            {
                GreyboxBlockFactory.Source = previousSource;
            }

            AssetDatabase.SaveAssets();

            // 프리미티브 큐브는 블록당 24정점이라 차이가 크다 - 조용히 넘어가면 안 된다
            UnityEngine.Debug.Log(
                $"[TrimSheet] 필드 프리팹 재생성: 블록 {blockCount}개, 정점 {vertexCount:N0}개, "
                + $"새로 구운 메시 {bakedCount}종 (셀 {CellSpan:0.##}m). "
                + "무거우면 셀 크기를 키운다.");
        }

        /// <summary>
        /// 블록 하나를 트림시트 메시로 만든다. 크기는 메시에 구워지므로 스케일은 1이다
        /// (스케일을 걸면 텍셀 밀도가 블록마다 달라져 트림시트를 쓰는 의미가 사라진다).
        /// </summary>
        static GameObject CreateBlock(TrimSheetDefinition definition, Vector3 size, Color color)
        {
            Vector3 clamped = TrimSheetCubeMesh.ClampSize(size);
            Mesh mesh = GetOrBakeMesh(definition, clamped, color);

            // 굽기에 실패하면 null - 팩토리가 프리미티브 큐브로 되돌아간다
            if (mesh == null)
                return null;

            GameObject block = new GameObject("TrimSheetBlock");

            MeshFilter filter = block.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = block.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = EnsureTintedMaterial(definition, color);

            blockCount += 1;
            vertexCount += mesh.vertexCount;

            return block;
        }

        /// <summary>
        /// (크기, 색) 조합별 메시. 색을 키에 넣는 이유는 같은 크기라도 배리에이션
        /// (바닥 타일 A/B/C)이 서로 다른 셀 배치를 갖게 하기 위함이다 -
        /// 시드를 색에서 뽑으므로 같은 색이면 항상 같은 모양이 재현된다.
        /// </summary>
        static Mesh GetOrBakeMesh(TrimSheetDefinition definition, Vector3 size, Color color)
        {
            string meshName = ResolveMeshName(size, color);

            // 재생성 1회 안에서만 공유한다. 디스크에 있다고 재사용하면 셀 크기나
            // 빌더 로직을 바꿔도 기존 메시가 그대로 남는다 (Codex 교차 검토 지적) -
            // 같은 경로에 다시 구워도 SaveMesh가 내용만 갈아끼워 참조는 유지된다
            if (MeshCache.TryGetValue(meshName, out Mesh cached) && cached != null)
                return cached;

            TrimSheetCubeOptions options = new TrimSheetCubeOptions
            {
                size = size,
                worldUnitsPerCell = CellSpan,
                cellMode = TrimSheetCellMode.MixPerTile,
                randomRotation = true,
                shapeJitter = TrimSheetCubeBuilder.DefaultShapeJitter,
                shapePress = TrimSheetCubeBuilder.DefaultShapePress,
                seed = ResolveSeed(size, color),
            };

            Mesh baked = TrimSheetCubeMesh.Build(definition, options);

            if (baked == null)
                return null;

            Mesh saved = TrimSheetAssets.SaveMesh(baked, meshName, MeshFolder);

            if (saved == null)
                return null;

            MeshCache[meshName] = saved;
            bakedCount += 1;

            return saved;
        }

        static Material EnsureTintedMaterial(TrimSheetDefinition definition, Color color)
        {
            string hex = ColorUtility.ToHtmlStringRGB(color);

            Material tinted = TrimSheetAssets.EnsureTintedMaterial(
                definition.material, MaterialFolder, $"TrimSheetField_{hex}", color);

            if (tinted != null)
                return tinted;

            return definition.material;
        }

        // 이름이 곧 에셋 경로다. 크기는 cm 단위 정수로 적어 파일명이 흔들리지 않게 한다
        static string ResolveMeshName(Vector3 size, Color color)
        {
            int x = Mathf.RoundToInt(size.x * 100f);
            int y = Mathf.RoundToInt(size.y * 100f);
            int z = Mathf.RoundToInt(size.z * 100f);

            return $"Field_{x}x{y}x{z}_{ColorUtility.ToHtmlStringRGB(color)}";
        }

        static int ResolveSeed(Vector3 size, Color color)
        {
            int sizeHash = Mathf.RoundToInt(size.x * 100f) * 73856093
                           ^ Mathf.RoundToInt(size.y * 100f) * 19349663
                           ^ Mathf.RoundToInt(size.z * 100f) * 83492791;

            return sizeHash ^ StableHash(ColorUtility.ToHtmlStringRGB(color));
        }

        // string.GetHashCode는 실행마다 값이 달라질 수 있다 - 같은 경로에 다시 구웠을 때
        // 모양이 바뀌면 안 되므로 직접 계산한다 (FNV-1a)
        static int StableHash(string text)
        {
            unchecked
            {
                int hash = (int)2166136261;

                foreach (char character in text)
                {
                    hash ^= character;
                    hash *= 16777619;
                }

                return hash;
            }
        }
    }
}
