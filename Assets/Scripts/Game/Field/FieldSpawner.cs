using System.Collections.Generic;
using Scavenger.Loot;
using Scavenger.Player;
using Scavenger.Run;
using Scavenger.Segment;
using UnityEngine;

namespace Scavenger.Field
{
    /// <summary>필드 세그먼트 종류. 구간에는 웨이포인트만 놓인다.</summary>
    public enum FieldSegmentKind
    {
        /// <summary>존 - 드랍/파밍 포인트가 배치되는 본체 (25 x 7).</summary>
        Zone,

        /// <summary>존과 존 사이 구간 - 탈출 웨이포인트만 놓이는 짧은 세그먼트.</summary>
        Junction,
    }

    /// <summary>
    /// 필드(스테이지) 생성/제거의 단일 경계 (필드 규칙 문서 3장).
    /// 플레이어 앞으로 세그먼트를 계속 이어 붙인다 - 진행은 끊기지 않는다.
    ///
    /// 세그먼트 구성: 존을 스테이지 존 개수만큼 이어 붙이고, 그 다음에
    /// 존과 존 사이 구간(Junction)을 끼운다. 구간에는 탈출 웨이포인트가 놓이며,
    /// 밟지 않고 걸어서 통과하면 그것이 곧 다음 스테이지 진입(깊이 +1)이다.
    /// 스테이지 인계에 월드 재생성은 없다 - 앞쪽 존이 계속 생성된다 (사용자 지시).
    ///
    /// 전방 패딩: 플레이어 앞으로 zoneLookAheadCount(기본 2) 존 거리만큼 미리
    /// 만들어 둔다 - 존이 생겨나는 장면이 화면에 잘려 보이지 않게 하기 위함.
    ///
    /// 뒤쪽 바닥 제거는 플레이어 위치에서 정해진 칸 수만큼 뒤에 기준선을 두고,
    /// 기준선을 지난 행을 정상 -> 제거 예정(흔들림) -> 제거로 넘긴다.
    ///
    /// 요소 분리: 본 파일 = 수명 주기/세그먼트 체인/제거 기준선,
    /// 배치(드랍 아이템/탈출 지점) = FieldSpawner.Drops.cs,
    /// 파밍 포인트 = FieldSpawner.FarmingPoints.cs,
    /// 장식(배경 블록) = Segment/SegmentEnvironment + EnvironmentRenderer.
    /// </summary>
    public sealed partial class FieldSpawner : MonoBehaviour
    {
        /// <summary>씬 단일 인스턴스. 카메라 쉐이크/HUD가 압박 상태를 조회한다.</summary>
        public static FieldSpawner Instance { get; private set; }

        [Header("그레이박스 리소스 (아트 교체 지점 - 프리팹에서 배선)")]
        [SerializeField] Material greyboxMaterial;
        [SerializeField] FieldTileSet floorTileSet;
        [SerializeField] FloorRow floorRowPrefab;
        [SerializeField] FarmingPoint farmingPointTopPrefab;
        [SerializeField] FarmingPoint farmingPointBottomPrefab;

        [Header("배경 블록 인스턴싱 (GPU 인스턴싱). 파밍 포인트 영역은 자동 제외된다")]
        public bool buildBackgroundBlocks = true;

        public ZoneDefinition Definition { get; private set; }

        /// <summary>플레이어가 있는 스테이지의 존 개수.</summary>
        public int StageZoneCount { get; private set; }

        /// <summary>플레이어가 있는 스테이지 순번 (0부터. 구간 통과 시 +1).</summary>
        public int StageIndex { get; private set; }

        /// <summary>제거 기준선 z (월드). 이 뒤의 바닥은 제거 대상.</summary>
        public float RemoveLineZ { get; private set; } = float.NegativeInfinity;

        /// <summary>플레이어가 있는 존 인덱스 (스테이지 내 순번. 구간이면 -1).</summary>
        public int CurrentZoneIndex { get; private set; } = -1;

        /// <summary>플레이어와 제거 기준선의 거리 (m). 근접 피드백 판정용.</summary>
        public float RemoveLineDistanceToPlayer
        {
            get
            {
                if (trackedPlayer == null || float.IsNegativeInfinity(RemoveLineZ))
                    return float.PositiveInfinity;

                return trackedPlayer.transform.position.z - RemoveLineZ;
            }
        }

