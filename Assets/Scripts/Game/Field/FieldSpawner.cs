using System.Collections.Generic;
using Scavenger.Loot;
using Scavenger.Player;
using Scavenger.Run;
using Scavenger.Segment;
using UnityEngine;

namespace Scavenger.Field
{
    /// <summary>
    /// 필드(스테이지) 생성/제거의 단일 경계 (필드 규칙 문서 3장).
    /// 스테이지 진입 시 존 개수를 확정하고, 플레이어 앞의 존을 절차적으로 미리 만들며
    /// 동시 생존 존을 최대 3개(직전/현재/다음)로 유지한다.
    /// 뒤쪽 바닥 제거는 플레이어 위치에서 정해진 칸 수만큼 뒤에 기준선을 두고,
    /// 기준선을 지난 행을 정상 -> 제거 예정(흔들림) -> 제거로 넘긴다.
    ///
    /// 요소 분리: 본 파일 = 수명 주기/존 체인/제거 기준선,
    /// 배치(드랍 아이템/탈출 지점) = FieldSpawner.Drops.cs,
    /// 장식(배경 블록) = Segment/SegmentEnvironment + EnvironmentRenderer.
    /// </summary>
    public sealed partial class FieldSpawner : MonoBehaviour
    {
        /// <summary>씬 단일 인스턴스. 카메라 쉐이크/HUD가 압박 상태를 조회한다.</summary>
        public static FieldSpawner Instance { get; private set; }

        public ZoneDefinition Definition { get; private set; }

        /// <summary>이 스테이지의 존 개수 (진입 시 확정).</summary>
        public int StageZoneCount { get; private set; }

        /// <summary>제거 기준선 z (월드). 이 뒤의 바닥은 제거 대상.</summary>
        public float RemoveLineZ { get; private set; } = float.NegativeInfinity;

        /// <summary>현재 플레이어가 있는 존 인덱스 (없으면 -1).</summary>
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

        /// <summary>스테이지 전체 진행도 0..1 (HUD 표시용).</summary>
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

        sealed class ZoneRecord
        {
            public Zone Zone;
            public int EnvChunkId;
        }

        readonly List<ZoneRecord> aliveZones = new List<ZoneRecord>();
        List<LootDefinition> lootCatalog;
        EnvironmentRenderer environmentRenderer;
        FollowCamera viewCamera;
        PlayerController trackedPlayer;

        // 스테이지 인계 시 플레이어 뒤로 확보하는 바닥 여유 (m).
        // 웨이포인트 트리거 깊이(z 두께 2m)와 캐릭터 반경을 덮는 값
        const float StageHandoffFloorMargin = 3f;

        float stageStartZ;
        int builtZoneCount;

