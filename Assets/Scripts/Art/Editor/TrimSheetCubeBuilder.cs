using UnityEditor;
using UnityEngine;

namespace Scavenger.ArtTools
{
    /// <summary>바리에이션 세트 한 항목. 큐브 크기 + 셀 사용 방식 조합.</summary>
    public readonly struct TrimSheetVariation
    {
        public readonly string name;
        public readonly Vector3 size;
        public readonly float worldUnitsPerCell;
        public readonly TrimSheetCellMode cellMode;
        public readonly int cellIndex;
        public readonly bool randomRotation;
        public readonly float shapeJitter;
        public readonly float shapePress;

        public TrimSheetVariation(
            string name,
            Vector3 size,
            float worldUnitsPerCell,
            TrimSheetCellMode cellMode,
            int cellIndex,
            bool randomRotation,
            float shapeJitter,
            float shapePress)
        {
            this.name = name;
            this.size = size;
            this.worldUnitsPerCell = worldUnitsPerCell;
            this.cellMode = cellMode;
            this.cellIndex = cellIndex;
            this.randomRotation = randomRotation;
            this.shapeJitter = shapeJitter;
            this.shapePress = shapePress;
        }
    }

    /// <summary>
    /// 씬에 트림시트 큐브를 배치한다. 창(<see cref="TrimSheetCubeWindow"/>)과
    /// 메뉴가 같은 결과를 내야 하므로 생성 로직은 여기만 갖는다.
    /// </summary>
    public static class TrimSheetCubeBuilder
    {
        public const string VariationRootName = "TrimSheet Variations";

        const float VariationGap = 1.5f;

        /// <summary>
        /// 기준 블록 크기(m). 1m 큐브 + 셀 0.5m = 면마다 2x2 타일 (사용자 지정 규격).
        /// </summary>
        public const float UnitBlockSize = 1f;

        /// <summary>기준 셀 크기(m). 1m 면을 2등분한다.</summary>
        public const float UnitCellSpan = 0.5f;

        /// <summary>기본 격자점 산포 / 눌림 크기(m). 1m 블록 / 0.5m 타일 기준으로 잡았다.</summary>
        public const float DefaultShapeJitter = 0.018f;
        public const float DefaultShapePress = 0.03f;

        /// <summary>
        /// 룩 확인용 표준 세트. 기준은 1m 큐브 / 면마다 2x2 타일이고,
        /// 거기서 깨끗한 셀 단독 / 크랙 셀 단독 / 가중 혼합 / 배수 크기 / 얇은 판으로 벌린다.
        /// 셀 인덱스 0,1 = 깨끗, 2,3 = 크랙 (TrimSheetCrackBaker 배치).
        ///
        /// 첫 항목은 변위 0 - 정점을 누른 결과를 비교할 기준으로 남겨둔다.
        /// 나머지는 시드가 서로 달라 같은 크기여도 쉐입이 조금씩 다르게 나온다.
        /// </summary>
        public static readonly TrimSheetVariation[] Variations =
        {
            new TrimSheetVariation("Block_Flat_Reference", UnitCube(), UnitCellSpan, TrimSheetCellMode.Single, 0, false, 0f, 0f),
            new TrimSheetVariation("Block_Clean_A", UnitCube(), UnitCellSpan, TrimSheetCellMode.Single, 0, false, DefaultShapeJitter, DefaultShapePress),
            new TrimSheetVariation("Block_Clean_B", UnitCube(), UnitCellSpan, TrimSheetCellMode.Single, 1, false, DefaultShapeJitter, DefaultShapePress),
            new TrimSheetVariation("Block_Cracked_A", UnitCube(), UnitCellSpan, TrimSheetCellMode.Single, 2, false, DefaultShapeJitter, DefaultShapePress),
            new TrimSheetVariation("Block_Cracked_B", UnitCube(), UnitCellSpan, TrimSheetCellMode.Single, 3, false, DefaultShapeJitter, DefaultShapePress),
            new TrimSheetVariation("Block_Mix_01", UnitCube(), UnitCellSpan, TrimSheetCellMode.MixPerTile, 0, true, DefaultShapeJitter, DefaultShapePress),
            new TrimSheetVariation("Block_Mix_02", UnitCube(), UnitCellSpan, TrimSheetCellMode.MixPerTile, 0, true, DefaultShapeJitter, DefaultShapePress),
            new TrimSheetVariation("Block_Mix_03", UnitCube(), UnitCellSpan, TrimSheetCellMode.MixPerTile, 0, true, DefaultShapeJitter, DefaultShapePress),

            // 많이 누른 버전 - 무너진 폐허 느낌이 필요할 때의 상한 감각
            new TrimSheetVariation("Block_Worn_Heavy", UnitCube(), UnitCellSpan, TrimSheetCellMode.MixPerTile, 0, true, 0.04f, 0.07f),

            new TrimSheetVariation("Block_Mix_2x2x2", new Vector3(2f, 2f, 2f), UnitCellSpan, TrimSheetCellMode.MixPerTile, 0, true, DefaultShapeJitter, DefaultShapePress),
            new TrimSheetVariation("Slab_Mix_3x05x3", new Vector3(3f, 0.5f, 3f), UnitCellSpan, TrimSheetCellMode.MixPerTile, 0, true, DefaultShapeJitter, DefaultShapePress),
            new TrimSheetVariation("Wall_Mix_4x2x05", new Vector3(4f, 2f, 0.5f), UnitCellSpan, TrimSheetCellMode.MixPerTile, 0, true, DefaultShapeJitter, DefaultShapePress),
            new TrimSheetVariation("Pillar_Mix_1x4", new Vector3(1f, 4f, 1f), UnitCellSpan, TrimSheetCellMode.MixPerTile, 0, true, DefaultShapeJitter, DefaultShapePress),
        };