        /// <summary>현재 스테이지 진행도 0..1 (HUD 표시용).</summary>
        public float StageProgress01
        {
            get
            {
                if (Definition == null || StageZoneCount <= 0 || trackedPlayer == null)
                    return 0f;

                float total = StageZoneCount * Definition.lengthMeters;

                if (total <= 0f)
                    return 0f;

                return Mathf.Clamp01((trackedPlayer.transform.position.z - stageStartZ) / total);
            }
        }

        /// <summary>
        /// 살아 있는 세그먼트 하나의 기록. 세그먼트 길이가 서로 다르므로
        /// 인덱스 산술로는 위치를 되짚을 수 없다 - 시작/끝 z를 직접 보유한다.
        /// </summary>
        sealed class FieldSegment
        {
            public FieldSegmentKind Kind;
            public Zone Zone;
            public int EnvChunkId;

            public int StageIndex;
            public int StageZoneCount;

            /// <summary>스테이지 내 존 순번. 구간이면 -1.</summary>
            public int ZoneIndexInStage;

            /// <summary>소속 스테이지의 첫 존 시작 z (진행도 기준점).</summary>
            public float StageStartZ;

            public float StartZ;
            public float EndZ;
        }

        readonly List<FieldSegment> aliveSegments = new List<FieldSegment>();
        List<LootDefinition> lootCatalog;
        EnvironmentRenderer environmentRenderer;
        FollowCamera viewCamera;
        PlayerController trackedPlayer;

        // 런 시작 시 플레이어 뒤로 확보하는 바닥 여유 (m).
        // 캐릭터 반경과 첫 행 두께를 덮는 값
        const float StageHandoffFloorMargin = 3f;

        // 한 번에 만드는 세그먼트 상한. 수치 오류로 프론티어가 전진하지 못할 때
        // 프레임이 멈추는 것을 막는 안전장치
        const int MaxSegmentsPerTick = 8;

        // 플레이어가 있는 스테이지의 시작 z (진행도 계산 기준)
        float stageStartZ;

        // 다음 세그먼트가 시작될 월드 z (생성 프론티어)
        float frontierZ;

        // 생성 중인 스테이지 상태. 전방 패딩 때문에 플레이어가 있는 스테이지보다
        // 앞서 나갈 수 있으므로 표시용 값(StageIndex 등)과 분리해 보유한다
        int buildStageIndex;
        int buildStageZoneCount;
        int buildStageZoneIndex;
        float buildStageStartZ;

        int nextSegmentIndex;

        // 바닥 타일 노이즈 오프셋. 스테이지마다 런 시드에서 1회 뽑는다 -
        // 같은 시드면 같은 바닥 패턴이 재현되고, 스테이지마다는 달라진다
        Vector2 tileNoiseOrigin;

        void OnEnable()
        {
            Instance = this;

            // 런타임 생성물의 기준 머티리얼 (Assets/Materials/Greybox/Common.mat)
            GreyboxPalette.SetBaseMaterial(greyboxMaterial);

            ValidateRootTransform();
        }

