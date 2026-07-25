using System.IO;
using UnityEditor;
using UnityEngine;

namespace Scavenger.ArtTools
{
    /// <summary>
    /// 트림시트 에셋(텍스처 임포트 설정 / 머티리얼 / 규격 에셋 / 구운 메시) 관리.
    ///
    /// 머티리얼은 새로 만들지 않고 Assets/Materials/Greybox/Common.mat을 복사한다.
    /// 그레이박스 공통 룩(스무스니스, 메탈릭, 렌더 상태)을 그대로 물려받아야 하고,
    /// 셰이더를 Shader.Find로 다시 찾으면 세팅이 어긋난다.
    /// </summary>
    public static class TrimSheetAssets
    {
        /// <summary>실사용 아틀라스. 깨끗한 셀 2장 + 크랙 셀 2장 (2x2).</summary>
        public const string AtlasPath = TrimSheetCrackBaker.OutputAtlasPath;

        public const string BaseMaterialPath = "Assets/Materials/Greybox/Common.mat";
        public const string MaterialPath = "Assets/Materials/Greybox/TrimSheet_Stone_01.mat";
        public const string DefinitionPath = "Assets/Art/TrimSheets/TrimSheet_Stone_01.asset";
        public const string MeshFolder = "Assets/Art/Meshes/TrimSheet";

        /// <summary>팔레트 틴트 머티리얼 폴더 (배경 블록용).</summary>
        public const string PaletteMaterialFolder = "Assets/Materials/Greybox/TrimSheetEnv";

        /// <summary>깨끗한 셀 대비 크랙 셀이 뽑힐 상대 가중치. 손상은 드문드문.</summary>
        const float CrackedCellWeight = 0.3f;

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// 돌 아틀라스용 규격 에셋을 보장한다. 이미 있으면 사용자가 만진 값
        /// (셀 크기 등)은 건드리지 않고 끊어진 참조만 메운다.
        /// </summary>
        [MenuItem("Scavenger/Trim Sheet/Setup Stone Atlas Assets")]
        public static TrimSheetDefinition EnsureStoneDefinition()
        {
            Texture2D atlas = EnsureAtlas();

            if (atlas == null)
                return null;

            Material material = EnsureMaterial(atlas);

            if (material == null)
                return null;

            TrimSheetDefinition definition = AssetDatabase.LoadAssetAtPath<TrimSheetDefinition>(DefinitionPath);

            if (definition == null)
            {
                definition = CreateStoneDefinition(atlas, material);
                AssetDatabase.SaveAssets();

                return definition;
            }

            // 기존 에셋은 참조만 복구 - 조정한 셀 크기/격자를 덮어쓰지 않는다
            bool changed = false;

            if (definition.atlas != atlas)
            {
                definition.atlas = atlas;
                changed = true;
            }

            if (definition.material != material)
            {
                definition.material = material;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
            }

            return definition;
        }

        static TrimSheetDefinition CreateStoneDefinition(Texture2D atlas, Material material)
        {
            EnsureFolder(Path.GetDirectoryName(DefinitionPath).Replace('\\', '/'));

            TrimSheetDefinition definition = ScriptableObject.CreateInstance<TrimSheetDefinition>();
            definition.atlas = atlas;
            definition.material = material;

            // TrimSheet_Stone_02.png = 256x256 셀 4장 (아래 줄 깨끗, 위 줄 크랙)
            definition.columns = 2;
            definition.rows = 2;

            // 셀 0.5m -> 1m 큐브의 각 면이 2x2 타일
            definition.worldUnitsPerCell = 0.5f;
            definition.insetHalfTexel = true;
            definition.cellWeights = new[] { 1f, 1f, CrackedCellWeight, CrackedCellWeight };

            AssetDatabase.CreateAsset(definition, DefinitionPath);

            return definition;
        }

        /// <summary>
        /// 실사용 아틀라스를 보장한다. 없으면 깨끗한 소스에서 크랙 아틀라스를 굽는다.
        /// </summary>
        static Texture2D EnsureAtlas()
        {
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);

            if (existing == null)
                existing = TrimSheetCrackBaker.Bake(TrimSheetCrackBaker.DefaultSeed);

            if (existing == null)
            {
                Debug.LogError($"[TrimSheet] 아틀라스를 만들 수 없다: {AtlasPath}");
                return null;
            }

            return EnsureAtlasImport();
        }

        /// <summary>
        /// 아틀라스 임포트 설정을 트림시트에 맞춘다.
        /// 특히 npotScale은 None이어야 한다 - Unity가 POT로 리스케일하면
        /// 셀 경계가 텍셀 격자에서 밀려 UV 0.5 분할이 셀 중앙을 자른다.
        /// </summary>
        static Texture2D EnsureAtlasImport()
        {
            TextureImporter importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;

            if (importer == null)
                return AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);

            bool changed = false;