        void OnEnable()
        {
            Instance = this;
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
        /// 스테이지 시작. startZ부터 존을 이어 붙이고 존 개수를 확정한다.
        /// 기존 존은 모두 제거된다 (런 재시작 / 다음 스테이지 진입 공용).
        /// </summary>
        public void StartStage(float startZ)
        {
            if (Definition == null)
                Definition = ZoneDefinition.CreateDefault();

            // 인계 가드: 새 스테이지는 반드시 플레이어보다 뒤에서 시작한다.
            // 기존 존을 즉시 걷어내므로, 플레이어가 새 존 0의 첫 행보다 앞에 있지
            // 않으면 발밑이 비어 낙사한다 (탈출/진행 지점 통과 시 실제 발생)
            if (trackedPlayer != null)
            {
                float guarded = trackedPlayer.transform.position.z - StageHandoffFloorMargin;
                startZ = Mathf.Min(startZ, guarded);
            }

            DespawnAll();

            RunManager run = RunManager.Instance;
            System.Random rng = run != null && run.Rng != null ? run.Rng : new System.Random(0);

            StageZoneCount = Definition.RollZoneCount(rng);
            stageStartZ = startZ;
            builtZoneCount = 0;
            CurrentZoneIndex = -1;

            // 제거 기준선은 스테이지 시작점 뒤에서 출발 - 첫 프레임에 발밑이 무너지지 않게
            RemoveLineZ = startZ - Definition.FloorRemoveStartDistance;

            // 진입 시점에 현재 + 다음 존을 함께 노출 (전방이 항상 보이도록)
            BuildZone(0);
            BuildZone(1);

            UnityEngine.Debug.Log($"[Field] Stage started. zones={StageZoneCount} startZ={startZ:F1}");
        }

        void Update()
        {
            if (Definition == null || trackedPlayer == null)
                return;

            RunManager run = RunManager.Instance;

            if (run == null || run.StateMachine.Current != RunState.Running)
                return;

            UpdateRemoveLine();
            UpdateZoneChain();
            UpdateZoneRemoval();
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

        // 플레이어가 다음 존에 들어서면 그 앞 존을 미리 만들고, 두 칸 뒤 존은 정리한다
        void UpdateZoneChain()
        {
            float playerZ = trackedPlayer.transform.position.z;
            int zoneIndex = Mathf.FloorToInt((playerZ - stageStartZ) / Definition.lengthMeters);

            zoneIndex = Mathf.Clamp(zoneIndex, 0, Mathf.Max(0, StageZoneCount - 1));

            if (zoneIndex == CurrentZoneIndex)
                return;

            CurrentZoneIndex = zoneIndex;

            // 다음 존 미리 생성 (스테이지 존 개수 한도까지)
            BuildZone(zoneIndex + 1);

            // 동시 생존 최대 3개 (직전/현재/다음) - 그보다 뒤는 즉시 정리
            DespawnZonesBefore(zoneIndex - 1);
        }

        void UpdateZoneRemoval()
        {
            for (int i = aliveZones.Count - 1; i >= 0; i--)
            {
                ZoneRecord record = aliveZones[i];

                if (record.Zone == null)
                {
                    ReleaseZoneRecord(record);
                    aliveZones.RemoveAt(i);
                    continue;
                }

                record.Zone.UpdateRemoval(RemoveLineZ, Definition.shakeSeconds);

                // 바닥이 전부 사라진 존은 오브젝트만 남으므로 정리한다
                if (!record.Zone.IsFullyRemoved)
                    continue;

                ReleaseZoneRecord(record);
                record.Zone.DestroyImmediateAll();
                aliveZones.RemoveAt(i);
            }
        }

        void BuildZone(int zoneIndex)
        {
            if (zoneIndex < 0 || zoneIndex >= StageZoneCount)
                return;

            if (zoneIndex < builtZoneCount)
                return;

            // 체인은 순서대로만 자란다 - 중간이 빈 채로 앞부터 만들지 않는다
            for (int index = builtZoneCount; index <= zoneIndex; index++)
                BuildZoneAt(index);
        }

        void BuildZoneAt(int zoneIndex)
        {
            float startZ = stageStartZ + zoneIndex * Definition.lengthMeters;
            bool isLast = zoneIndex == StageZoneCount - 1;

            GameObject zoneObject = new GameObject($"Zone_{zoneIndex:D2}");
            zoneObject.transform.SetParent(transform);
            zoneObject.transform.position = Vector3.zero;

            Zone zone = zoneObject.AddComponent<Zone>();
            zone.Build(Definition, zoneIndex, startZ, isLast);

            PopulateDrops(zone);
            PopulateFarmingPoints(zone);

            if (isLast)
                BuildStageExit(zone);

            int envChunkId = BuildBackground(startZ);

            aliveZones.Add(new ZoneRecord
            {
                Zone = zone,
                EnvChunkId = envChunkId,
            });

            builtZoneCount = Mathf.Max(builtZoneCount, zoneIndex + 1);
        }

        // 배경 블록은 인스턴스 렌더링 (ADR-0005) - 존 시작 z를 원점으로 등록
        int BuildBackground(float startZ)
        {
            if (environmentRenderer == null)
            {
                environmentRenderer = GetComponent<EnvironmentRenderer>();

                if (environmentRenderer == null)
                    environmentRenderer = gameObject.AddComponent<EnvironmentRenderer>();
            }

            RunManager run = RunManager.Instance;
            System.Random rng = run != null && run.Rng != null ? run.Rng : new System.Random(0);

            SightClearance clearance = BuildSightClearance(viewCamera, Definition);
            List<EnvironmentBlock> blocks = SegmentEnvironment.GenerateBlocks(Definition, rng, clearance);

            return environmentRenderer.AddChunk(blocks, new Vector3(0f, 0f, startZ));
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

        public void DespawnAll()
        {
            foreach (ZoneRecord record in aliveZones)
            {
                ReleaseZoneRecord(record);

                if (record.Zone != null)
                    record.Zone.DestroyImmediateAll();
            }

            aliveZones.Clear();
            builtZoneCount = 0;
        }

        void DespawnZonesBefore(int keepFromIndex)
        {
            for (int i = aliveZones.Count - 1; i >= 0; i--)
            {
                ZoneRecord record = aliveZones[i];

                if (record.Zone == null)
                {
                    ReleaseZoneRecord(record);
                    aliveZones.RemoveAt(i);
                    continue;
                }

                if (record.Zone.ZoneIndex >= keepFromIndex)
                    continue;

                ReleaseZoneRecord(record);
                record.Zone.DestroyImmediateAll();
                aliveZones.RemoveAt(i);
            }
        }

        void ReleaseZoneRecord(ZoneRecord record)
        {
            if (record.EnvChunkId == 0 || environmentRenderer == null)
                return;

            environmentRenderer.RemoveChunk(record.EnvChunkId);
            record.EnvChunkId = 0;
        }
    }
}