        /// <summary>
        /// 필드 루트는 원점 + 무회전 + 스케일 1이어야 한다.
        /// 세그먼트 지형은 월드 좌표와 localScale로 배치하므로, 루트가 움직이면
        /// 만들어 둔 바닥 전체가 함께 끌려가고(실제 발생: 루트가 z -26.9로 밀려 있었다),
        /// 스케일이 1이 아니면 콜라이더 치수가 배로 어긋난다 (Codex 검토 지적).
        /// </summary>
        void ValidateRootTransform()
        {
            bool offsetOk = transform.position == Vector3.zero;
            bool rotationOk = transform.rotation == Quaternion.identity;
            bool scaleOk = IsUnitScale(transform.lossyScale);

            if (offsetOk && rotationOk && scaleOk)
                return;

            UnityEngine.Debug.LogWarning(
                $"[Field] 필드 루트 트랜스폼이 기본값이 아니다 (pos={transform.position} " +
                $"rot={transform.eulerAngles} scale={transform.lossyScale}). " +
                "생성된 바닥이 함께 끌려가므로 원점/무회전/스케일 1로 되돌린다. " +
                "부모 오브젝트가 원인이면 그쪽을 맞출 것.");

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        static bool IsUnitScale(Vector3 scale)
        {
            if (!Mathf.Approximately(scale.x, 1f))
                return false;

            if (!Mathf.Approximately(scale.y, 1f))
                return false;

            if (!Mathf.Approximately(scale.z, 1f))
                return false;

            return true;
        }

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Configure(ZoneDefinition definition, List<LootDefinition> catalog)
        {
            Definition = definition;
            lootCatalog = catalog;
        }

        /// <summary>배경 시야 클리어런스 기준 카메라. GameFlow가 배선.</summary>
        public void SetViewCamera(FollowCamera camera)
        {
            viewCamera = camera;
        }

        /// <summary>제거 기준선 계산 대상 플레이어. GameFlow가 배선.</summary>
        public void Track(PlayerController player)
        {
            trackedPlayer = player;
        }

        /// <summary>
        /// 필드 시작 (런 시작 전용). startZ부터 세그먼트를 이어 붙이기 시작한다.
        /// 기존 세그먼트는 모두 제거된다 - 스테이지 인계는 여기를 거치지 않는다
        /// (구간을 걸어서 통과하는 것이 곧 다음 스테이지 진입).
        /// </summary>
        public void StartStage(float startZ)
        {
            if (Definition == null)
                Definition = ZoneDefinition.CreateDefault();

            // 인계 가드: 필드는 반드시 플레이어보다 뒤에서 시작한다.
            // 기존 세그먼트를 즉시 걷어내므로, 플레이어가 첫 행보다 앞에 있지
            // 않으면 발밑이 비어 낙사한다
            if (trackedPlayer != null)
            {
                float guarded = trackedPlayer.transform.position.z - StageHandoffFloorMargin;
                startZ = Mathf.Min(startZ, guarded);
            }

            DespawnAll();

            nextSegmentIndex = 0;
            frontierZ = startZ;

            buildStageIndex = 0;
            buildStageStartZ = startZ;
            buildStageZoneIndex = 0;

            System.Random rng = ResolveRng();

            buildStageZoneCount = Definition.RollZoneCount(rng);
            tileNoiseOrigin = new Vector2(rng.Next(0, 10000), rng.Next(0, 10000));

            StageIndex = 0;
            StageZoneCount = buildStageZoneCount;
            CurrentZoneIndex = -1;
            stageStartZ = startZ;

            // 제거 기준선은 시작점 뒤에서 출발 - 첫 프레임에 발밑이 무너지지 않게
            RemoveLineZ = startZ - Definition.FloorRemoveStartDistance;

            // 시작 시점에 전방 패딩까지 채운다 (생성 장면이 화면에 보이지 않게)
            EnsureSegmentsAhead(startZ);

            UnityEngine.Debug.Log(
                $"[Field] Field started. zones={StageZoneCount} startZ={startZ:F1} " +
                $"lookAhead={Definition.LookAheadDistance:F1}m");
        }

        void Update()
        {
            if (Definition == null || trackedPlayer == null)
                return;

            RunManager run = RunManager.Instance;

            if (run == null || run.StateMachine.Current != RunState.Running)
                return;

            UpdateRemoveLine();
            EnsureSegmentsAhead(trackedPlayer.transform.position.z);
            UpdatePlayerSegment();
            UpdateSegmentRemoval();
        }

        // 제거 기준선 = 플레이어 뒤 정해진 칸 수. 전진 전용 래칫 -
        // 플레이어가 되돌아가도 이미 사라진 바닥이 되살아나지는 않는다
        void UpdateRemoveLine()
        {
            float target = trackedPlayer.transform.position.z - Definition.FloorRemoveStartDistance;

            RemoveLineZ = Mathf.Max(RemoveLineZ, target);

            // 되돌아갈 수 없음 (문서 3.2 (1)) - 기준선보다 뒤로 이동 불가.
            // 사라진 바닥으로 걸어 들어가 억울하게 낙사하는 것도 함께 막는다
            trackedPlayer.Motor.MinZ = RemoveLineZ;
        }

        // -- 세그먼트 체인 -----------------------------------------------------

        /// <summary>
        /// 기준 z 앞으로 패딩 거리만큼 세그먼트를 확보한다.
        /// 존이 화면 안에서 생겨나는 것이 보이지 않게 하는 유일한 장치이므로
        /// 패딩(zoneLookAheadCount)은 화면에 들어오는 거리보다 커야 한다.
        /// </summary>
        void EnsureSegmentsAhead(float referenceZ)
        {
            float target = referenceZ + Definition.LookAheadDistance;

            for (int guard = 0; guard < MaxSegmentsPerTick; guard++)
            {
                if (frontierZ >= target)
                    return;

                BuildNextSegment();
            }
        }

        // 스테이지 존 개수를 다 만들면 다음은 구간이고, 구간 뒤부터 새 스테이지가
        // 시작된다 - 필드 생성은 어디서도 멈추지 않는다 (무한 진행)
        void BuildNextSegment()
        {
            if (buildStageZoneIndex >= buildStageZoneCount)
            {
                BuildSegment(FieldSegmentKind.Junction, Definition.junctionLengthBlocks);
                BeginNextStage();
                return;
            }

            BuildSegment(FieldSegmentKind.Zone, Definition.zoneLengthBlocks);
        }

        // 구간 뒤에서 새 스테이지를 연다. 존 개수와 바닥 노이즈를 다시 뽑는다
        void BeginNextStage()
        {
            System.Random rng = ResolveRng();

            buildStageIndex += 1;
            buildStageStartZ = frontierZ;
            buildStageZoneIndex = 0;
            buildStageZoneCount = Definition.RollZoneCount(rng);

            tileNoiseOrigin = new Vector2(rng.Next(0, 10000), rng.Next(0, 10000));
        }

        void BuildSegment(FieldSegmentKind kind, int lengthBlocks)
        {
            bool isJunction = kind == FieldSegmentKind.Junction;
            float startZ = frontierZ;

            GameObject zoneObject = new GameObject(ResolveSegmentName(kind, nextSegmentIndex));
            zoneObject.transform.SetParent(transform);
            zoneObject.transform.position = Vector3.zero;

            Zone zone = zoneObject.AddComponent<Zone>();

            zone.Build(
                new Zone.BuildContext
                {
                    Definition = Definition,
                    TileSet = floorTileSet,
                    RowPrefab = floorRowPrefab,
                    NoiseOrigin = tileNoiseOrigin,
                    LengthBlocks = lengthBlocks,
                },
                nextSegmentIndex, startZ, isJunction);

            PopulateSegment(zone, isJunction);

            int envChunkId = BuildBackground(startZ, zone.EndZ - zone.StartZ);

            aliveSegments.Add(new FieldSegment
            {
                Kind = kind,
                Zone = zone,
                EnvChunkId = envChunkId,
                StageIndex = buildStageIndex,
                StageZoneCount = buildStageZoneCount,
                ZoneIndexInStage = isJunction ? -1 : buildStageZoneIndex,
                StageStartZ = buildStageStartZ,
                StartZ = zone.StartZ,
                EndZ = zone.EndZ,
            });

            if (!isJunction)
                buildStageZoneIndex += 1;

            nextSegmentIndex += 1;
            frontierZ = zone.EndZ;
        }

        // 생성물에 HideFlags.DontSaveInEditor를 붙이지 말 것.
        // 씬 저장은 막아주지만 Unity가 FindObjectsByType에서 그 오브젝트를 제외하므로,
        // 생성물을 찾는 코드가 조용히 0개를 받는다 (검증 중 실제로 겪었다).
        // 씬에 저장된 잔존물은 DespawnAll의 DestroyLeftoverSegments가 걷어낸다

        // 구간에는 드랍/파밍 포인트를 두지 않는다 - 정산 지점을 읽기 쉽게 비운다
        void PopulateSegment(Zone zone, bool isJunction)
        {
            if (isJunction)
            {
                // 배경 필터가 이전 존의 영역 기록을 보고 엉뚱한 블록을 지우지 않게
                // 여기서도 비운다 (파밍 포인트를 배치하지 않는 세그먼트)
                currentFootprints.Clear();

                BuildJunctionWaypoint(zone);
                return;
            }

            PopulateDrops(zone);
            PopulateFarmingPoints(zone);
        }

        static string ResolveSegmentName(FieldSegmentKind kind, int segmentIndex)
        {
            if (kind == FieldSegmentKind.Junction)
                return $"Junction_{segmentIndex:D2}";

            return $"Zone_{segmentIndex:D2}";
        }

        // 플레이어가 있는 세그먼트를 갱신한다. 구간을 지나 다음 스테이지 존에
        // 들어서면 그것이 곧 진행(Advance) - 밟아야 하는 포탈은 없다
        void UpdatePlayerSegment()
        {
            FieldSegment segment = FindSegmentAt(trackedPlayer.transform.position.z);

            if (segment == null)
                return;

            // 스테이지 지표는 전진 전용 래칫. 후퇴가 몇 칸 허용되므로(제거 기준선까지)
            // 구간 경계에서 앞뒤로 걸으면 깊이가 여러 번 올라간다 - 실제로 발생하는 경로
            if (segment.StageIndex < StageIndex)
                return;

            if (segment.StageIndex > StageIndex)
                AdvanceToStage(segment.StageIndex);

            StageIndex = segment.StageIndex;
            StageZoneCount = segment.StageZoneCount;
            CurrentZoneIndex = segment.ZoneIndexInStage;
            stageStartZ = segment.StageStartZ;
        }

        void AdvanceToStage(int stageIndex)
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return;

            // 스테이지를 건너뛰었더라도 깊이는 한 칸씩 올린다 (깊이 스케일링 누락 방지)
            for (int stage = StageIndex; stage < stageIndex; stage++)
                run.AdvanceDepth();
        }

