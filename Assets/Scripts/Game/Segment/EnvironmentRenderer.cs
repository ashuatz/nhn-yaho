using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scavenger.Segment
{
    /// <summary>
    /// 배경 블록 인스턴스 렌더러 (ADR-0005). GameObject 없이
    /// Graphics.RenderMeshInstanced로 팔레트별 배치를 매 프레임 그린다.
    /// 웹(WebGL2) 호환 - BatchRendererGroup은 웹 미지원이라 채택하지 않음.
    ///
    /// 셀 출렁임 (brg-shooter 차용): Wave가 0보다 큰 블록만 동적 그룹으로 분리해
    /// 매 프레임 y 오프셋(사인파 + 블록별 위상)으로 행렬을 다시 쓴다.
    /// 정적 블록(상부층/데브리)은 등록 시 1회만 행렬을 만든다.
    /// 원거리 팔레트(FarPaletteStart 이상)는 그림자를 끈다.
    /// </summary>
    public sealed class EnvironmentRenderer : MonoBehaviour
    {
        /// <summary>임펄스 발신자(폭탄/붕괴)가 참조하는 활성 렌더러.</summary>
        public static EnvironmentRenderer Active { get; private set; }

        [Header("비우면 런타임 폴백 (내장 큐브 + URP Lit)")]
        [SerializeField] Mesh instanceMesh;
        [SerializeField] Material baseMaterial;

        [Header("셀 출렁임 (Wave 블록 전용)")]
        public bool animate = true;
        public float waveSpeed = 1.4f;
        public float waveAmplitude = 0.35f;

        [Header("이벤트 임펄스 (brg-shooter 바운스 이식) - 스프링 감쇠")]
        public float impulseStiffness = 26f;
        public float impulseDamping = 5f;

        sealed class AnimatedGroup
        {
            public Vector3[] BasePositions;
            public Quaternion[] BaseRotations;
            public Vector3[] BaseScales;
            public float[] Waves;
            public float[] Phases;
            public float[] ImpulseOffsets;
            public float[] ImpulseVelocities;
            public Matrix4x4[] WorkMatrices;
        }

        sealed class Chunk
        {
            public Matrix4x4[][] StaticByPalette;
            public AnimatedGroup[] AnimatedByPalette;
            public Bounds Bounds;
        }

        readonly Dictionary<int, Chunk> chunks = new Dictionary<int, Chunk>();
        int nextChunkId = 1;
        Material[] paletteMaterials;

        // Graphics.RenderMeshInstanced의 콜당 인스턴스 상한
        const int MaxInstancesPerCall = 1023;

        /// <summary>블록 목록을 월드 오프셋 적용해 등록하고 청크 id를 돌려준다.</summary>
        public int AddChunk(List<EnvironmentBlock> blocks, Vector3 worldOffset)
        {
            EnsureResources();

            int paletteCount = SegmentEnvironment.Palette.Length;
            List<Matrix4x4>[] staticGather = new List<Matrix4x4>[paletteCount];
            List<EnvironmentBlock>[] animatedGather = new List<EnvironmentBlock>[paletteCount];

            for (int i = 0; i < paletteCount; i++)
            {
                staticGather[i] = new List<Matrix4x4>();
                animatedGather[i] = new List<EnvironmentBlock>();
            }

            Bounds bounds = new Bounds(worldOffset, Vector3.one);
            Matrix4x4 offsetMatrix = Matrix4x4.Translate(worldOffset);

            foreach (EnvironmentBlock block in blocks)
            {
                Matrix4x4 world = offsetMatrix * block.LocalMatrix;

                if (block.Wave > 0f)
                {
                    EnvironmentBlock worldBlock = block;
                    worldBlock.LocalMatrix = world;
                    animatedGather[block.PaletteIndex].Add(worldBlock);
                }
                else
                {
                    staticGather[block.PaletteIndex].Add(world);
                }

                // 컬링 바운드: 위치 +- 스케일 근사 (+ 출렁임 여유)
                Vector3 center = world.GetColumn(3);
                Vector3 extents = world.lossyScale * 0.5f + Vector3.up * waveAmplitude;
                bounds.Encapsulate(center + extents);
                bounds.Encapsulate(center - extents);
            }

            Chunk chunk = new Chunk
            {
                StaticByPalette = new Matrix4x4[paletteCount][],
                AnimatedByPalette = new AnimatedGroup[paletteCount],
                Bounds = bounds,
            };

            for (int i = 0; i < paletteCount; i++)
            {
                chunk.StaticByPalette[i] = staticGather[i].ToArray();
                chunk.AnimatedByPalette[i] = BuildAnimatedGroup(animatedGather[i]);
            }

            int id = nextChunkId;
            nextChunkId += 1;

            chunks.Add(id, chunk);
            return id;
        }

        public void RemoveChunk(int id)
        {
            chunks.Remove(id);
        }

        public void ClearChunks()
        {
            chunks.Clear();
        }

        void OnEnable()
        {
            Active = this;
        }

        void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        /// <summary>
        /// 이벤트 지점 주변의 동적 셀에 바운스 임펄스를 준다 (폭발, 붕괴 등).
        /// brg-shooter의 이벤트 반응 셀 이동을 스프링-감쇠로 이식한 것.
        /// </summary>
        public void AddImpulse(Vector3 worldPosition, float radius, float strength)
        {
            foreach (Chunk chunk in chunks.Values)
            {
                foreach (AnimatedGroup group in chunk.AnimatedByPalette)
                {
                    if (group == null)
                        continue;

                    for (int i = 0; i < group.BasePositions.Length; i++)
                    {
                        Vector3 toCell = group.BasePositions[i] - worldPosition;
                        toCell.y = 0f;

                        float distance = toCell.magnitude;

                        if (distance > radius)
                            continue;

                        float falloff = 1f - distance / radius;
                        group.ImpulseVelocities[i] += strength * falloff;
                    }
                }
            }
        }

        static AnimatedGroup BuildAnimatedGroup(List<EnvironmentBlock> worldBlocks)
        {
            if (worldBlocks.Count == 0)
                return null;

            int count = worldBlocks.Count;

            AnimatedGroup group = new AnimatedGroup
            {
                BasePositions = new Vector3[count],
                BaseRotations = new Quaternion[count],
                BaseScales = new Vector3[count],
                Waves = new float[count],
                Phases = new float[count],
                ImpulseOffsets = new float[count],
                ImpulseVelocities = new float[count],
                WorkMatrices = new Matrix4x4[count],
            };

            for (int i = 0; i < count; i++)
            {
                Matrix4x4 matrix = worldBlocks[i].LocalMatrix;

                group.BasePositions[i] = matrix.GetColumn(3);
                group.BaseRotations[i] = matrix.rotation;
                group.BaseScales[i] = matrix.lossyScale;
                group.Waves[i] = worldBlocks[i].Wave;
                group.Phases[i] = worldBlocks[i].Phase;
                group.WorkMatrices[i] = matrix;
            }

            return group;
        }

        void Update()
        {
            if (chunks.Count == 0)
                return;

            EnsureResources();

            float time = Time.time;

            foreach (Chunk chunk in chunks.Values)
            {
                if (animate)
                    AnimateChunk(chunk, time);

                DrawChunk(chunk);
            }
        }

        void AnimateChunk(Chunk chunk, float time)
        {
            float deltaTime = Time.deltaTime;

            foreach (AnimatedGroup group in chunk.AnimatedByPalette)
            {
                if (group == null)
                    continue;

                for (int i = 0; i < group.WorkMatrices.Length; i++)
                {
                    // 스프링-감쇠 임펄스 적분 (brg-shooter 바운스 이식)
                    float offset = group.ImpulseOffsets[i];
                    float velocity = group.ImpulseVelocities[i];

                    velocity += (-impulseStiffness * offset - impulseDamping * velocity) * deltaTime;
                    offset += velocity * deltaTime;

                    group.ImpulseOffsets[i] = offset;
                    group.ImpulseVelocities[i] = velocity;

                    float offsetY = Mathf.Sin(time * waveSpeed + group.Phases[i])
                                    * waveAmplitude * group.Waves[i]
                                    + offset;

                    Vector3 position = group.BasePositions[i];
                    position.y += offsetY;

                    group.WorkMatrices[i] = Matrix4x4.TRS(position, group.BaseRotations[i], group.BaseScales[i]);
                }
            }
        }

        void DrawChunk(Chunk chunk)
        {
            for (int palette = 0; palette < chunk.StaticByPalette.Length; palette++)
            {
                // 원거리 레이어는 그림자 생략 (비용 절감, 포그에 묻힘)
                bool castShadows = palette < SegmentEnvironment.FarPaletteStart;

                DrawBatch(chunk.StaticByPalette[palette], palette, chunk.Bounds, castShadows);

                AnimatedGroup group = chunk.AnimatedByPalette[palette];

                if (group != null)
                    DrawBatch(group.WorkMatrices, palette, chunk.Bounds, castShadows);
            }
        }

        void DrawBatch(Matrix4x4[] matrices, int palette, Bounds bounds, bool castShadows)
        {
            if (matrices == null || matrices.Length == 0)
                return;

            RenderParams renderParams = new RenderParams(paletteMaterials[palette])
            {
                worldBounds = bounds,
                shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows = true,
            };

            int drawn = 0;

            while (drawn < matrices.Length)
            {
                int count = Mathf.Min(MaxInstancesPerCall, matrices.Length - drawn);
                Graphics.RenderMeshInstanced(renderParams, instanceMesh, 0, matrices, count, drawn);
                drawn += count;
            }
        }

        void EnsureResources()
        {
            if (paletteMaterials != null)
                return;

            if (instanceMesh == null)
                instanceMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            if (baseMaterial == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
                baseMaterial = new Material(litShader);
            }

            Color[] palette = SegmentEnvironment.Palette;
            paletteMaterials = new Material[palette.Length];

            for (int i = 0; i < palette.Length; i++)
            {
                Material material = new Material(baseMaterial);
                material.color = palette[i];
                material.enableInstancing = true;
                paletteMaterials[i] = material;
            }
        }
    }
}
