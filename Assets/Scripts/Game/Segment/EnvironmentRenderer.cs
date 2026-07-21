using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scavenger.Segment
{
    /// <summary>
    /// 배경 블록 인스턴스 렌더러 (ADR-0005). GameObject 없이
    /// Graphics.RenderMeshInstanced로 팔레트별 배치를 매 프레임 그린다.
    /// 웹(WebGL2) 호환 - BatchRendererGroup은 웹 미지원이라 채택하지 않음.
    /// 구간 청크 단위로 등록/해제 (SegmentSpawner가 소유).
    /// </summary>
    public sealed class EnvironmentRenderer : MonoBehaviour
    {
        [Header("비우면 런타임 폴백 (내장 큐브 + URP Lit)")]
        [SerializeField] Mesh instanceMesh;
        [SerializeField] Material baseMaterial;

        sealed class Chunk
        {
            public Matrix4x4[][] MatricesByPalette;
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

            List<Matrix4x4>[] gather = new List<Matrix4x4>[SegmentEnvironment.Palette.Length];

            for (int i = 0; i < gather.Length; i++)
                gather[i] = new List<Matrix4x4>();

            Bounds bounds = new Bounds(worldOffset, Vector3.one);
            Matrix4x4 offsetMatrix = Matrix4x4.Translate(worldOffset);

            foreach (EnvironmentBlock block in blocks)
            {
                Matrix4x4 world = offsetMatrix * block.LocalMatrix;
                gather[block.PaletteIndex].Add(world);

                // 컬링 바운드: 위치 +- 스케일 근사
                Vector3 center = world.GetColumn(3);
                Vector3 extents = world.lossyScale * 0.5f;
                bounds.Encapsulate(center + extents);
                bounds.Encapsulate(center - extents);
            }

            Chunk chunk = new Chunk
            {
                MatricesByPalette = new Matrix4x4[gather.Length][],
                Bounds = bounds,
            };

            for (int i = 0; i < gather.Length; i++)
                chunk.MatricesByPalette[i] = gather[i].ToArray();

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

        void Update()
        {
            if (chunks.Count == 0)
                return;

            EnsureResources();

            foreach (Chunk chunk in chunks.Values)
                DrawChunk(chunk);
        }

        void DrawChunk(Chunk chunk)
        {
            for (int palette = 0; palette < chunk.MatricesByPalette.Length; palette++)
            {
                Matrix4x4[] matrices = chunk.MatricesByPalette[palette];

                if (matrices == null || matrices.Length == 0)
                    continue;

                RenderParams renderParams = new RenderParams(paletteMaterials[palette])
                {
                    worldBounds = chunk.Bounds,
                    shadowCastingMode = ShadowCastingMode.On,
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
