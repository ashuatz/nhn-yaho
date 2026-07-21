using System.Collections.Generic;
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
    /// 구간 그레이박스 환경 PCG 데이터 생성기 (ADR-0003/0004/0005 - 공간감 검증용).
    /// 평탄한 보행로 양옆으로 복셀풍 럽블 매스 + 상부층(복층) + 데브리.
    /// 보행 영역은 항상 y=0 평면 유지. 난수는 주입된 rng(런 시드) 사용.
    /// 출력은 인스턴스 블록 데이터 - GameObject를 만들지 않는다 (웹 호환 인스턴싱).
    /// 보행로 바닥만 GameObject (콜라이더 필요).
    /// </summary>
    public static class SegmentEnvironment
    {
        /// <summary>블록 색 팔레트. 인덱스가 EnvironmentBlock.PaletteIndex와 대응.</summary>
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

        /// <summary>이 인덱스부터는 원거리 레이어 - 렌더러가 그림자를 끈다.</summary>
        public const int FarPaletteStart = 6;

        const float RubbleSliceDepth = 2.2f;

        // 레이어별 출렁임 강도 (brg-shooter 셀 웨이브 차용)
        const float RubbleWave = 0.35f;
        const float MidWave = 0.6f;
        const float FarWave = 1f;

        // -- 데이터 생성 ------------------------------------------------------

        public static List<EnvironmentBlock> GenerateBlocks(SegmentDefinition definition, System.Random rng)
        {
            List<EnvironmentBlock> blocks = new List<EnvironmentBlock>(384);

            AddRubbleSides(blocks, definition, rng);
            AddUpperStory(blocks, definition, rng);
            AddDebris(blocks, definition, rng);
            AddMidground(blocks, definition, rng);
            AddFarground(blocks, definition, rng);

            return blocks;
        }

        /// <summary>보행로 바닥. 콜라이더가 필요해 유일하게 GameObject로 만든다.</summary>
        public static GameObject BuildWalkFloor(Transform parent, SegmentDefinition definition)
        {
            float length = definition.lengthMeters;
            float halfWidth = definition.corridorHalfWidth;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(parent, false);
            floor.transform.localScale = new Vector3(halfWidth * 2f + 1f, 0.2f, length);
            floor.transform.localPosition = new Vector3(0f, -0.1f, length * 0.5f);

            TintGameObject(floor, new Color(0.3f, 0.31f, 0.33f));
            return floor;
        }

        // -- 측면 럽블 매스 ---------------------------------------------------

        static void AddRubbleSides(List<EnvironmentBlock> blocks, SegmentDefinition definition, System.Random rng)
        {
            float length = definition.lengthMeters;
            float halfWidth = definition.corridorHalfWidth;

            for (int side = -1; side <= 1; side += 2)
            {
                // 높이를 이전 슬라이스와 보간해 들쭉날쭉하되 연속적인 능선을 만든다
                float previousHeight = NextRange(rng, 1.2f, 2.8f);

                for (float z = 0f; z < length; z += RubbleSliceDepth)
                {
                    float targetHeight = NextRange(rng, 0.6f, 3.6f);
                    float height = Mathf.Lerp(previousHeight, targetHeight, 0.55f);
                    previousHeight = height;

                    AddRubbleBlock(blocks, rng, side, halfWidth, z, height, rowOffset: 0f);

                    // 바깥 두 번째 열 - 더 높게 쌓아 협곡 실루엣 강조
                    if (rng.NextDouble() < 0.45)
                    {
                        float backHeight = height + NextRange(rng, 0.8f, 2.4f);
                        AddRubbleBlock(blocks, rng, side, halfWidth, z, backHeight, rowOffset: 2.1f);
                    }

                    // 드물게 랜드마크 기둥 - 원경에서 진행 방향 가늠용
                    if (rng.NextDouble() < 0.06)
                    {
                        float pillarHeight = NextRange(rng, 4.5f, 7f);
                        AddRubbleBlock(blocks, rng, side, halfWidth, z, pillarHeight, rowOffset: 1f);
                    }
                }
            }
        }

        static void AddRubbleBlock(
            List<EnvironmentBlock> blocks, System.Random rng, int side, float halfWidth,
            float z, float height, float rowOffset)
        {
            float width = NextRange(rng, 1.5f, 2.5f);
            float x = side * (halfWidth + width * 0.5f + 0.15f + rowOffset);
            float depth = RubbleSliceDepth * NextRange(rng, 0.85f, 1.05f);

            AddBlock(
                blocks,
                new Vector3(x, height * 0.5f - 0.1f, z + RubbleSliceDepth * 0.5f),
                Quaternion.identity,
                new Vector3(width, height, depth),
                rng.Next(0, 3),
                RubbleWave,
                NextPhase(rng));
        }

        // -- 상부층 (복층 느낌) ------------------------------------------------

        static void AddUpperStory(List<EnvironmentBlock> blocks, SegmentDefinition definition, System.Random rng)
        {
            float length = definition.lengthMeters;
            float halfWidth = definition.corridorHalfWidth;

            // 측면 상부 플랫폼: 복도 안쪽으로 오버행 - 2층 발코니/통로 느낌
            for (int side = -1; side <= 1; side += 2)
            {
                float z = NextRange(rng, 4f, 10f);

                while (z < length - 9f)
                {
                    if (rng.NextDouble() < 0.6)
                        AddPlatform(blocks, rng, side, halfWidth, z);

                    z += NextRange(rng, 9f, 16f);
                }
            }

            // 브릿지: 복도를 가로지르는 상부 통로 - 플레이어 머리 위를 지나간다
            float bridgeZ = NextRange(rng, 15f, 30f);

            while (bridgeZ < length - 10f)
            {
                if (rng.NextDouble() < 0.55)
                {
                    float height = NextRange(rng, 3.4f, 4f);
                    float depth = NextRange(rng, 2.2f, 3.2f);

                    AddBlock(
                        blocks,
                        new Vector3(0f, height, bridgeZ + depth * 0.5f),
                        Quaternion.identity,
                        new Vector3(halfWidth * 2f + 5f, 0.4f, depth),
                        UpperPalette(rng));
                }

                bridgeZ += NextRange(rng, 22f, 40f);
            }
        }

        static void AddPlatform(
            List<EnvironmentBlock> blocks, System.Random rng, int side, float halfWidth, float z)
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
                new Vector3(centerX, height, centerZ),
                Quaternion.identity,
                new Vector3(width, 0.35f, depth),
                UpperPalette(rng));

            // 안쪽 모서리 지지 기둥
            AddBlock(
                blocks,
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

        static void AddMidground(List<EnvironmentBlock> blocks, SegmentDefinition definition, System.Random rng)
        {
            float length = definition.lengthMeters;
            float halfWidth = definition.corridorHalfWidth;

            for (int side = -1; side <= 1; side += 2)
            {
                for (float z = 0f; z < length; z += 4.5f)
                {
                    if (rng.NextDouble() >= 0.7)
                        continue;

                    float width = NextRange(rng, 3f, 6f);
                    float height = NextRange(rng, 3f, 8f);
                    float depth = NextRange(rng, 3.5f, 5.5f);
                    float x = side * (halfWidth + 5f + NextRange(rng, 0f, 6f));

                    AddBlock(
                        blocks,
                        new Vector3(x, height * 0.5f - 0.1f, z + depth * 0.5f),
                        Quaternion.identity,
                        new Vector3(width, height, depth),
                        6,
                        MidWave,
                        NextPhase(rng));
                }
            }
        }

        static void AddFarground(List<EnvironmentBlock> blocks, SegmentDefinition definition, System.Random rng)
        {
            float length = definition.lengthMeters;
            float halfWidth = definition.corridorHalfWidth;

            for (int side = -1; side <= 1; side += 2)
            {
                for (float z = 0f; z < length; z += 7f)
                {
                    if (rng.NextDouble() >= 0.8)
                        continue;

                    float width = NextRange(rng, 5f, 10f);
                    float height = NextRange(rng, 6f, 16f);
                    float depth = NextRange(rng, 5f, 8f);
                    float x = side * (halfWidth + 13f + NextRange(rng, 0f, 14f));

                    AddBlock(
                        blocks,
                        new Vector3(x, height * 0.5f - 0.1f, z + depth * 0.5f),
                        Quaternion.identity,
                        new Vector3(width, height, depth),
                        7,
                        FarWave,
                        NextPhase(rng));
                }
            }
        }

        // -- 보행로 내 데브리 --------------------------------------------------

        static void AddDebris(List<EnvironmentBlock> blocks, SegmentDefinition definition, System.Random rng)
        {
            float length = definition.lengthMeters;
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
                    new Vector3(x, size * 0.5f, z),
                    Quaternion.Euler(0f, yaw, 0f),
                    new Vector3(size, size, size),
                    5);
            }
        }

        // -- GameObject 백엔드 (배경 사전 배치/수동 편집 전용, ADR-0005) --------

        public static void BuildGameObjects(Transform parent, SegmentDefinition definition, System.Random rng)
        {
            BuildWalkFloor(parent, definition);

            List<EnvironmentBlock> blocks = GenerateBlocks(definition, rng);

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

                TintGameObject(cube, Palette[block.PaletteIndex]);
            }
        }

        // -- 공통 -----------------------------------------------------------

        static void AddBlock(
            List<EnvironmentBlock> blocks, Vector3 position, Quaternion rotation, Vector3 scale, int paletteIndex)
        {
            AddBlock(blocks, position, rotation, scale, paletteIndex, wave: 0f, phase: 0f);
        }

        static void AddBlock(
            List<EnvironmentBlock> blocks, Vector3 position, Quaternion rotation, Vector3 scale,
            int paletteIndex, float wave, float phase)
        {
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

        static void TintGameObject(GameObject block, Color color)
        {
            Renderer blockRenderer = block.GetComponent<Renderer>();

            if (blockRenderer == null)
                return;

            // 에디트 모드에서 .material 접근은 에러 - 인스턴스를 만들어 교체 (양쪽 모드 공용)
            Material material = new Material(blockRenderer.sharedMaterial);
            material.color = color;
            blockRenderer.sharedMaterial = material;
        }
    }
}
