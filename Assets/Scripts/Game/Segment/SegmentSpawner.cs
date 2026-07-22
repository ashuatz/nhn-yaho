using System.Collections.Generic;
using Scavenger.Loot;
using Scavenger.Player;
using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 구간 그레이박스 생성/제거의 단일 경계. 생성 방식(Instantiate/Destroy)을
    /// 이 클래스 뒤에 숨겨 추후 풀링 교체가 가능하게 한다 (구현계획 v0.0.2).
    /// 구간 끝의 ChoiceNode에서 전진을 고르면 다음 구간을 이어 붙이고
    /// 뒤쪽 구간을 제거한다 (동시 생존 최대 2개).
    ///
    /// 요소 4분리 (M4-1): 본 파일 = 코어 조율(수명 주기/공유 상태).
    /// 길 = SegmentSpawner.Path.cs (바닥/사전 배치/단차/부착),
    /// 기능 = SegmentSpawner.Features.cs (루트/폭탄/트랩/선택지/신호),
    /// 장식 = SegmentEnvironment.cs (배경 블록 데이터),
    /// 길 공용 지오메트리 = SegmentPath.cs (보행 바닥 스트립),
    /// 라이팅·이펙트 = DepthLighting.cs / DangerGrid.cs / EnvironmentRenderer.cs.
    /// </summary>
    public sealed partial class SegmentSpawner : MonoBehaviour
    {
        public SegmentDefinition Definition { get; private set; }
        public DepthCurve Curve { get; private set; }

        sealed class SegmentRecord
        {
            public GameObject Root;
            public float EndZ;
            public int EnvChunkId;
        }

        readonly List<SegmentRecord> aliveSegments = new List<SegmentRecord>();
        List<LootDefinition> lootCatalog;
        EnvironmentAuthoring preplacedEnvironment;
        bool preplacedSearched;
        EnvironmentRenderer environmentRenderer;
        FollowCamera viewCamera;

        /// <summary>지금까지 생성된 구간 체인의 끝 z. 룩어헤드 생성 기준점.</summary>
        public float TailEndZ { get; private set; }

        public void Configure(SegmentDefinition definition, List<LootDefinition> catalog, DepthCurve curve)
        {
            Definition = definition;
            lootCatalog = catalog;
            Curve = curve;
        }

        /// <summary>시야 클리어런스 기준 카메라. GameFlow가 배선.</summary>
        public void SetViewCamera(FollowCamera camera)
        {
            viewCamera = camera;
        }

        CollapseFront EnsureCollapseFront()
        {
            CollapseFront collapse = GetComponent<CollapseFront>();

            if (collapse == null)
                collapse = gameObject.AddComponent<CollapseFront>();

            return collapse;
        }

        /// <summary>
        /// 카메라 리그 수치로 시야 라인 클리어런스를 만든다.
        /// 먼 끝점은 복도 반대편 바닥 (플레이어가 -x 끝에 있어도 가리지 않게 보수적).
        /// </summary>
        public static SightClearance BuildSightClearance(FollowCamera camera, SegmentDefinition definition)
        {
            if (camera == null || definition == null)
                return default;

            Vector2 cameraPoint = new Vector2(
                camera.lookAtOffset.x + camera.positionOffsetWorld.x,
                camera.lookAtOffset.y + camera.positionOffsetWorld.y);

            Vector2 farPoint = new Vector2(-definition.corridorHalfWidth, 0f);

            return new SightClearance
            {
                Enabled = true,
                NearPoint = cameraPoint,
                FarPoint = farPoint,
                Margin = 1f,
            };
        }

        /// <summary>startZ부터 시작하는 구간 하나를 만들고 루트를 돌려준다.</summary>
        public GameObject BuildSegment(int depth, float startZ)
        {
            if (Definition == null)
                Definition = SegmentDefinition.CreateDefault();

            if (Curve == null)
                Curve = DepthCurve.CreateDefault();

            GameObject root = new GameObject($"Segment_depth{depth}");
            root.transform.SetParent(transform);
            root.transform.position = new Vector3(0f, 0f, startZ);

            int envChunkId = BuildShell(root.transform);
            PopulateLedges(root.transform, depth);
            PopulateLoot(root.transform, depth);
            PopulateBombs(root.transform, depth);
            PopulateSinkTraps(depth);
            PopulatePushTraps(root.transform, depth);
            PopulateRockfalls(root.transform, depth);
            PopulateChargers(root.transform, depth);
            PopulateToppleColumns(root.transform, depth);
            BuildCheckpoint(root.transform);
            BuildChoiceNode(root.transform, depth);
            BuildSignalEmitters(root.transform);

            float endZ = startZ + Definition.lengthMeters;

            aliveSegments.Add(new SegmentRecord
            {
                Root = root,
                EndZ = endZ,
                EnvChunkId = envChunkId,
            });

            TailEndZ = Mathf.Max(TailEndZ, endZ);
            return root;
        }

        /// <summary>
        /// 라운드 시작용 체인: 현재 구간 + 다음 구간을 함께 생성한다.
        /// 다음 스테이지가 항상 시야에 확정 노출되어 끊김이 없다 (ADR-0004).
        /// </summary>
        public void BuildInitialChain(int depth, float startZ)
        {
            BuildSegment(depth, startZ);
            BuildSegment(depth + 1, TailEndZ);
        }

        public void DespawnAll()
        {
            foreach (SegmentRecord segment in aliveSegments)
                DespawnSegment(segment);

            aliveSegments.Clear();
            TailEndZ = 0f;
        }

        /// <summary>endZ가 기준보다 뒤인 구간을 제거한다 (지나간 구간 정리).</summary>
        public void DespawnBehind(float z)
        {
            for (int i = aliveSegments.Count - 1; i >= 0; i--)
            {
                SegmentRecord segment = aliveSegments[i];

                if (segment.EndZ >= z)
                    continue;

                DespawnSegment(segment);
                aliveSegments.RemoveAt(i);
            }
        }

        void DespawnSegment(SegmentRecord segment)
        {
            if (segment.EnvChunkId != 0 && environmentRenderer != null)
                environmentRenderer.RemoveChunk(segment.EnvChunkId);

            if (segment.Root == null)
                return;

            // Destroy는 프레임 끝까지 지연되므로 먼저 비활성화해
            // 이전 런의 LootSpot/ChoiceNode가 같은 프레임에 동작하지 못하게 한다 (Codex 검토 반영)
            segment.Root.SetActive(false);
            Destroy(segment.Root);
        }

        // -- 공유 유틸 -------------------------------------------------------

        static GameObject CreateBlock(Transform parent, string blockName)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = blockName;
            block.transform.SetParent(parent, false);
            return block;
        }

        static void Tint(GameObject block, Color color)
        {
            Renderer blockRenderer = block.GetComponent<Renderer>();

            if (blockRenderer == null)
                return;

            blockRenderer.material.color = color;
        }

        static float Lerp(System.Random rng, float min, float max)
        {
            return Mathf.Lerp(min, max, (float)rng.NextDouble());
        }
    }
}