        static Vector3 UnitCube()
        {
            return new Vector3(UnitBlockSize, UnitBlockSize, UnitBlockSize);
        }

        /// <summary>
        /// 큐브 하나를 굽고 씬에 놓는다. 메시는 에셋으로 저장되고
        /// 같은 이름이 있으면 내용만 갈아끼운다.
        /// </summary>
        public static GameObject CreateCube(
            TrimSheetDefinition definition,
            TrimSheetCubeOptions options,
            string objectName,
            Vector3 position)
        {
            if (definition == null)
            {
                Debug.LogError("[TrimSheet] 규격 에셋이 없다. Setup Stone Atlas Assets을 먼저 실행한다.");
                return null;
            }

            if (!definition.IsReady())
            {
                Debug.LogError($"[TrimSheet] 규격이 불완전하다 (머티리얼 누락): {AssetDatabase.GetAssetPath(definition)}");
                return null;
            }

            Mesh built = TrimSheetCubeMesh.Build(definition, options);

            if (built == null)
                return null;

            Mesh saved = TrimSheetAssets.SaveMesh(built, objectName);

            if (saved == null)
                return null;

            GameObject cube = new GameObject(objectName);
            cube.transform.position = position;

            MeshFilter filter = cube.AddComponent<MeshFilter>();
            filter.sharedMesh = saved;

            MeshRenderer renderer = cube.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = definition.material;

            // 걸어 올라가며 확인할 수 있게. 정점 변위 때문에 실제 외곽이 지정 크기와
            // 조금 달라지므로 구운 메시의 바운즈를 그대로 쓴다 (근사 프록시로 충분하다).
            BoxCollider collider = cube.AddComponent<BoxCollider>();
            collider.center = saved.bounds.center;
            collider.size = saved.bounds.size;

            Undo.RegisterCreatedObjectUndo(cube, "Create Trim Sheet Cube");

            return cube;
        }

        /// <summary>
        /// 바리에이션 세트를 한 루트 아래에 X축으로 늘어놓는다.
        /// 기존 루트가 있으면 지우고 다시 만든다.
        /// </summary>
        public static GameObject BuildVariationSet(TrimSheetDefinition definition, Vector3 origin, int seed)
        {
            if (definition == null)
            {
                Debug.LogError("[TrimSheet] 규격 에셋이 없다. Setup Stone Atlas Assets을 먼저 실행한다.");
                return null;
            }

            ClearVariationSet();

            GameObject root = new GameObject(VariationRootName);
            root.transform.position = origin;
            Undo.RegisterCreatedObjectUndo(root, "Build Trim Sheet Variations");

            float cursorX = 0f;

            for (int i = 0; i < Variations.Length; i++)
            {
                TrimSheetVariation variation = Variations[i];

                TrimSheetCubeOptions options = new TrimSheetCubeOptions
                {
                    size = variation.size,
                    worldUnitsPerCell = variation.worldUnitsPerCell,
                    cellMode = variation.cellMode,
                    cellIndex = variation.cellIndex,
                    randomRotation = variation.randomRotation,
                    shapeJitter = variation.shapeJitter,
                    shapePress = variation.shapePress,

                    // 항목마다 다른 시드 - 혼합 패턴과 쉐입이 서로 겹쳐 보이지 않게
                    seed = seed + i * 977,
                };

                Vector3 size = TrimSheetCubeMesh.ClampSize(variation.size);

                cursorX += size.x * 0.5f;

                // 피벗이 중심이므로 바닥을 루트 높이에 맞추려면 절반만큼 올린다
                Vector3 localPosition = new Vector3(cursorX, size.y * 0.5f, 0f);

                GameObject cube = CreateCube(definition, options, variation.name, origin + localPosition);

                cursorX += size.x * 0.5f + VariationGap;

                if (cube == null)
                    continue;

                cube.transform.SetParent(root.transform, true);
            }

            return root;
        }

        public static GameObject FindVariationRoot()
        {
            return GameObject.Find(VariationRootName);
        }

        public static void ClearVariationSet()
        {
            GameObject existing = FindVariationRoot();

            if (existing == null)
                return;

            Undo.DestroyObjectImmediate(existing);
        }

        /// <summary>루트 아래 큐브 개수. 창의 상태 표시용.</summary>
        public static int CountVariationCubes()
        {
            GameObject root = FindVariationRoot();

            if (root == null)
                return 0;

            return root.GetComponentsInChildren<MeshFilter>(true).Length;
        }
    }
}