        FieldSegment FindSegmentAt(float z)
        {
            foreach (FieldSegment segment in aliveSegments)
            {
                if (z < segment.StartZ || z >= segment.EndZ)
                    continue;

                return segment;
            }

            return null;
        }

        void UpdateSegmentRemoval()
        {
            // 기준선보다 한 존 이상 뒤로 밀려난 세그먼트는 통째로 정리한다.
            // 행은 스스로 낙하해 사라지므로 여유를 두고 뒤에서 걷어내기만 한다
            float purgeZ = RemoveLineZ - Definition.lengthMeters;

            for (int i = aliveSegments.Count - 1; i >= 0; i--)
            {
                FieldSegment segment = aliveSegments[i];

                if (segment.Zone == null)
                {
                    ReleaseSegment(segment);
                    aliveSegments.RemoveAt(i);
                    continue;
                }

                segment.Zone.UpdateRemoval(RemoveLineZ, Definition.shakeSeconds);

                // 바닥이 전부 사라졌거나 기준선 뒤로 완전히 밀려난 세그먼트를 정리
                if (!segment.Zone.IsFullyRemoved && segment.EndZ > purgeZ)
                    continue;

                ReleaseSegment(segment);
                segment.Zone.DestroyImmediateAll();
                aliveSegments.RemoveAt(i);
            }
        }

