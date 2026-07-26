using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scavenger.Segment
{
    /// <summary>
    /// 배경 블록 인스턴스 렌더러 (ADR-0005). GameObject 없이
    /// Graphics.RenderMeshInstanced로 배치를 매 프레임 그린다.
    /// 웹(WebGL2) 호환 - BatchRendererGroup은 웹 미지원이라 채택하지 않음.
    ///
    /// 배치 키는 (메시, 팔레트)다. 트림시트 블록 세트(EnvironmentBlockSet)가 배선되면
    /// 크기별로 구운 메시를 쓰기 때문에 팔레트만으로는 묶을 수 없다 -
    /// 셀 2m 기준 존당 20종 남짓이라 인스턴싱 이점은 유지된다 (측정값).
    /// 세트가 없으면 예전처럼 큐브 하나를 스케일해 팔레트별로만 묶는다.
    ///
    /// 셀 출렁임 (brg-shooter 차용): Wave가 0보다 큰 블록만 동적 배치로 분리해
    /// 매 프레임 y 오프셋(사인파 + 블록별 위상)으로 행렬을 다시 쓴다.
    /// 정적 블록(상부층/데브리)은 등록 시 1회만 행렬을 만든다.
    /// 원거리 팔레트(FarPaletteStart 이상)는 그림자를 끈다.
    /// </summary>
    public sealed class EnvironmentRenderer : MonoBehaviour
    {
        /// <summary>임펄스 발신자(폭탄/붕괴)가 참조하는 활성 렌더러.</summary>
        public static EnvironmentRenderer Active { get; private set; }

        [Header("트림시트 배경 블록 세트 (비우면 큐브 인스턴싱 폴백)")]
        [SerializeField] EnvironmentBlockSet blockSet;

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

        /// <summary>드로우 한 번의 단위 (메시 + 팔레트 머티리얼).</summary>
        sealed class Batch
        {
            /// <summary>블록 세트의 항목 인덱스. 세트가 없으면 -1 (공용 큐브).</summary>
            public int EntryIndex;

            public int Palette;

            /// <summary>정적 블록 행렬. 없으면 null.</summary>
            public Matrix4x4[] Static;

            /// <summary>출렁이는 블록. 없으면 null.</summary>
            public AnimatedGroup Animated;
        }

        sealed class Chunk
        {
            public Batch[] Batches;
            public Bounds Bounds;
        }

        // 배치를 모으는 임시 키 (메시 항목 + 팔레트 + 출렁임 여부)
        readonly struct BatchKey : System.IEquatable<BatchKey>
        {
            public readonly int EntryIndex;
            public readonly int Palette;
            public readonly bool Animated;

            public BatchKey(int entryIndex, int palette, bool animated)
            {
                EntryIndex = entryIndex;
                Palette = palette;
                Animated = animated;
            }

            public bool Equals(BatchKey other)
            {
                if (EntryIndex != other.EntryIndex)
                    return false;

                if (Palette != other.Palette)
                    return false;

                return Animated == other.Animated;
            }

            public override bool Equals(object obj)
            {
                return obj is BatchKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (EntryIndex * 397) ^ (Palette * 31) ^ (Animated ? 1 : 0);
            }
        }

        readonly Dictionary<int, Chunk> chunks = new Dictionary<int, Chunk>();
        int nextChunkId = 1;
        Material[] paletteMaterials;

        // 블록 세트 검사 결과 (세션 1회). 인스턴싱이 꺼진 세트는 쓰지 않는다
        bool blockSetChecked;
        bool blockSetUsable;

        // Graphics.RenderMeshInstanced의 콜당 인스턴스 상한
        const int MaxInstancesPerCall = 1023;

        /// <summary>블록 목록을 월드 오프셋 적용해 등록하고 청크 id를 돌려준다.</summary>
        public int AddChunk(List<EnvironmentBlock> blocks, Vector3 worldOffset)
        {
            EnsureResources();

            Dictionary<BatchKey, List<Matrix4x4>> staticGather =
                new Dictionary<BatchKey, List<Matrix4x4>>();
            Dictionary<BatchKey, List<EnvironmentBlock>> animatedGather =
                new Dictionary<BatchKey, List<EnvironmentBlock>>();

            Bounds bounds = new Bounds(worldOffset, Vector3.one);
            Matrix4x4 offsetMatrix = Matrix4x4.Translate(worldOffset);

            foreach (EnvironmentBlock block in blocks)
            {
                Matrix4x4 world = ResolveWorldMatrix(offsetMatrix, block, out int entryIndex);

                BatchKey key = new BatchKey(entryIndex, block.PaletteIndex, block.Wave > 0f);

                if (block.Wave > 0f)
                {
                    EnvironmentBlock worldBlock = block;
                    worldBlock.LocalMatrix = world;

                    Gather(animatedGather, key).Add(worldBlock);
                }
                else
                {
                    Gather(staticGather, key).Add(world);
                }

                // 컬링 바운드: 위치 +- 크기 근사 (+ 출렁임 여유).
                // 구운 메시는 행렬 스케일이 1이라 실제 치수를 세트에서 가져온다
                Vector3 center = world.GetColumn(3);
                Vector3 size = ResolveDrawSize(entryIndex, world.lossyScale);
                Vector3 extents = size * 0.5f + Vector3.up * waveAmplitude;

                bounds.Encapsulate(center + extents);
                bounds.Encapsulate(center - extents);
            }

            Chunk chunk = new Chunk
            {
                Batches = BuildBatches(staticGather, animatedGather),
                Bounds = bounds,
            };

            int id = nextChunkId;
            nextChunkId += 1;

            chunks.Add(id, chunk);
            return id;
        }

        /// <summary>
        /// 블록의 월드 행렬. 트림시트 세트가 있으면 크기가 메시에 구워져 있으므로
        /// 스케일을 1로 두고, 스냅으로 커진 만큼 위치를 보정한다 (윗면/복도쪽 면 유지).
        /// </summary>
        Matrix4x4 ResolveWorldMatrix(Matrix4x4 offsetMatrix, EnvironmentBlock block, out int entryIndex)
        {
            Matrix4x4 world = offsetMatrix * block.LocalMatrix;

            entryIndex = -1;

            if (!HasBlockSet())
                return world;

            Vector3 originalSize = world.lossyScale;
            entryIndex = blockSet.ResolveEntry(originalSize);

            if (entryIndex < 0)
                return world;

            Vector3 snappedSize = blockSet.SizeAt(entryIndex);
            Vector3 position = blockSet.AlignPosition(world.GetColumn(3), originalSize, snappedSize);

            return Matrix4x4.TRS(position, world.rotation, Vector3.one);
        }

        Vector3 ResolveDrawSize(int entryIndex, Vector3 matrixScale)
        {
            if (entryIndex < 0 || blockSet == null)
                return matrixScale;

            return blockSet.SizeAt(entryIndex);
        }

        static List<T> Gather<T>(Dictionary<BatchKey, List<T>> gather, BatchKey key)
        {
            if (gather.TryGetValue(key, out List<T> list))
                return list;

            list = new List<T>();
            gather.Add(key, list);

            return list;
        }

        static Batch[] BuildBatches(
            Dictionary<BatchKey, List<Matrix4x4>> staticGather,
            Dictionary<BatchKey, List<EnvironmentBlock>> animatedGather)
        {
            List<Batch> batches = new List<Batch>(staticGather.Count + animatedGather.Count);

            foreach (KeyValuePair<BatchKey, List<Matrix4x4>> pair in staticGather)
            {
                batches.Add(new Batch
                {
                    EntryIndex = pair.Key.EntryIndex,
                    Palette = pair.Key.Palette,
                    Static = pair.Value.ToArray(),
                });
            }

            foreach (KeyValuePair<BatchKey, List<EnvironmentBlock>> pair in animatedGather)
            {
                batches.Add(new Batch
                {
                    EntryIndex = pair.Key.EntryIndex,
                    Palette = pair.Key.Palette,
                    Animated = BuildAnimatedGroup(pair.Value),
                });
            }

            return batches.ToArray();
        }

        bool HasBlockSet()
        {
            if (blockSet == null)
                return false;

            if (!blockSet.IsReady)
                return false;

            return ValidateBlockSetMaterials();
        }

        /// <summary>
        /// 세트 머티리얼이 인스턴싱을 켜 두었는지 1회 확인한다.
        /// 꺼져 있으면 RenderMeshInstanced가 매 프레임 예외를 던지고 배경이 통째로
        /// 사라진다 (실제 발생) - 그런 세트는 쓰지 않고 큐브 폴백으로 돌아간다.
        /// </summary>
        bool ValidateBlockSetMaterials()
        {
            if (blockSetChecked)
                return blockSetUsable;

            blockSetChecked = true;
            blockSetUsable = true;

            foreach (Material material in blockSet.paletteMaterials)
            {
                if (material == null || material.enableInstancing)
                    continue;

                blockSetUsable = false;

                UnityEngine.Debug.LogWarning(
                    $"[Env] 배경 블록 세트의 머티리얼 {material.name}에 GPU 인스턴싱이 꺼져 있다. " +
                    "큐브 폴백으로 그린다 - Scavenger > Field Trim Sheet > Bake Background Blocks로 " +
                    "다시 구우면 켜진다.");

                break;
            }

            return blockSetUsable;
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
                foreach (Batch batch in chunk.Batches)
                {
                    AnimatedGroup group = batch.Animated;

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

            foreach (Batch batch in chunk.Batches)
            {
                AnimatedGroup group = batch.Animated;

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
            foreach (Batch batch in chunk.Batches)
            {
                // 원거리 레이어는 그림자 생략 (비용 절감, 포그에 묻힘)
                bool castShadows = batch.Palette < SegmentEnvironment.FarPaletteStart;

                if (batch.Static != null)
                    DrawBatch(batch.Static, batch, chunk.Bounds, castShadows);

                if (batch.Animated != null)
                    DrawBatch(batch.Animated.WorkMatrices, batch, chunk.Bounds, castShadows);
            }
        }

        void DrawBatch(Matrix4x4[] matrices, Batch batch, Bounds bounds, bool castShadows)
        {
            if (matrices == null || matrices.Length == 0)
                return;

            Mesh mesh = ResolveMesh(batch.EntryIndex);

            if (mesh == null)
                return;

            RenderParams renderParams = new RenderParams(ResolveMaterial(batch.Palette))
            {
                worldBounds = bounds,
                shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows = true,
            };

            int drawn = 0;

            while (drawn < matrices.Length)
            {
                int count = Mathf.Min(MaxInstancesPerCall, matrices.Length - drawn);
                Graphics.RenderMeshInstanced(renderParams, mesh, 0, matrices, count, drawn);
                drawn += count;
            }
        }

        Mesh ResolveMesh(int entryIndex)
        {
            if (entryIndex < 0 || !HasBlockSet())
                return instanceMesh;

            Mesh mesh = blockSet.MeshAt(entryIndex);

            if (mesh != null)
                return mesh;

            return instanceMesh;
        }

        // 세트가 쓸 수 없다고 판정되면 머티리얼도 폴백이어야 한다 -
        // 메시만 되돌리고 머티리얼을 그대로 두면 인스턴싱 예외가 그대로 난다
        Material ResolveMaterial(int palette)
        {
            if (HasBlockSet())
            {
                Material fromSet = blockSet.MaterialFor(palette);

                if (fromSet != null)
                    return fromSet;
            }

            return paletteMaterials[Mathf.Clamp(palette, 0, paletteMaterials.Length - 1)];
        }

        void EnsureResources()
        {
            if (paletteMaterials != null)
                return;

            if (instanceMesh == null)
                instanceMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            Color[] palette = SegmentEnvironment.Palette;
            paletteMaterials = new Material[palette.Length];

            for (int i = 0; i < palette.Length; i++)
            {
                // 배경도 런타임 생성물과 같은 기준 머티리얼(Common.mat) 쉐이더를 쓴다
                // (사용자 지시). 팔레트 색당 1장 공유 - 파괴 책임은 팔레트에 있다
                Material shared = Field.GreyboxPalette.GetTinted(palette[i]);

                if (shared != null)
                {
                    paletteMaterials[i] = shared;
                    continue;
                }

                // 폴백: 기준 머티리얼 미주입 (에디터 프리뷰 등)
                if (baseMaterial == null)
                {
                    Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
                    baseMaterial = new Material(litShader);
                }

                Material material = new Material(baseMaterial);
                material.color = palette[i];
                material.enableInstancing = true;
                paletteMaterials[i] = material;
            }
        }
    }
}
