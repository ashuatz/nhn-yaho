using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Obstacle
{
    /// <summary>
    /// 굴러오는 블록 스포너 (웹 프로토타입 이식). 플레이어가 근처에 있는 동안
    /// 일정 간격으로 플레이어 정면(진행 방향 앞)에 RollingBlock을 스폰한다.
    /// 웹의 전역 타이머를 구역 기반으로 옮긴 형태 - 배치/밀도는 스포너 패턴과 일관.
    /// 스폰 x는 회피 동선을 좌우하는 게임 결과 - 배치 시 RunManager.Rng에서 시드 배정.
    /// </summary>
    public sealed class RollingBlockSpawner : MonoBehaviour
    {
        [Header("스폰 (SegmentSpawner가 주입)")]
        public float spawnAheadDistance = 20f;
        public float activateDistance = 22f;
        public float speedMin = 6.5f;
        public float speedMax = 9.5f;
        public float damage = 35f;
        public float intervalMinSeconds = 3.5f;
        public float intervalMaxSeconds = 6.5f;

        [Header("스폰 x 산포 반경 (복도 중앙 기준)")]
        public float spawnHalfWidth = 3f;

        /// <summary>스폰 산포 시드. 배치 시 RunManager.Rng에서 배정 (0이면 인스턴스 폴백).</summary>
        public int scatterSeed;

        float interval;
        PlayerController player;
        System.Random rng;

        void Start()
        {
            interval = NextRange(intervalMinSeconds, intervalMaxSeconds);
        }

        void Update()
        {
            if (!IsRunActive())
                return;

            if (!TryResolvePlayer())
                return;

            // 플레이어가 이 구역 근처를 지날 때만 가동
            float distance = Mathf.Abs(player.transform.position.z - transform.position.z);

            if (distance > activateDistance)
                return;

            interval -= Time.deltaTime;

            if (interval > 0f)
                return;

            interval = NextRange(intervalMinSeconds, intervalMaxSeconds);
            SpawnBlock();
        }

        void SpawnBlock()
        {
            Vector3 playerPosition = player.transform.position;

            // 플레이어 정면(진행 방향 앞)에 스폰 - 굴러오며 회피를 요구
            float spawnX = NextRange(-spawnHalfWidth, spawnHalfWidth);
            float spawnZ = playerPosition.z + spawnAheadDistance;

            GameObject blockObject = new GameObject("RollingBlock");
            blockObject.transform.SetParent(transform, true);
            blockObject.transform.position = new Vector3(spawnX, 0f, spawnZ);

            float speed = NextRange(speedMin, speedMax);

            RollingBlock block = blockObject.AddComponent<RollingBlock>();
            block.Initialize(speed, damage, player);
        }

        bool TryResolvePlayer()
        {
            if (player != null)
                return true;

            player = FindFirstObjectByType<PlayerController>();
            return player != null;
        }

        static bool IsRunActive()
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return false;

            return run.StateMachine.Current == RunState.Running;
        }

        // 지연 초기화: 스포너의 시드 주입 후 첫 사용 시점에 생성 (RockfallZone과 동일)
        float NextRange(float min, float max)
        {
            if (rng == null)
                rng = new System.Random(scatterSeed != 0 ? scatterSeed : GetInstanceID());

            return Mathf.Lerp(min, max, (float)rng.NextDouble());
        }
    }
}
