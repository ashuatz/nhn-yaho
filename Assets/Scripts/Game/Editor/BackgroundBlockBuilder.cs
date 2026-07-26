using System.Collections.Generic;
using Scavenger.ArtTools;
using Scavenger.Field;
using Scavenger.Player;
using Scavenger.Segment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// 배경 블록을 에디터 타임에 실제 GameObject로 생성하는 공용 유틸.
    ///
    /// 런타임 배경은 GPU 인스턴싱 드로우(EnvironmentRenderer, ADR-0005)라
    /// 에디트 모드에서는 아무것도 보이지 않는다. 룩을 확인하거나 손으로 다듬으려면
    /// 실제 오브젝트가 필요해 이 경로를 쓴다.
    ///
    /// FieldSpawner 인스펙터 버튼과 Environment Authoring 윈도우가 함께 호출한다.
    /// 두 입구가 같은 결과를 내도록 생성 로직은 여기에만 둔다.
    /// </summary>
    static class BackgroundBlockBuilder
    {
        public const string RootName = "PreplacedEnvironment";

        public const int MinZoneCount = 1;
        public const int MaxZoneCount = 50;

        /// <summary>이 개수를 넘으면 생성 전에 한 번 물어본다.</summary>
        const int ConfirmBlockCount = 3000;

        public static EnvironmentAuthoring FindExisting()
        {
            return Object.FindFirstObjectByType<EnvironmentAuthoring>();
        }

        /// <summary>사전 배치된 배경 블록 수. 인스펙터 표시용.</summary>
        public static int CountBlocks(EnvironmentAuthoring authoring)
        {
            if (authoring == null)
                return 0;

            // 블록은 청크 아래 자식으로만 존재한다 (루트 -> 청크 -> 블록)
            return authoring.GetComponentsInChildren<MeshRenderer>(true).Length;
        }

        public static EnvironmentAuthoring Build(ZoneDefinition definition, int seed, int zoneCount)
        {
            return Build(definition, seed, zoneCount, useTrimSheetBlocks: false, TrimSheetEnvBlocks.DefaultEnvCellSpan);
        }

        /// <summary>
        /// 배경 블록을 실제 오브젝트로 생성한다. 기존 사전 배치는 먼저 지운다.
        /// </summary>
        /// <param name="useTrimSheetBlocks">
        /// true면 단색 프리미티브 큐브 대신 트림시트 블록 프리팹을 인스턴스화한다.
        /// 크기는 셀 격자에 스냅되고 크기별 메시가 재사용된다.
        /// </param>
        /// <returns>생성된 마커. 사용자가 취소하면 null.</returns>
        public static EnvironmentAuthoring Build(
            ZoneDefinition definition, int seed, int zoneCount, bool useTrimSheetBlocks, float trimSheetCellSpan)
        {
            ZoneDefinition activeDefinition = definition;

            if (activeDefinition == null)
                activeDefinition = ZoneDefinition.CreateDefault();

            int clampedZoneCount = Mathf.Clamp(zoneCount, MinZoneCount, MaxZoneCount);

            if (!ConfirmLargeBuild(activeDefinition, clampedZoneCount))
                return null;

            TrimSheetBlockSource trimSheetSource = default;

            if (useTrimSheetBlocks && !TrimSheetBlockSource.TryCreate(trimSheetCellSpan, out trimSheetSource))
                return null;

            Clear();

            GameObject root = new GameObject(RootName);
            EnvironmentAuthoring authoring = root.AddComponent<EnvironmentAuthoring>();

            System.Random rng = new System.Random(seed);

            // 씬 카메라 기준 시야 클리어런스 - 런타임 생성과 동일 규칙 적용
            FollowCamera sceneCamera = Object.FindFirstObjectByType<FollowCamera>();
            SightClearance clearance = FieldSpawner.BuildSightClearance(sceneCamera, activeDefinition);

            try
            {
                for (int i = 0; i < clampedZoneCount; i++)
                {
                    ReportProgress(i, clampedZoneCount);

                    GameObject chunk = new GameObject($"EnvChunk_{i}");
                    chunk.transform.SetParent(root.transform, false);
                    chunk.transform.localPosition = new Vector3(0f, 0f, i * activeDefinition.lengthMeters);

                    if (useTrimSheetBlocks)
                    {
                        trimSheetSource.BuildChunk(chunk.transform, activeDefinition, rng, clearance);
                        continue;
                    }

                    SegmentEnvironment.BuildGameObjects(chunk.transform, activeDefinition, rng, clearance);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (useTrimSheetBlocks)
                trimSheetSource.ReportAndFlush();

            authoring.coveredFromZ = 0f;
            authoring.coveredToZ = clampedZoneCount * activeDefinition.lengthMeters;

            Undo.RegisterCreatedObjectUndo(root, "Build Background Blocks");
            EditorSceneManager.MarkSceneDirty(root.scene);

            return authoring;
        }

        public static void Clear()
        {
            EnvironmentAuthoring existing = FindExisting();

            if (existing == null)
                return;

            UnityEngine.SceneManagement.Scene scene = existing.gameObject.scene;
            Undo.DestroyObjectImmediate(existing.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>
        /// 존 하나가 수백 블록이라 존 수가 많으면 오브젝트가 수천 개가 된다.
        /// 실수로 씬을 무겁게 만드는 것을 막는다.
        /// </summary>
        static bool ConfirmLargeBuild(ZoneDefinition definition, int zoneCount)
        {
            int estimated = EstimateBlockCount(definition, zoneCount);

            if (estimated < ConfirmBlockCount)
                return true;

            return EditorUtility.DisplayDialog(
                "배경 블록 생성",
                $"블록이 약 {estimated}개 생성된다 (존 {zoneCount}개). "
                + "씬이 무거워지고 저장 용량이 늘어난다. 계속할까?",
                "생성",
                "취소");
        }

        /// <summary>
        /// 실제 개수는 클리어런스 거부와 난수에 따라 달라진다.
        /// 경고 판단용 대략치만 낸다 (한 존을 실제로 생성해 표본을 얻는다).
        /// </summary>
        static int EstimateBlockCount(ZoneDefinition definition, int zoneCount)
        {
            System.Random probeRng = new System.Random(0);
            int perZone = SegmentEnvironment.GenerateBlocks(definition, probeRng).Count;

            return perZone * zoneCount;
        }

        static void ReportProgress(int zoneIndex, int zoneCount)
        {
            EditorUtility.DisplayProgressBar(
                "배경 블록 생성",
                $"존 {zoneIndex + 1} / {zoneCount}",
                (float)zoneIndex / zoneCount);
        }

        /// <summary>
        /// 트림시트 블록 프리팹으로 배경을 채우는 백엔드.
        ///
        /// <see cref="SegmentEnvironment.GenerateBlocks"/>가 낸 데이터를 그대로 소비한다 -
        /// 배치 규칙(클리어런스, 팔레트, 레이어)은 건드리지 않고 무엇을 인스턴스화할지만 바꾼다.
        /// </summary>
        struct TrimSheetBlockSource
        {
            GameObject prefab;
            TrimSheetDefinition definition;
            float cellSpan;
            int instanceCount;
            long vertexCount;

            public static bool TryCreate(float cellSpan, out TrimSheetBlockSource source)
            {
                source = default;

                TrimSheetDefinition definition = TrimSheetAssets.EnsureStoneDefinition();

                if (definition == null || !definition.IsReady())
                {
                    Debug.LogError("[TrimSheet] 규격 에셋이 준비되지 않았다. "
                                   + "Scavenger > Trim Sheet > Setup Stone Atlas Assets을 먼저 실행한다.");

                    return false;
                }

                GameObject prefab = TrimSheetEnvBlocks.EnsurePrefab();

                if (prefab == null)
                    return false;

                TrimSheetEnvBlocks.BeginBuild();

                source = new TrimSheetBlockSource
                {
                    prefab = prefab,
                    definition = definition,
                    cellSpan = cellSpan,
                };

                return true;
            }

            public void BuildChunk(
                Transform chunk, ZoneDefinition definition2, System.Random rng, SightClearance clearance)
            {
                List<EnvironmentBlock> blocks = SegmentEnvironment.GenerateBlocks(definition2, rng, clearance);

                foreach (EnvironmentBlock block in blocks)
                    PlaceBlock(chunk, block);
            }

            void PlaceBlock(Transform chunk, EnvironmentBlock block)
            {
                Matrix4x4 matrix = block.LocalMatrix;
                Vector3 scale = matrix.lossyScale;

                Mesh mesh = TrimSheetEnvBlocks.GetOrBakeSnappedMesh(
                    definition, scale, cellSpan, out Vector3 snappedSize);

                if (mesh == null)
                    return;

                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, chunk) as GameObject;

                if (instance == null)
                    return;

                instance.name = "EnvBlock";

                // 스냅된 크기가 메시에 구워져 있으므로 스케일은 1로 둔다.
                // 여기서 스케일을 다시 걸면 텍셀 밀도가 어긋난다.
                instance.transform.localPosition = matrix.GetColumn(3);
                instance.transform.localRotation = matrix.rotation;
                instance.transform.localScale = Vector3.one;

                // 피벗이 중심이라 스냅으로 크기가 바뀌어도 중심은 그대로 유지된다.
                // 바닥선을 맞추고 싶으면 여기서 y를 (snapped.y - scale.y) * 0.5f 만큼 올린다.
                MeshFilter filter = instance.GetComponent<MeshFilter>();

                if (filter != null)
                    filter.sharedMesh = mesh;

                MeshRenderer renderer = instance.GetComponent<MeshRenderer>();

                if (renderer != null)
                {
                    Color paletteColor = GreyboxThemeAccess.Tint(
                        SegmentEnvironment.Palette[block.PaletteIndex], Field.GreyboxTone.Background);

                    renderer.sharedMaterial = TrimSheetEnvBlocks.GetPaletteMaterial(
                        definition, block.PaletteIndex,
                        SegmentEnvironment.PaletteName(block.PaletteIndex), paletteColor);
                }

                instanceCount++;
                vertexCount += mesh.vertexCount;
            }

            /// <summary>
            /// 정점 총량을 남긴다. 트림시트 블록은 프리미티브 큐브(24정점)보다 훨씬 무거워
            /// 조용히 넘어가면 씬이 왜 무거워졌는지 알 수 없다.
            /// </summary>
            public void ReportAndFlush()
            {
                TrimSheetEnvBlocks.FlushBuild();

                Debug.Log(
                    $"[TrimSheet] 배경 블록 {instanceCount}개 배치, 정점 {vertexCount:N0}개, "
                    + $"새로 구운 크기별 메시 {TrimSheetEnvBlocks.BakedThisSession}종 "
                    + $"(셀 {cellSpan:0.##}m). 무거우면 셀 크기를 키운다.");
            }
        }
    }
}
