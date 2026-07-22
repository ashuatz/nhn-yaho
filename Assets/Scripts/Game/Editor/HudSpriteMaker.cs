using System.IO;
using UnityEditor;
using UnityEngine;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// HUD 패널 스프라이트 생성 (웹 프로토타입 CSS 이식, ADR-0008).
    /// 웹 .panel: rgba(11,15,20,0.86) 배경 + 둥근 모서리(9px) + 1px 테두리.
    /// 9-slice 스프라이트로 만들어 어떤 크기 패널에도 모서리/테두리가 유지된다.
    /// 기본(청회색 테두리)과 골드 두 종. Resources 하위 - 런타임 로드(HudSprites).
    /// </summary>
    public static class HudSpriteMaker
    {
        const int Size = 32;
        const int Radius = 9;
        const int Border = 2;

        // 웹 CSS 색
        static readonly Color PanelFill = new Color(11f / 255f, 15f / 255f, 20f / 255f, 0.86f);
        static readonly Color EdgeColor = new Color(150f / 255f, 170f / 255f, 190f / 255f, 0.22f);
        static readonly Color GoldEdge = new Color(217f / 255f, 169f / 255f, 79f / 255f, 0.55f);

        [MenuItem("Scavenger/Generate HUD Sprites")]
        public static void GenerateAll()
        {
            EnsureResourcesFolder();

            EnsurePanel("PanelSprite", EdgeColor);
            EnsurePanel("PanelSpriteGold", GoldEdge);

            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("[HudSprite] 패널 스프라이트 생성 완료 (Assets/Resources/HudSprites)");
        }

        static Sprite EnsurePanel(string name, Color edge)
        {
            string path = $"Assets/Resources/HudSprites/{name}.png";
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (existing != null)
                return existing;

            Texture2D texture = DrawRoundedPanel(edge);

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            ConfigureNineSlice(path);

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // 둥근 모서리 사각형: 내부 채움 + 테두리. 모서리 반경 밖은 투명.
        static Texture2D DrawRoundedPanel(Color edge)
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            Color clear = new Color(0f, 0f, 0f, 0f);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dist = CornerDistance(x, y);

                    // 모서리 반경 밖 = 투명
                    if (dist > Radius)
                    {
                        texture.SetPixel(x, y, clear);
                        continue;
                    }

                    // 테두리 밴드: 가장자리(사각/모서리)에서 Border 픽셀 이내
                    bool onEdge = IsOnEdge(x, y, dist);

                    texture.SetPixel(x, y, onEdge ? edge : PanelFill);
                }
            }

            texture.Apply();
            return texture;
        }

        // 가장 가까운 모서리 중심으로부터의 거리 (둥근 모서리 판정용).
        // 모서리 영역이 아니면 0 (내부 취급)
        static float CornerDistance(int x, int y)
        {
            int cx = x < Radius ? Radius : (x >= Size - Radius ? Size - Radius - 1 : x);
            int cy = y < Radius ? Radius : (y >= Size - Radius ? Size - Radius - 1 : y);

            bool inCornerX = x < Radius || x >= Size - Radius;
            bool inCornerY = y < Radius || y >= Size - Radius;

            if (!inCornerX || !inCornerY)
                return 0f;

            float dx = x - cx;
            float dy = y - cy;

            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static bool IsOnEdge(int x, int y, float cornerDist)
        {
            // 직선 가장자리 테두리
            bool straightEdge =
                x < Border || y < Border || x >= Size - Border || y >= Size - Border;

            // 둥근 모서리 테두리 (반경 - Border ~ 반경 사이)
            bool cornerEdge = cornerDist > Radius - Border && cornerDist <= Radius;

            return straightEdge || cornerEdge;
        }

        static void ConfigureNineSlice(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;

            // 9-slice border = 모서리 반경 (가운데는 늘어나고 모서리는 고정)
            importer.spriteBorder = new Vector4(Radius, Radius, Radius, Radius);

            importer.SaveAndReimport();
        }

        static void EnsureResourcesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            if (!AssetDatabase.IsValidFolder("Assets/Resources/HudSprites"))
                AssetDatabase.CreateFolder("Assets/Resources", "HudSprites");
        }
    }
}
