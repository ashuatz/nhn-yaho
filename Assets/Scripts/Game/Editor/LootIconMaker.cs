using System.IO;
using UnityEditor;
using UnityEngine;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// 아이템 아이콘 스프라이트 생성 (웹 프로토타입 이식, ADR-0008). 이모지 금지 규약을
    /// 지키면서 종류가 구분되는 아이콘을 절차적 도형 텍스처로 만든다.
    /// id별 도형(사각/마름모/막대/물결/상자)을 그려 Assets/Textures/LootIcons/에 PNG로
    /// 저장하고 Sprite로 임포트해 재사용한다. 색은 tier 색을 입혀 등급감을 준다.
    /// 정식 아트 승격 전까지의 그레이박스 아이콘.
    /// </summary>
    public static class LootIconMaker
    {
        // Resources 하위에 둬 런타임 Resources.Load로 접근 (LootIcons 헬퍼)
        const string FolderParent = "Assets/Resources";
        const string Folder = "Assets/Resources/LootIcons";
        const int Size = 64;

        /// <summary>
        /// id에 맞는 아이콘 스프라이트를 돌려준다 (없으면 생성). 알 수 없는 id는
        /// 기본 사각 아이콘. 색은 tier 기반.
        /// </summary>
        public static Sprite Ensure(string id, int tier)
        {
            EnsureFolder();

            string path = $"{Folder}/{id}.png";
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (existing != null)
                return existing;

            Color color = Scavenger.Loot.LootDefinition.TierColor(tier);
            Texture2D texture = Draw(id, color);

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            ConfigureAsSprite(path);

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // id별 도형을 골라 그린다. 투명 배경 + 색 도형 + 어두운 외곽으로 또렷하게
        static Texture2D Draw(string id, Color color)
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            Clear(texture);

            if (id == "scrap_metal" || id == "gear")
            {
                DrawGear(texture, color);
            }
            else if (id == "crystal")
            {
                DrawDiamond(texture, color);
            }
            else if (id == "battery")
            {
                DrawBar(texture, color);
            }
            else if (id == "coil")
            {
                DrawWave(texture, color);
            }
            else if (id == "lockbox" || id == "chest")
            {
                DrawBox(texture, color);
            }
            else
            {
                // 폐지 등 기본 - 채운 사각
                DrawSquare(texture, color);
            }

            texture.Apply();
            return texture;
        }

        static void Clear(Texture2D texture)
        {
            Color clear = new Color(0f, 0f, 0f, 0f);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                    texture.SetPixel(x, y, clear);
            }
        }

        // -- 도형들 (중심 기준, 여백 8px) --------------------------------------

        static void DrawSquare(Texture2D texture, Color color)
        {
            FillRect(texture, 14, 14, 50, 50, color);
            Outline(texture, 14, 14, 50, 50);
        }

        static void DrawBox(Texture2D texture, Color color)
        {
            // 상자: 사각 + 상단 뚜껑선 + 자물쇠 점
            FillRect(texture, 12, 12, 52, 46, color);
            Outline(texture, 12, 12, 52, 46);

            Color dark = new Color(0f, 0f, 0f, 0.5f);

            for (int x = 12; x < 52; x++)
                texture.SetPixel(x, 40, dark);

            FillRect(texture, 30, 26, 34, 34, dark);
        }

        static void DrawBar(Texture2D texture, Color color)
        {
            // 배터리: 세로 막대 + 상단 단자
            FillRect(texture, 24, 12, 40, 50, color);
            Outline(texture, 24, 12, 40, 50);
            FillRect(texture, 28, 50, 36, 56, color);
        }

        static void DrawDiamond(Texture2D texture, Color color)
        {
            // 마름모 (중심 32,32, 반경 22)
            int cx = 32, cy = 32, r = 22;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (Mathf.Abs(x - cx) + Mathf.Abs(y - cy) <= r)
                        texture.SetPixel(x, y, color);
                }
            }
        }

        static void DrawGear(Texture2D texture, Color color)
        {
            // 기어: 원 + 8방향 이빨. 거리/각도 기반
            int cx = 32, cy = 32;
            float outer = 22f, inner = 9f;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float angle = Mathf.Atan2(dy, dx);
                    float tooth = Mathf.Cos(angle * 8f) * 3f;
                    float radius = outer + tooth;

                    if (dist <= radius && dist >= inner)
                        texture.SetPixel(x, y, color);
                }
            }
        }

        static void DrawWave(Texture2D texture, Color color)
        {
            // 코일: 사인 물결 밴드
            for (int x = 8; x < 56; x++)
            {
                float phase = (x - 8) / 48f * Mathf.PI * 3f;
                int centerY = 32 + Mathf.RoundToInt(Mathf.Sin(phase) * 14f);

                for (int t = -4; t <= 4; t++)
                {
                    int y = centerY + t;

                    if (y >= 0 && y < Size)
                        texture.SetPixel(x, y, color);
                }
            }
        }

        // -- 픽셀 유틸 --------------------------------------------------------

        static void FillRect(Texture2D texture, int x0, int y0, int x1, int y1, Color color)
        {
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    if (x >= 0 && x < Size && y >= 0 && y < Size)
                        texture.SetPixel(x, y, color);
                }
            }
        }

        static void Outline(Texture2D texture, int x0, int y0, int x1, int y1)
        {
            Color dark = new Color(0f, 0f, 0f, 0.6f);

            for (int x = x0; x < x1; x++)
            {
                texture.SetPixel(x, y0, dark);
                texture.SetPixel(x, y1 - 1, dark);
            }

            for (int y = y0; y < y1; y++)
            {
                texture.SetPixel(x0, y, dark);
                texture.SetPixel(x1 - 1, y, dark);
            }
        }

        static void ConfigureAsSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(FolderParent))
                AssetDatabase.CreateFolder("Assets", "Resources");

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder(FolderParent, "LootIcons");
        }

        /// <summary>모든 기본 아이템 아이콘을 미리 생성 (메뉴/셋업에서 1회 호출).</summary>
        [MenuItem("Scavenger/Generate Loot Icons")]
        public static void GenerateAll()
        {
            Ensure("scrap_paper", 1);
            Ensure("scrap_metal", 2);
            Ensure("lockbox", 3);

            // 웹 확장 대비 종류도 함께 생성 (도형 매핑 존재)
            Ensure("crystal", 2);
            Ensure("battery", 1);
            Ensure("coil", 1);
            Ensure("chest", 3);

            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("[LootIcon] 아이콘 생성 완료 (Assets/Resources/LootIcons)");
        }
    }
}
