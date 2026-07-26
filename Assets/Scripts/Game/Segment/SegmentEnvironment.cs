using System.Collections.Generic;
using Scavenger.Field;
using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 배경 인스턴스 블록: 구간 로컬 행렬 + 팔레트 인덱스.
    /// 렌더링은 EnvironmentRenderer(RenderMeshInstanced), 수동 편집 경로는
    /// GameObject 백엔드가 소비한다 (ADR-0005).
    /// </summary>
    public struct EnvironmentBlock
    {
        public Matrix4x4 LocalMatrix;
        public int PaletteIndex;

        /// <summary>출렁임 강도 0..1. 0이면 정적 블록 (렌더러가 행렬 갱신을 생략).</summary>
        public float Wave;

        /// <summary>출렁임 위상 (라디안). 블록마다 달라 물결처럼 보인다.</summary>
        public float Phase;
    }

    /// <summary>
    /// 카메라 시야 라인 클리어런스. 카메라-플레이어 시선 밴드와 겹치는 블록은
    /// 생성 자체를 거부한다 (카메라측 가림 방지 - 사용자 지시).
    /// (x, y) 평면 2D 검사 - 리그가 z로 슬라이드하므로 전 z에 동일 적용.
    /// </summary>
    public struct SightClearance
    {
        public bool Enabled;

        /// <summary>시선 먼 끝점 (복도 반대편 바닥). x, y.</summary>
        public Vector2 FarPoint;

        /// <summary>시선 가까운 끝점 (카메라 위치). x, y.</summary>
        public Vector2 NearPoint;

        public float Margin;

        public bool Rejects(Vector3 position, Vector3 scale)
        {
            if (!Enabled)
                return false;

            // 카메라 반대편에 완전히 있는 블록만 검사 제외 - 중심만 반대편이고
            // 폭이 시야 밴드를 가로지르는 블록은 통과시키면 안 된다 (Codex 교차 검토).
            // (반대편까지 전부 검사하면 낮은 지형 요소가 부당하게 거부됨 - 기존 검증 반영)
            float halfX = scale.x * 0.5f;

            if (NearPoint.x > 0f && position.x + halfX < 0f)
                return false;

            if (NearPoint.x < 0f && position.x - halfX > 0f)
                return false;

            float blockMinX = position.x - scale.x * 0.5f;
            float blockMaxX = position.x + scale.x * 0.5f;

            float lineMinX = Mathf.Min(FarPoint.x, NearPoint.x);
            float lineMaxX = Mathf.Max(FarPoint.x, NearPoint.x);

            float overlapMin = Mathf.Max(blockMinX, lineMinX);
            float overlapMax = Mathf.Min(blockMaxX, lineMaxX);

            if (overlapMin > overlapMax)
                return false;

            float yA = LineY(overlapMin);
            float yB = LineY(overlapMax);
            float lineMinY = Mathf.Min(yA, yB) - Margin;
            float lineMaxY = Mathf.Max(yA, yB) + Margin;

            float blockMinY = position.y - scale.y * 0.5f;
            float blockMaxY = position.y + scale.y * 0.5f;

            return blockMaxY >= lineMinY && blockMinY <= lineMaxY;
        }

        float LineY(float x)
        {
            float deltaX = NearPoint.x - FarPoint.x;

            if (Mathf.Abs(deltaX) < 0.0001f)
                return FarPoint.y;

            float t = (x - FarPoint.x) / deltaX;
            return Mathf.Lerp(FarPoint.y, NearPoint.y, t);
        }
    }

    /// <summary>
    /// 구간 그레이박스 환경 PCG 데이터 생성기 (ADR-0003/0004/0005 - 공간감 검증용).
    /// 평탄한 보행로 양옆으로 복셀풍 럽블 매스 + 상부층(복층) + 데브리.
    /// 보행 영역은 항상 y=0 평면 유지. 난수는 주입된 rng(런 시드) 사용.
    /// 출력은 인스턴스 블록 데이터 - GameObject를 만들지 않는다 (웹 호환 인스턴싱).
    /// 보행로 바닥만 GameObject (콜라이더 필요).
    /// </summary>
    public static class SegmentEnvironment
    {
        /// <summary>
        /// 블록 색 **기준색** (색상 + 채도). 인덱스가 EnvironmentBlock.PaletteIndex와 대응.
        /// 실제 밝기는 GreyboxTheme(Assets/Settings/GreyboxTheme.asset)이 정한다 -
        /// 여기 값을 직접 올리면 재생성 때마다 조절값을 덮어쓴다.
        /// 레이어 간 밝기 순서(근경 > 중경 > 원경)는 배수를 걸어도 유지된다 - 깊이 구분의 근거다.
        /// </summary>
        public static readonly Color[] Palette =
        {
            new Color(0.16f, 0.18f, 0.22f),   // 0: 럽블 어두움
            new Color(0.19f, 0.21f, 0.25f),   // 1: 럽블 중간
            new Color(0.22f, 0.24f, 0.28f),   // 2: 럽블 밝음
            new Color(0.2f, 0.22f, 0.27f),    // 3: 상부층 A
            new Color(0.24f, 0.26f, 0.31f),   // 4: 상부층 B
            new Color(0.29f, 0.29f, 0.32f),   // 5: 데브리
            new Color(0.11f, 0.13f, 0.17f),   // 6: 중경 매스
            new Color(0.07f, 0.09f, 0.13f),   // 7: 원경 스카이라인
        };

        /// <summary>
        /// 팔레트 인덱스의 역할 이름. 생성되는 머티리얼 에셋 이름에 들어간다
        /// (사용자 지시 2026-07-26: 색상 해시 이름 대신 어디에 쓰이는지 드러나는 이름).
        /// </summary>
        public static readonly string[] PaletteNames =
        {
            "RubbleDark",
            "RubbleMid",
            "RubbleLight",
            "UpperStoryA",
            "UpperStoryB",
            "Debris",
            "Midground",
            "Skyline",
        };

        /// <summary>이 인덱스부터는 원거리 레이어 - 렌더러가 그림자를 끈다.</summary>
        public const int FarPaletteStart = 6;

        /// <summary>팔레트 인덱스 -> 역할 이름 (범위를 벗어나면 인덱스 표기).</summary>
        public static string PaletteName(int paletteIndex)
        {
            if (paletteIndex < 0 || paletteIndex >= PaletteNames.Length)
                return $"Palette{paletteIndex}";

            return PaletteNames[paletteIndex];
        }

        const float RubbleSliceDepth = 2.2f;

        // 레이어별 출렁임 강도 (brg-shooter 셀 웨이브 차용)
        const float RubbleWave = 0.35f;
        const float MidWave = 0.6f;
        const float FarWave = 1f;

        // -- 데이터 생성 ------------------------------------------------------

        public static List<EnvironmentBlock> GenerateBlocks(ZoneDefinition definition, System.Random rng)
        {
            return GenerateBlocks(definition, rng, default);
        }

        public static List<EnvironmentBlock> GenerateBlocks(
            ZoneDefinition definition, System.Random rng, SightClearance clearance)
        {
            return GenerateBlocks(definition, rng, clearance, definition.lengthMeters);
        }

        /// <summary>
        /// 세그먼트 길이를 직접 지정하는 경로. 존과 존 사이 구간처럼 존보다 짧은
        /// 세그먼트는 배경도 그 길이만큼만 만들어야 다음 세그먼트 배경과 겹치지 않는다.
        /// </summary>
        public static List<EnvironmentBlock> GenerateBlocks(
            ZoneDefinition definition, System.Random rng, SightClearance clearance, float segmentLength)
        {
            List<EnvironmentBlock> blocks = new List<EnvironmentBlock>(384);

            AddRubbleSides(blocks, definition, rng, clearance, segmentLength);
            AddUpperStory(blocks, definition, rng, clearance, segmentLength);
            AddDebris(blocks, definition, rng, clearance, segmentLength);
            AddMidground(blocks, definition, rng, clearance, segmentLength);
            AddFarground(blocks, definition, rng, clearance, segmentLength);

            return blocks;
        }

        /// <summary>
        /// 카메라가 있는 쪽 (+1 = +x, -1 = -x). 카메라측은 지형이 발판을 가리면 안 되므로
        /// 위로 쌓지 않고 단차로 내려간다 (사용자 지시). 클리어런스가 비활성이면
        /// 아이소 리그 기본값(+x: yaw -45 카메라 오프셋 x가 양수, ADR-0008) 가정.
        /// 활성이면 SightClearance.NearPoint(FollowCamera.CameraOffsetXY)로 실제 부호 판정.
        /// </summary>
        public static int CameraSide(SightClearance clearance)
        {
            // 클리어런스 미제공 시 아이소 리그 기본 부호(+x, yaw -45)
            if (!clearance.Enabled)
                return 1;

            if (clearance.NearPoint.x >= 0f)
                return 1;

            return -1;
        }

        // 보행 바닥 스트립 생성은 길 카테고리로 이동 - SegmentPath.BuildWalkFloorStrips (M4-1)

        // -- 측면 럽블 매스 ---------------------------------------------------

        static void AddRubbleSides(
            List<EnvironmentBlock> blocks, ZoneDefinition definition, System.Random rng,
            SightClearance clearance, float segmentLength)
        {
            float length = segmentLength;
            float halfWidth = definition.corridorHalfWidth;
            int cameraSide = CameraSide(clearance);

            for (int side = -1; side <= 1; side += 2)
            {
                // 카메라측은 위로 쌓으면 발판이 가려진다 - 내려가는 단차 지형으로 대체
                if (side == cameraSide)
                {
                    AddTerracedDescent(blocks, rng, clearance, side, halfWidth, length);
                    continue;
                }

                // 높이를 이전 슬라이스와 보간해 들쭉날쭉하되 연속적인 능선을 만든다
                float previousHeight = NextRange(rng, 1.2f, 2.8f);

                for (float z = 0f; z < length; z += RubbleSliceDepth)
                {
                    float targetHeight = NextRange(rng, 0.6f, 3.6f);
                    float height = Mathf.Lerp(previousHeight, targetHeight, 0.55f);
                    previousHeight = height;

                    AddRubbleBlock(blocks, rng, clearance, side, halfWidth, z, height, rowOffset: 0f);

                    // 바깥 두 번째 열 - 더 높게 쌓아 협곡 실루엣 강조
                    if (rng.NextDouble() < 0.45)
                    {
                        float backHeight = height + NextRange(rng, 0.8f, 2.4f);
                        AddRubbleBlock(blocks, rng, clearance, side, halfWidth, z, backHeight, rowOffset: 2.1f);
                    }

                    // 드물게 랜드마크 기둥 - 원경에서 진행 방향 가늠용
                    if (rng.NextDouble() < 0.06)
                    {
                        float pillarHeight = NextRange(rng, 4.5f, 7f);
                        AddRubbleBlock(blocks, rng, clearance, side, halfWidth, z, pillarHeight, rowOffset: 1f);
                    }
                }
            }
        }

        // 카메라측 지형: 복도에서 멀어질수록 계단식으로 내려간다 (사용자 지시).
        // 상판(top)이 항상 보행면(y=0) 아래라 발판/플레이어를 절대 가리지 않는다
        static void AddTerracedDescent(
            List<EnvironmentBlock> blocks, System.Random rng, SightClearance clearance,
            int side, float halfWidth, float length)
        {
            const int TerraceRows = 3;
            const float RowSpacing = 2.3f;

            for (int row = 0; row < TerraceRows; row++)
            {
                // 열마다 한 단씩 낮아진다. 첫 열은 보행면 바로 아래
                float baseTop = -0.15f - row * NextRange(rng, 0.9f, 1.4f);
                float previousTop = baseTop;

                for (float z = 0f; z < length; z += RubbleSliceDepth)
                {
                    float targetTop = baseTop + NextRange(rng, -0.35f, 0.15f);
                    float top = Mathf.Lerp(previousTop, targetTop, 0.55f);
                    previousTop = top;

                    float width = NextRange(rng, 1.6f, 2.6f);
                    float depth = RubbleSliceDepth * NextRange(rng, 0.85f, 1.05f);
                    float height = NextRange(rng, 2.5f, 4f);
                    float x = side * (halfWidth + width * 0.5f + 0.15f + row * RowSpacing);

                    AddBlock(
                        blocks,
                        clearance,
                        new Vector3(x, top - height * 0.5f, z + RubbleSliceDepth * 0.5f),
                        Quaternion.identity,
                        new Vector3(width, height, depth),
                        rng.Next(0, 3),
                        RubbleWave,
                        NextPhase(rng));
                }
            }
        }

        static void AddRubbleBlock(
            List<EnvironmentBlock> blocks, System.Random rng, SightClearance clearance, int side,
            float halfWidth, float z, float height, float rowOffset)
        {
            float width = NextRange(rng, 1.5f, 2.5f);
            float x = side * (halfWidth + width * 0.5f + 0.15f + rowOffset);
            float depth = RubbleSliceDepth * NextRange(rng, 0.85f, 1.05f);

            AddBlock(
                blocks,
                clearance,
                new Vector3(x, height * 0.5f - 0.1f, z + RubbleSliceDepth * 0.5f),
                Quaternion.identity,
                new Vector3(width, height, depth),
                rng.Next(0, 3),
                RubbleWave,
                NextPhase(rng));
        }

        // -- 상부층 (복층 느낌) ------------------------------------------------

        static void AddUpperStory(
            List<EnvironmentBlock> blocks, ZoneDefinition definition, System.Random rng,
            SightClearance clearance, float segmentLength)
        {
            float length = segmentLength;
            float halfWidth = definition.corridorHalfWidth;
            int cameraSide = CameraSide(clearance);

            // 측면 상부 플랫폼: 복도 안쪽으로 오버행 - 2층 발코니/통로 느낌.
            // 카메라측은 상부 구조물이 발판을 가리므로 제외 (단차 하강 지형만)
            for (int side = -1; side <= 1; side += 2)
            {
                if (side == cameraSide)
                    continue;

                float z = NextRange(rng, 4f, 10f);

                while (z < length - 9f)
                {
                    if (rng.NextDouble() < 0.6)
                        AddPlatform(blocks, rng, clearance, side, halfWidth, z);

                    z += NextRange(rng, 9f, 16f);
                }
            }

            // 브릿지: 상부 통로. 카메라 시야 라인을 넘지 않게 카메라 반대편에서
            // 복도 중앙 부근까지만 걸친다 (시야 클리어런스와의 양립)
            float bridgeZ = NextRange(rng, 15f, 30f);

            while (bridgeZ < length - 10f)
            {
                if (rng.NextDouble() < 0.55)
                {
                    float height = NextRange(rng, 3.4f, 4f);
                    float depth = NextRange(rng, 2.2f, 3.2f);
                    float bridgeWidth = halfWidth + 4f;
                    float bridgeCenterX = -cameraSide * (halfWidth * 0.5f + 0.5f);

                    AddBlock(
                        blocks,
                        clearance,
                        new Vector3(bridgeCenterX, height, bridgeZ + depth * 0.5f),
                        Quaternion.identity,
                        new Vector3(bridgeWidth, 0.4f, depth),
                        UpperPalette(rng),
                        wave: 0f,
                        phase: 0f);
                }

                bridgeZ += NextRange(rng, 22f, 40f);
            }
        }

        static void AddPlatform(
            List<EnvironmentBlock> blocks, System.Random rng, SightClearance clearance,
            int side, float halfWidth, float z)
        {
            float width = NextRange(rng, 3f, 4.5f);
            float depth = NextRange(rng, 5f, 9f);
            float height = NextRange(rng, 3.1f, 3.9f);
            float overhang = NextRange(rng, 0.8f, 1.6f);

            float innerX = halfWidth - overhang;
            float centerX = side * (innerX + width * 0.5f);
            float centerZ = z + depth * 0.5f;

            // 슬래브
            AddBlock(
                blocks,
                clearance,
                new Vector3(centerX, height, centerZ),
                Quaternion.identity,
                new Vector3(width, 0.35f, depth),
                UpperPalette(rng));

            // 안쪽 모서리 지지 기둥
            AddBlock(
                blocks,
                clearance,
                new Vector3(side * (innerX + 0.2f), height * 0.5f, centerZ),
                Quaternion.identity,
                new Vector3(0.35f, height, 0.35f),
                UpperPalette(rng));

            // 플랫폼 위 잡동사니 실루엣
            if (rng.NextDouble() < 0.6)
            {
                float propSize = NextRange(rng, 0.5f, 1.1f);

                AddBlock(
                    blocks,
                    clearance,
                    new Vector3(
                        centerX + NextRange(rng, -width * 0.3f, width * 0.3f),
                        height + 0.175f + propSize * 0.5f,
                        centerZ + NextRange(rng, -depth * 0.3f, depth * 0.3f)),
                    Quaternion.identity,
                    new Vector3(propSize, propSize, propSize),
                    UpperPalette(rng));
            }
        }

        // -- 중경/원경 레이어 --------------------------------------------------

        // 중경 (팔레트 6): 시야 반대편 = 위로 솟는 매스 (협곡 너머 잔해 지대),
        // 카메라측 = 보행면 아래로 가라앉은 매스 - 내려간 단차 아래의 잔해 지대.
        // 카메라측 상판은 항상 y<0 이라 발판을 가리지 않는다 (사용자 지시)
        static void AddMidground(
            List<EnvironmentBlock> blocks, ZoneDefinition definition, System.Random rng,
            SightClearance clearance, float segmentLength)
        {
            float length = segmentLength;
            float halfWidth = definition.corridorHalfWidth;
            int cameraSide = CameraSide(clearance);

            for (int side = -1; side <= 1; side += 2)
            {
                bool sunken = side == cameraSide;

                for (float z = 0f; z < length; z += 4.5f)
                {
                    if (rng.NextDouble() >= (sunken ? 0.5 : 0.75))
                        continue;

                    float width = NextRange(rng, 3f, 6.5f);
                    float height = NextRange(rng, 4f, 10f);
                    float depth = NextRange(rng, 3.5f, 5.5f);
                    float x = side * (halfWidth + 5f + NextRange(rng, 0f, 6f));

                    // 카메라측은 상판이 단차 아래 - 아래로 뻗는 매스
                    float y = sunken
                        ? NextRange(rng, -2.6f, -1f) - height * 0.5f
                        : height * 0.5f - 0.1f;

                    AddBlock(
                        blocks,
                        clearance,
                        new Vector3(x, y, z + depth * 0.5f),
                        Quaternion.identity,
                        new Vector3(width, height, depth),
                        6,
                        MidWave,
                        NextPhase(rng));
                }
            }
        }

        // 원경 (팔레트 7): 시야 반대편 = 높은 스카이라인 실루엣,
        // 카메라측 = 더 내려간 저지대 - 복도가 능선 위에 있다는 인상을 만든다
        static void AddFarground(
            List<EnvironmentBlock> blocks, ZoneDefinition definition, System.Random rng,
            SightClearance clearance, float segmentLength)
        {
            float length = segmentLength;
            float halfWidth = definition.corridorHalfWidth;
            int cameraSide = CameraSide(clearance);

            for (int side = -1; side <= 1; side += 2)
            {
                bool sunken = side == cameraSide;

                for (float z = 0f; z < length; z += 7f)
                {
                    if (rng.NextDouble() >= (sunken ? 0.55 : 0.85))
                        continue;

                    float width = NextRange(rng, 5f, 10f);
                    float height = NextRange(rng, 8f, 20f);
                    float depth = NextRange(rng, 5f, 8f);
                    float x = side * (halfWidth + 13f + NextRange(rng, 0f, 14f));

                    float y = sunken
                        ? NextRange(rng, -4.5f, -2f) - height * 0.5f
                        : height * 0.5f - 0.1f;

                    AddBlock(
                        blocks,
                        clearance,
                        new Vector3(x, y, z + depth * 0.5f),
                        Quaternion.identity,
                        new Vector3(width, height, depth),
                        7,
                        FarWave,
                        NextPhase(rng));
                }
            }
        }

        // -- 보행로 내 데브리 --------------------------------------------------

        static void AddDebris(
            List<EnvironmentBlock> blocks, ZoneDefinition definition, System.Random rng,
            SightClearance clearance, float segmentLength)
        {
            float length = segmentLength;
            float halfWidth = definition.corridorHalfWidth;

            int count = Mathf.RoundToInt(length / 6f);

            for (int i = 0; i < count; i++)
            {
                float side = rng.Next(0, 2) == 0 ? -1f : 1f;
                float x = side * NextRange(rng, halfWidth * 0.55f, halfWidth * 0.95f);
                float z = NextRange(rng, 3f, length - 3f);
                float size = NextRange(rng, 0.25f, 0.55f);
                float yaw = (float)rng.NextDouble() * 90f;

                AddBlock(
                    blocks,
                    clearance,
                    new Vector3(x, size * 0.5f, z),
                    Quaternion.Euler(0f, yaw, 0f),
                    new Vector3(size, size, size),
                    5);
            }
        }

        // -- GameObject 백엔드 (배경 사전 배치/수동 편집 전용, ADR-0005) --------

        public static void BuildGameObjects(Transform parent, ZoneDefinition definition, System.Random rng)
        {
            BuildGameObjects(parent, definition, rng, default);
        }

        public static void BuildGameObjects(
            Transform parent, ZoneDefinition definition, System.Random rng, SightClearance clearance)
        {
            // 보행 바닥은 존이 소유한다 (Field/Zone) - 여기서는 배경 블록만 만든다
            List<EnvironmentBlock> blocks = GenerateBlocks(definition, rng, clearance);

            foreach (EnvironmentBlock block in blocks)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "EnvBlock";
                cube.transform.SetParent(parent, false);

                Matrix4x4 matrix = block.LocalMatrix;
                cube.transform.localPosition = matrix.GetColumn(3);
                cube.transform.localRotation = matrix.rotation;
                cube.transform.localScale = matrix.lossyScale;

                // 배경은 비주얼 전용 - 콜라이더 불필요 (좌우 이동은 사전 클램프)
                Collider cubeCollider = cube.GetComponent<Collider>();

                if (cubeCollider != null)
                    RemoveObject(cubeCollider);

                TintGameObject(cube, block.PaletteIndex);
            }
        }

        // -- 공통 -----------------------------------------------------------

        static void AddBlock(
            List<EnvironmentBlock> blocks, SightClearance clearance,
            Vector3 position, Quaternion rotation, Vector3 scale, int paletteIndex)
        {
            AddBlock(blocks, clearance, position, rotation, scale, paletteIndex, wave: 0f, phase: 0f);
        }

        static void AddBlock(
            List<EnvironmentBlock> blocks, SightClearance clearance,
            Vector3 position, Quaternion rotation, Vector3 scale,
            int paletteIndex, float wave, float phase)
        {
            // 카메라 시야 라인과 겹치면 생성 자체를 거부한다
            if (clearance.Rejects(position, scale))
                return;

            blocks.Add(new EnvironmentBlock
            {
                LocalMatrix = Matrix4x4.TRS(position, rotation, scale),
                PaletteIndex = paletteIndex,
                Wave = wave,
                Phase = phase,
            });
        }

        static int UpperPalette(System.Random rng)
        {
            return rng.Next(0, 2) == 0 ? 3 : 4;
        }

        static float NextRange(System.Random rng, float min, float max)
        {
            return Mathf.Lerp(min, max, (float)rng.NextDouble());
        }

        static float NextPhase(System.Random rng)
        {
            return (float)rng.NextDouble() * Mathf.PI * 2f;
        }

        // 에디트 모드(사전 배치 윈도우)에서도 호출되므로 Destroy/DestroyImmediate 분기
        static void RemoveObject(Object target)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(target);
                return;
            }

            Object.DestroyImmediate(target);
        }

        // 배경 블록 색칠 (사전 배치/런타임 공용) - 머티리얼 규칙을 한 곳에 유지
        internal static void TintGameObject(GameObject block, int paletteIndex)
        {
            Renderer blockRenderer = block.GetComponent<Renderer>();

            if (blockRenderer == null)
                return;

            Color color = Field.GreyboxTheme.Tint(
                Palette[Mathf.Clamp(paletteIndex, 0, Palette.Length - 1)],
                Field.GreyboxTone.Background);

#if UNITY_EDITOR
            // 에디트 모드(사전 배치)에서는 반드시 디스크 에셋 머티리얼 사용 -
            // 인메모리 머티리얼은 씬 저장 후 참조가 깨져 마젠타가 된다 (실제 발생)
            if (!Application.isPlaying)
            {
                blockRenderer.sharedMaterial = EnsureEditorMaterialAsset(paletteIndex, color);
                return;
            }
#endif

            // 플레이 모드: 세션 수명의 인스턴스로 충분
            Material material = new Material(blockRenderer.sharedMaterial);
            material.color = color;
            blockRenderer.sharedMaterial = material;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 사전 배치 배경 블록의 머티리얼. 이름은 팔레트 역할이다
        /// (Env_00_RubbleDark 등) - 색상 해시 이름은 어디에 쓰이는지 알 수 없다.
        /// </summary>
        static Material EnsureEditorMaterialAsset(int paletteIndex, Color color)
        {
            const string folder = "Assets/Materials/Greybox/Env";

            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Materials"))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Materials");

            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Materials/Greybox"))
                UnityEditor.AssetDatabase.CreateFolder("Assets/Materials", "Greybox");

            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
                UnityEditor.AssetDatabase.CreateFolder("Assets/Materials/Greybox", "Env");

            string path = $"{folder}/Env_{paletteIndex:00}_{PaletteName(paletteIndex)}.mat";

            Material material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;

            UnityEditor.AssetDatabase.CreateAsset(material, path);
            return material;
        }
#endif
    }
}