            if (importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                changed = true;
            }

            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                changed = true;
            }

            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                changed = true;
            }

            if (importer.filterMode != FilterMode.Bilinear)
            {
                importer.filterMode = FilterMode.Bilinear;
                changed = true;
            }

            if (!importer.mipmapEnabled)
            {
                importer.mipmapEnabled = true;
                changed = true;
            }

            // 스치는 각도에서 모르타르 선이 뭉개지지 않게
            if (importer.anisoLevel < 4)
            {
                importer.anisoLevel = 4;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        }

        static Material EnsureMaterial(Texture2D atlas)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            if (material == null)
            {
                if (!AssetDatabase.CopyAsset(BaseMaterialPath, MaterialPath))
                {
                    Debug.LogError($"[TrimSheet] 기준 머티리얼 복사 실패: {BaseMaterialPath} -> {MaterialPath}");
                    return null;
                }

                AssetDatabase.ImportAsset(MaterialPath);
                material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            }

            if (material == null)
                return null;

            bool changed = false;

            if (material.GetTexture(BaseMapId) != atlas)
            {
                material.SetTexture(BaseMapId, atlas);
                changed = true;
            }

            // UV를 메시에 구웠으므로 머티리얼 타일링은 반드시 1:1이어야 한다
            if (material.GetTextureScale(BaseMapId) != Vector2.one)
            {
                material.SetTextureScale(BaseMapId, Vector2.one);
                changed = true;
            }

            if (material.GetTextureOffset(BaseMapId) != Vector2.zero)
            {
                material.SetTextureOffset(BaseMapId, Vector2.zero);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
            }

            return material;
        }

        /// <summary>
        /// 메시를 에셋으로 저장한다. 같은 경로가 이미 있으면 내용만 갈아끼운다 -
        /// 새 에셋으로 만들면 씬/프리팹이 물고 있던 참조가 끊긴다.
        /// </summary>
        public static Mesh SaveMesh(Mesh mesh, string meshName)
        {
            return SaveMesh(mesh, meshName, MeshFolder);
        }

        /// <summary>
        /// 팔레트 색을 물린 트림시트 머티리얼. 배경 블록은 깊이 구분을 팔레트 색으로 하므로
        /// 트림시트 텍스처를 쓰면서도 색 단계를 유지해야 한다
        /// (단색 머티리얼 하나로 통일하면 근경과 원경이 붙어 보인다).
        /// </summary>
        public static Material EnsurePaletteMaterial(Material baseMaterial, int paletteIndex, Color color)
        {
            return EnsureTintedMaterial(
                baseMaterial, PaletteMaterialFolder, $"TrimSheetEnv_{paletteIndex:00}", color);
        }

        /// <summary>
        /// 트림시트 머티리얼의 색만 다른 복사본. 기준 머티리얼을 <b>복사</b>하는 이유는
        /// 아틀라스/타일링/렌더 상태를 그대로 물려받아야 하기 때문이다
        /// (Shader.Find로 새로 만들면 그레이박스 공통 룩이 어긋난다).
        /// </summary>
        public static Material EnsureTintedMaterial(
            Material baseMaterial, string folder, string materialName, Color color)
        {
            if (baseMaterial == null)
                return null;

            EnsureFolder(folder);

            string path = $"{folder}/{materialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                string basePath = AssetDatabase.GetAssetPath(baseMaterial);

                if (!AssetDatabase.CopyAsset(basePath, path))
                {
                    Debug.LogError($"[TrimSheet] 틴트 머티리얼 복사 실패: {basePath} -> {path}");
                    return null;
                }

                AssetDatabase.ImportAsset(path);
                material = AssetDatabase.LoadAssetAtPath<Material>(path);
            }

            if (material == null)
                return null;

            if (material.GetColor(BaseColorId) != color)
            {
                material.SetColor(BaseColorId, color);
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        public static Mesh SaveMesh(Mesh mesh, string meshName, string folder)
        {
            if (mesh == null)
                return null;

            EnsureFolder(folder);

            string path = $"{folder}/{meshName}.asset";
            Object occupant = AssetDatabase.LoadMainAssetAtPath(path);

            // 메시가 아닌 에셋이 그 경로에 있으면 CreateAsset이 통째로 날려버린다.
            // 이름 하나 때문에 남의 에셋을 잃지 않도록 거부한다.
            if (occupant != null && !(occupant is Mesh))
            {
                Debug.LogError(
                    $"[TrimSheet] 같은 경로에 메시가 아닌 에셋이 있다: {path} ({occupant.GetType().Name}). "
                    + "다른 이름을 쓴다.");

                Object.DestroyImmediate(mesh);

                return null;
            }

            Mesh existing = occupant as Mesh;

            if (existing == null)
            {
                mesh.name = meshName;
                AssetDatabase.CreateAsset(mesh, path);

                return mesh;
            }

            // 기존 메시를 쓰는 씬/프리팹이 있으면 그 지오메트리가 여기서 바뀐다.
            // 되돌릴 수 있게 에셋 상태를 먼저 Undo에 등록한다.
            Undo.RegisterCompleteObjectUndo(existing, "Rebake Trim Sheet Mesh");

            EditorUtility.CopySerialized(mesh, existing);
            existing.name = meshName;
            Object.DestroyImmediate(mesh);

            EditorUtility.SetDirty(existing);

            return existing;
        }

        /// <summary>중간 폴더까지 만든다. AssetDatabase.CreateFolder는 부모가 있어야 한다.</summary>
        public static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return;

            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folderPath);

            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
                return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
