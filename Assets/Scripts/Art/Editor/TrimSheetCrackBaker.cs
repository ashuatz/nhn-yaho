using System.IO;
using UnityEditor;
using UnityEngine;

namespace Scavenger.ArtTools
{
    /// <summary>
    /// 깨끗한 2x1 돌 아틀라스에서 크랙/치핑 변형을 더한 2x2 아틀라스를 굽는다.
    ///
    /// 아래 줄(셀 0, 1) = 원본 그대로, 위 줄(셀 2, 3) = 같은 타일에 크랙을 그린 것.
    /// 손상 타일을 별도 셀로 두면 혼합 가중치만으로 손상 빈도를 조절할 수 있다
    /// (셰이더나 두 번째 머티리얼이 필요 없다).
    ///
    /// 시드 고정 - 같은 시드면 같은 아틀라스가 나온다.
    /// </summary>
    public static class TrimSheetCrackBaker
    {
        public const string SourceAtlasPath = "Assets/Art/Tex/TrimSheet_Stone_01.png";
        public const string OutputAtlasPath = "Assets/Art/Tex/TrimSheet_Stone_02.png";

        public const int DefaultSeed = 20260725;

        /// <summary>셀 한 변 픽셀. 소스 아틀라스 셀 크기와 같아야 한다.</summary>
        const int CellSize = 256;
        const int SourceColumns = 2;

        /// <summary>크랙이 파고드는 최대 길이 (셀 크기 비율).</summary>
        const float MaxCrackLength = 0.75f;

        [MenuItem("Scavenger/Trim Sheet/Bake Cracked Atlas")]
        public static void BakeMenu()
        {
            Texture2D baked = Bake(DefaultSeed);

            if (baked == null)
                return;

            EditorGUIUtility.PingObject(baked);
            Debug.Log($"[TrimSheet] 크랙 아틀라스 생성: {OutputAtlasPath} ({baked.width}x{baked.height}, 셀 {SourceColumns}x2)");
        }

        /// <summary>
        /// 크랙 아틀라스를 굽고 임포트까지 마친 텍스처를 돌려준다.
        /// 이미 있으면 덮어쓴다 (같은 에셋을 갱신하므로 머티리얼 참조는 유지된다).
        /// </summary>
        public static Texture2D Bake(int seed)
        {
            Texture2D source = LoadSourceAtlas();

            if (source == null)
                return null;

            if (source.width != CellSize * SourceColumns || source.height != CellSize)
            {
                Debug.LogError(
                    $"[TrimSheet] 소스 아틀라스 크기가 {CellSize * SourceColumns}x{CellSize}이 아니다: "
                    + $"{source.width}x{source.height} ({SourceAtlasPath})");

                Object.DestroyImmediate(source);
                return null;
            }

            Color32[] sourcePixels = source.GetPixels32();
            Object.DestroyImmediate(source);

            int width = CellSize * SourceColumns;
            int height = CellSize * 2;
            Color32[] output = new Color32[width * height];

            // 아래 줄 = 원본 (Texture2D는 좌하단 원점이라 셀 인덱스 0,1과 그대로 맞는다)
            for (int i = 0; i < sourcePixels.Length; i++)
                output[i] = sourcePixels[i];

            // 위 줄 = 같은 타일 + 크랙
            for (int cellX = 0; cellX < SourceColumns; cellX++)
            {
                int originX = cellX * CellSize;
                int originY = CellSize;

                for (int y = 0; y < CellSize; y++)
                {
                    int sourceRow = y * width;
                    int destRow = (originY + y) * width;

                    for (int x = 0; x < CellSize; x++)
                        output[destRow + originX + x] = sourcePixels[sourceRow + originX + x];
                }

                // 셀마다 다른 시드 - 두 크랙 타일이 똑같이 갈라지면 반복감이 남는다
                System.Random rng = new System.Random(seed + cellX * 7919);

                DrawCracks(output, width, height, originX, originY, rng);
                DrawChips(output, width, height, originX, originY, rng);
            }

            return WriteAtlas(output, width, height);
        }

        /// <summary>
        /// 소스 PNG를 파일에서 직접 읽는다. 임포트된 텍스처는 isReadable이 꺼져 있어
        /// GetPixels32를 쓸 수 없다 (임포터를 건드리지 않고 우회한다).
        /// </summary>
        static Texture2D LoadSourceAtlas()
        {
            string fullPath = Path.Combine(
                Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
                SourceAtlasPath);

            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[TrimSheet] 소스 아틀라스가 없다: {SourceAtlasPath}");
                return null;
            }

            Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            source.hideFlags = HideFlags.HideAndDontSave;

            if (!source.LoadImage(File.ReadAllBytes(fullPath)))
            {
                Debug.LogError($"[TrimSheet] 소스 아틀라스를 디코드할 수 없다: {SourceAtlasPath}");
                Object.DestroyImmediate(source);

                return null;
            }