        System.Random ResolveRng()
        {
            RunManager run = RunManager.Instance;

            if (run != null && run.Rng != null)
                return run.Rng;

            return new System.Random(0);
        }

        // -- 배경 -------------------------------------------------------------

        // 배경 블록은 인스턴스 렌더링 (ADR-0005) - 세그먼트 시작 z를 원점으로 등록.
        // 세그먼트 길이를 함께 넘긴다 - 구간은 존보다 짧아 존 길이로 만들면
        // 다음 세그먼트 배경과 겹친다
        int BuildBackground(float startZ, float segmentLength)
        {
            if (!buildBackgroundBlocks)
                return 0;

            if (environmentRenderer == null)
            {
                environmentRenderer = GetComponent<EnvironmentRenderer>();

                if (environmentRenderer == null)
                    environmentRenderer = gameObject.AddComponent<EnvironmentRenderer>();
            }

            System.Random rng = ResolveRng();

            SightClearance clearance = BuildSightClearance(viewCamera, Definition);
            List<EnvironmentBlock> blocks = SegmentEnvironment.GenerateBlocks(
                Definition, rng, clearance, segmentLength);

            // 파밍 포인트가 차지한 영역의 블록은 버린다 - 안 버리면 럽블이 플랫폼을
            // 관통해 솟는다. 생성 자체를 막지 않고 사후 필터인 이유는 rng 소비 순서를
            // 건드리지 않기 위함 (같은 시드 = 같은 배치 유지)
            RemoveBlocksInFootprints(blocks, startZ);

            return environmentRenderer.AddChunk(blocks, new Vector3(0f, 0f, startZ));
        }

