using UnityEditor;
using UnityEngine;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// 그레이박스 머티리얼 에셋 관리. new Material(...)을 프리팹/씬에 그대로 물리면
    /// 디스크 에셋이 아니라서 리로드 후 참조가 깨져 마젠타가 된다 (실제 발생).
    /// 색상별 머티리얼을 Assets/Materials/Greybox/에 에셋으로 만들어 재사용한다.
    /// </summary>
    public static class GreyboxMaterials
    {
        const string FolderParent = "Assets/Materials";
        const string Folder = "Assets/Materials/Greybox";

        public static Material Ensure(string materialName, Color color)
        {
            return Ensure(materialName, color, subfolder: null);
        }

        /// <summary>
        /// 이름으로 머티리얼 에셋을 보장한다. 이름은 **어디에 쓰이는지**를 담아야 한다
        /// (사용자 지시 2026-07-26: 색상 해시 이름 금지) - 예: FarmingPoint_Top_Platform.
        /// 색만 다른 같은 용도는 같은 에셋을 재사용하고, 색이 바뀌면 갱신한다.
        /// </summary>
        /// <param name="subfolder">Assets/Materials/Greybox 아래 하위 폴더 (없으면 루트).</param>
        public static Material Ensure(string materialName, Color color, string subfolder)
        {
            string folder = ResolveFolder(subfolder);
            string path = $"{folder}/{materialName}.mat";

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(ResolveLitShader());
                material.color = color;

                AssetDatabase.CreateAsset(material, path);
                return material;
            }

            // 템플릿 색이 바뀌면 따라간다 - 이름이 용도라서 색은 그 용도의 현재 값이다
            if (material.color != color)
            {
                material.color = color;
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        /// <summary>
        /// 새로 만드는 그레이박스 머티리얼의 셰이더. 프로젝트에 LUT 포그용 파생 셰이더
        /// (Scavenger/Simple Lit)가 있으면 그것을 쓴다 - 기준 머티리얼(Common.mat)이
        /// 이미 그 셰이더라, URP Lit으로 만들면 프리팹을 다시 구울 때마다 포그가 걸리지 않는
        /// 머티리얼이 새로 생겨 손으로 되돌려야 한다.
        /// </summary>
        static Shader ResolveLitShader()
        {
            Shader projectShader = Shader.Find(ProjectLitShaderName);

            if (projectShader != null)
                return projectShader;

            return Shader.Find("Universal Render Pipeline/Lit");
        }

        const string ProjectLitShaderName = "Scavenger/Simple Lit";

        static string ResolveFolder(string subfolder)
        {
            EnsureFolder();

            if (string.IsNullOrEmpty(subfolder))
                return Folder;

            string path = $"{Folder}/{subfolder}";

            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(Folder, subfolder);

            return path;
        }

        /// <summary>
        /// 바닥 그리드 머티리얼 (웹 이식). 격자선 텍스처를 Base Map으로 얹은 URP Lit.
        /// 셀 경계에 라인이 있어 큐브 윗면에 타일링하면 그리드가 보인다. 색은 베이스톤.
        /// 텍스처/머티리얼은 에셋으로 1회 생성해 재사용 (리로드 후 참조 안정).
        /// </summary>
        public static Material EnsureFloorGrid()
        {
            EnsureResourcesFolder();

            // Resources 하위 - 런타임 Resources.Load로 접근 (FloorGridMaterial 헬퍼)
            const string path = "Assets/Resources/FloorGrid.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
                return existing;

            Texture2D gridTexture = EnsureGridTexture();

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            Material material = new Material(litShader);
            material.color = new Color(0.32f, 0.36f, 0.3f);
            material.SetTexture("_BaseMap", gridTexture);
            material.mainTexture = gridTexture;

            // 매끈한 반사 억제 - 바닥은 무광 (그리드 선이 또렷하게)
            material.SetFloat("_Smoothness", 0.1f);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // 1셀 = 경계에 선이 있는 정사각 텍스처. 반복(Repeat)으로 무한 격자.
        // 채널: 셀 내부는 밝게(1), 경계 라인은 어둡게(0.55) - 곱연산으로 색을 눌러 라인 표현
        static Texture2D EnsureGridTexture()
        {
            EnsureResourcesFolder();

            const string path = "Assets/Resources/GridCell.png";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (existing != null)
                return existing;

            const int size = 64;
            const int line = 3;

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool onLine = x < line || y < line || x >= size - line || y >= size - line;

                    // 경계선은 어둡게, 내부는 밝게 (베이스 색에 곱해져 그리드가 드러난다)
                    float shade = onLine ? 0.5f : 1f;

                    texture.SetPixel(x, y, new Color(shade, shade, shade, 1f));
                }
            }

            texture.Apply();

            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(FolderParent))
                AssetDatabase.CreateFolder("Assets", "Materials");

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder(FolderParent, "Greybox");
        }

        static void EnsureResourcesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
        }

        /// <summary>바닥 그리드 머티리얼/텍스처를 미리 생성 (메뉴).</summary>
        [MenuItem("Scavenger/Generate Floor Grid")]
        public static void GenerateFloorGridMenu()
        {
            EnsureFloorGrid();
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("[Grid] 바닥 그리드 머티리얼 생성 완료 (Assets/Resources)");
        }

        /// <summary>
        /// 전진 방향 화살표 머티리얼 (웹 이식). 청록 삼각형 텍스처 + emissive(Bloom).
        /// 바닥에 눕힌 quad가 전진(+z)을 가리키도록 스포너가 배치. Resources 하위.
        /// </summary>
        public static Material EnsureArrowMaterial()
        {
            EnsureResourcesFolder();

            const string path = "Assets/Resources/FloorArrow.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
                return existing;

            Texture2D arrowTexture = EnsureArrowTexture();

            // Unlit + 반투명 - 바닥 데칼처럼 은은하게. emissive는 Unlit 색이 곧 밝기
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            Material material = new Material(shader);
            material.SetTexture("_BaseMap", arrowTexture);
            material.mainTexture = arrowTexture;
            material.color = new Color(0.35f, 0.78f, 1f, 0.5f);

            // 알파 블렌드 설정 (URP Unlit Transparent)
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // 위(+y 텍스처)를 가리키는 삼각형. 투명 배경.
        static Texture2D EnsureArrowTexture()
        {
            EnsureResourcesFolder();

            const string path = "Assets/Resources/ArrowShape.png";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (existing != null)
                return existing;

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color fill = Color.white;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 위로 갈수록 좁아지는 삼각형 (꼭짓점 위)
                    float t = y / (float)size;
                    float halfWidth = Mathf.Lerp(size * 0.42f, 0f, t);
                    float centerX = size * 0.5f;

                    bool inside = Mathf.Abs(x - centerX) <= halfWidth && y > size * 0.15f;

                    texture.SetPixel(x, y, inside ? fill : clear);
                }
            }

            texture.Apply();

            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