            return source;
        }

        // ---------------------------------------------------------------------
        // 손상 그리기
        // ---------------------------------------------------------------------

        /// <summary>
        /// 셀 하나에 크랙 몇 줄. 모르타르(셀 경계)에서 출발해 안쪽으로 파고든다 -
        /// 실제 석재도 이음선에서부터 갈라진다.
        /// </summary>
        static void DrawCracks(Color32[] pixels, int width, int height, int originX, int originY, System.Random rng)
        {
            int crackCount = 2 + rng.Next(2);

            for (int i = 0; i < crackCount; i++)
            {
                GetEdgeStart(rng, out float startX, out float startY, out float angle);

                float length = CellSize * Mathf.Lerp(0.3f, MaxCrackLength, (float)rng.NextDouble());

                WalkCrack(pixels, width, height, originX, originY, startX, startY, angle, length, rng, 1);
            }
        }

        /// <summary>셀 네 변 중 하나에서 시작점과 안쪽 방향을 고른다.</summary>
        static void GetEdgeStart(System.Random rng, out float startX, out float startY, out float angle)
        {
            // 모서리에 딱 붙이면 모르타르에 묻혀 안 보인다. 변의 가운데 60% 구간에서 시작.
            float along = CellSize * (0.2f + 0.6f * (float)rng.NextDouble());

            // 안쪽 방향에 +-40도 산포
            float spread = Mathf.Deg2Rad * 40f * ((float)rng.NextDouble() * 2f - 1f);

            int edge = rng.Next(4);

            if (edge == 0)
            {
                startX = along;
                startY = 0f;
                angle = Mathf.PI * 0.5f + spread;

                return;
            }

            if (edge == 1)
            {
                startX = along;
                startY = CellSize - 1f;
                angle = -Mathf.PI * 0.5f + spread;

                return;
            }

            if (edge == 2)
            {
                startX = 0f;
                startY = along;
                angle = spread;

                return;
            }

            startX = CellSize - 1f;
            startY = along;
            angle = Mathf.PI + spread;
        }

        /// <summary>
        /// 크랙 한 줄을 걸어가며 어둡게 만든다. 남은 분기 예산(<paramref name="branchBudget"/>)이
        /// 있으면 도중에 갈라진다.
        /// </summary>
        static void WalkCrack(
            Color32[] pixels,
            int width,
            int height,
            int originX,
            int originY,
            float x,
            float y,
            float angle,
            float length,
            System.Random rng,
            int branchBudget)
        {
            const float StepSize = 1.2f;

            int stepCount = Mathf.Max(1, Mathf.RoundToInt(length / StepSize));
            int branchAt = stepCount / 2 + rng.Next(Mathf.Max(1, stepCount / 4));

            for (int step = 0; step < stepCount; step++)
            {
                // 끝으로 갈수록 얇고 옅게 - 갈라짐이 자연히 잦아든다
                float remaining = 1f - step / (float)stepCount;
                float strength = Mathf.Lerp(0.45f, 1f, remaining);

                DarkenBrush(pixels, width, height, originX + x, originY + y, strength);

                if (branchBudget > 0 && step == branchAt)
                {
                    float branchAngle = angle + Mathf.Deg2Rad * (25f + 25f * (float)rng.NextDouble())
                                        * (rng.Next(2) == 0 ? 1f : -1f);

                    WalkCrack(pixels, width, height, originX, originY, x, y, branchAngle,
                        length * 0.45f, rng, branchBudget - 1);

                    branchBudget = 0;
                }

                // 진행 방향을 조금씩 틀어 직선이 되지 않게
                angle += Mathf.Deg2Rad * 12f * ((float)rng.NextDouble() * 2f - 1f);

                x += Mathf.Cos(angle) * StepSize;
                y += Mathf.Sin(angle) * StepSize;

                if (x < 0f || y < 0f || x >= CellSize || y >= CellSize)
                    return;
            }
        }