        void RemoveBlocksInFootprints(List<EnvironmentBlock> blocks, float startZ)
        {
            if (currentFootprints.Count == 0)
                return;

            for (int i = blocks.Count - 1; i >= 0; i--)
            {
                Vector3 localPosition = blocks[i].LocalMatrix.GetColumn(3);
                Vector3 scale = blocks[i].LocalMatrix.lossyScale;

                float minX = localPosition.x - scale.x * 0.5f;
                float maxX = localPosition.x + scale.x * 0.5f;

                // 블록 z는 청크 로컬 - 파밍 포인트 영역은 월드라 startZ를 더해 맞춘다
                float worldZ = localPosition.z + startZ;
                float minZ = worldZ - scale.z * 0.5f;
                float maxZ = worldZ + scale.z * 0.5f;

                if (!IsInsideAnyFootprint(minX, maxX, minZ, maxZ))
                    continue;

                blocks.RemoveAt(i);
            }
        }

        bool IsInsideAnyFootprint(float minX, float maxX, float minZ, float maxZ)
        {
            foreach (Footprint footprint in currentFootprints)
            {
                if (footprint.Overlaps(minX, maxX, minZ, maxZ))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 카메라 리그 수치로 시야 라인 클리어런스를 만든다 (카메라측 가림 방지).
        /// 먼 끝점은 존 반대편 바닥 - 플레이어가 끝에 있어도 가리지 않게 보수적.
        /// </summary>
        public static SightClearance BuildSightClearance(FollowCamera camera, ZoneDefinition definition)
        {
            if (camera == null || definition == null)
                return default;

            Vector2 cameraPoint = camera.CameraOffsetXY();
            Vector2 farPoint = new Vector2(-definition.corridorHalfWidth, 0f);

            return new SightClearance
            {
                Enabled = true,
                NearPoint = cameraPoint,
                FarPoint = farPoint,
                Margin = 1f,
            };
        }

        // -- 정리 -------------------------------------------------------------

        public void DespawnAll()
        {
            foreach (FieldSegment segment in aliveSegments)
            {
                ReleaseSegment(segment);

                if (segment.Zone != null)
                    segment.Zone.DestroyImmediateAll();
            }

            aliveSegments.Clear();

            DestroyLeftoverSegments();
        }

        /// <summary>
        /// 기록에 없는 세그먼트까지 걷어낸다. 런타임 생성물이 씬에 저장된 채로 열리면
        /// (실제 발생) 새로 만든 바닥과 두 겹으로 겹쳐 보이고, 리스트 기반 정리로는
        /// 지워지지 않는다. 생성 경계가 이 컴포넌트이므로 여기서 책임진다.
        /// </summary>
        void DestroyLeftoverSegments()
        {
            Zone[] leftovers = GetComponentsInChildren<Zone>(true);

            if (leftovers.Length == 0)
                return;

            UnityEngine.Debug.LogWarning(
                $"[Field] 씬에 남아 있던 세그먼트 {leftovers.Length}개를 제거한다. " +
                "런타임 생성물이 씬에 저장된 상태였다 (플레이 중 씬 저장 여부 확인 필요).");

            foreach (Zone zone in leftovers)
            {
                if (zone != null)
                    zone.DestroyImmediateAll();
            }
        }

        void ReleaseSegment(FieldSegment segment)
        {
            if (segment.EnvChunkId == 0 || environmentRenderer == null)
                return;

            environmentRenderer.RemoveChunk(segment.EnvChunkId);
            segment.EnvChunkId = 0;
        }
    }
}