        /// <summary>
        /// 깨져 나간 자리(스폴). 원형으로 찍으면 구멍을 뚫은 것처럼 보이므로
        /// 방향별 반지름을 흔들어 각진 파편 모양을 만든다.
        ///
        /// 안쪽은 갓 드러난 면이라 주변보다 밝고, 테두리에만 얕은 그늘이 진다.
        /// 통째로 어둡게 하면 돌이 아니라 구멍이 된다.
        /// </summary>
        static void DrawChips(Color32[] pixels, int width, int height, int originX, int originY, System.Random rng)
        {
            const int SectorCount = 8;

            int chipCount = 1 + rng.Next(3);

            for (int i = 0; i < chipCount; i++)
            {
                float centerX = CellSize * (0.15f + 0.7f * (float)rng.NextDouble());
                float centerY = CellSize * (0.15f + 0.7f * (float)rng.NextDouble());
                float baseRadius = CellSize * (0.015f + 0.03f * (float)rng.NextDouble());

                // 방향별 반지름 - 이웃 섹터를 보간해 울퉁불퉁한 윤곽이 된다
                float[] sectorRadius = new float[SectorCount];

                for (int s = 0; s < SectorCount; s++)
                    sectorRadius[s] = baseRadius * (0.55f + 0.65f * (float)rng.NextDouble());

                int span = Mathf.CeilToInt(baseRadius * 1.2f) + 1;

                for (int dy = -span; dy <= span; dy++)
                {
                    for (int dx = -span; dx <= span; dx++)
                    {
                        float distance = Mathf.Sqrt(dx * dx + dy * dy);

                        if (distance < 0.001f)
                            distance = 0.001f;

                        float radius = SampleSectorRadius(sectorRadius, Mathf.Atan2(dy, dx));

                        if (distance > radius)
                            continue;

                        float edgeRatio = distance / radius;

                        int px = Mathf.RoundToInt(originX + centerX + dx);
                        int py = Mathf.RoundToInt(originY + centerY + dy);

                        // 테두리 25% 구간만 그늘, 나머지는 밝게 드러난 면
                        if (edgeRatio > 0.75f)
                        {
                            DarkenPixel(pixels, width, height, px, py, 0.35f);
                            continue;
                        }

                        BrightenPixel(pixels, width, height, px, py, 0.3f * (1f - edgeRatio));
                    }
                }
            }
        }

        /// <summary>방향(라디안)에 해당하는 반지름. 섹터 사이는 선형 보간.</summary>
        static float SampleSectorRadius(float[] sectorRadius, float angle)
        {
            int count = sectorRadius.Length;

            float normalized = (angle + Mathf.PI * 2f) % (Mathf.PI * 2f) / (Mathf.PI * 2f);
            float scaled = normalized * count;

            int index = Mathf.FloorToInt(scaled) % count;
            int next = (index + 1) % count;

            return Mathf.Lerp(sectorRadius[index], sectorRadius[next], scaled - Mathf.Floor(scaled));
        }

        /// <summary>
        /// 중심 1px + 상하좌우 절반 세기로 어둡게. 1px 선만 그으면
        /// 밉맵에서 먼저 사라져 크랙이 거리에 따라 없어진다.
        /// </summary>
        static void DarkenBrush(Color32[] pixels, int width, int height, float x, float y, float strength)
        {
            int px = Mathf.RoundToInt(x);
            int py = Mathf.RoundToInt(y);

            DarkenPixel(pixels, width, height, px, py, strength);

            DarkenPixel(pixels, width, height, px + 1, py, strength * 0.4f);
            DarkenPixel(pixels, width, height, px - 1, py, strength * 0.4f);
            DarkenPixel(pixels, width, height, px, py + 1, strength * 0.4f);
            DarkenPixel(pixels, width, height, px, py - 1, strength * 0.4f);
        }

        static void DarkenPixel(Color32[] pixels, int width, int height, int x, int y, float strength)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            int index = y * width + x;
            Color32 current = pixels[index];

            // 원본 돌이 이미 어두워서(휘도 80 남짓) 세게 누르면 잉크로 그은 선처럼 뜬다.
            // 0.55까지만 눌러 원본에 있던 옅은 균열과 같은 톤을 유지한다.
            float multiplier = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(strength));

            pixels[index] = new Color32(
                (byte)Mathf.RoundToInt(current.r * multiplier),
                (byte)Mathf.RoundToInt(current.g * multiplier),
                (byte)Mathf.RoundToInt(current.b * multiplier),
                current.a);
        }

        static void BrightenPixel(Color32[] pixels, int width, int height, int x, int y, float strength)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            int index = y * width + x;
            Color32 current = pixels[index];

            float multiplier = 1f + Mathf.Clamp01(strength);

            pixels[index] = new Color32(
                (byte)Mathf.Min(255, Mathf.RoundToInt(current.r * multiplier)),
                (byte)Mathf.Min(255, Mathf.RoundToInt(current.g * multiplier)),
                (byte)Mathf.Min(255, Mathf.RoundToInt(current.b * multiplier)),
                current.a);
        }

        // ---------------------------------------------------------------------
        // 출력
        // ---------------------------------------------------------------------

        static Texture2D WriteAtlas(Color32[] pixels, int width, int height)
        {
            Texture2D atlas = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            atlas.hideFlags = HideFlags.HideAndDontSave;
            atlas.SetPixels32(pixels);
            atlas.Apply(false);

            byte[] png = atlas.EncodeToPNG();
            Object.DestroyImmediate(atlas);

            string fullPath = Path.Combine(
                Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
                OutputAtlasPath);

            File.WriteAllBytes(fullPath, png);

            AssetDatabase.ImportAsset(OutputAtlasPath, ImportAssetOptions.ForceUpdate);

            return AssetDatabase.LoadAssetAtPath<Texture2D>(OutputAtlasPath);
        }
    }
}
