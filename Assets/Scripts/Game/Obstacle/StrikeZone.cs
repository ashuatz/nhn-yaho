using System.Collections.Generic;
using Scavenger.Player;
using Scavenger.Run;
using Scavenger.Segment;
using UnityEngine;

namespace Scavenger.Obstacle
{
    /// <summary>
    /// 미사일 폭격 구역 (웹 프로토타입 이식). 플레이어가 구역 근처에 있는 동안
    /// 반복 발사: 셀 몇 개를 예고(DangerGrid 점멸)한 뒤 폭발해 HP 피해를 준다.
    /// 낙하물(RockfallZone)과 달리 1회성이 아니라 재장전하며 반복 - 구역을 빨리
    /// 통과하도록 압박한다. 예고 시간이 있어 회피 가능 (즉사 저격 금지 규약).
    /// 셀 선택 산포는 게임 결과(사망) - 배치 시 RunManager.Rng에서 시드 배정.
    /// </summary>
    public sealed class StrikeZone : MonoBehaviour
    {
        [Header("구역 (SegmentSpawner가 주입)")]
        public float halfWidthX = 2.5f;
        public float lengthZ = 4f;

        [Header("발사 (SegmentSpawner가 주입)")]
        public float telegraphSeconds = 1.35f;
        public float damage = 40f;
        public float activateDistance = 16f;
        public float reloadMinSeconds = 2.4f;
        public float reloadMaxSeconds = 4f;

        /// <summary>셀 선택 산포 시드. 배치 시 RunManager.Rng에서 배정 (0이면 인스턴스 폴백).</summary>
        public int scatterSeed;

        const int MinCells = 3;
        const int MaxCells = 5;
        const float CellHitRadius = 0.8f;
        const float ExplosionLinger = 0.6f;

        sealed class Strike
        {
            public List<Vector2> Cells;
            public float Elapsed;
            public bool Exploded;
            public int DangerHandle;
        }

        readonly List<Strike> strikes = new List<Strike>();
        float reloadTimer;

        PlayerController player;
        PlayerHealth playerHealth;
        System.Random rng;

        void Update()
        {
            if (!IsRunActive())
            {
                ClearStrikes();
                return;
            }

            if (!TryResolvePlayer())
                return;

            TickReload();
            TickStrikes();
        }

        void OnDestroy()
        {
            ClearStrikes();
        }

        // 플레이어가 구역 근처일 때만 재장전이 돈다 - 멀리 있는 구역이 미리 터지지 않게
        void TickReload()
        {
            float distance = Mathf.Abs(player.transform.position.z - transform.position.z);

            if (distance > activateDistance)
                return;

            reloadTimer -= Time.deltaTime;

            if (reloadTimer > 0f)
                return;

            reloadTimer = NextRange(reloadMinSeconds, reloadMaxSeconds);
            LaunchStrike();
        }

        void LaunchStrike()
        {
            int cellCount = MinCells + Mathf.FloorToInt(NextRange(0f, MaxCells - MinCells + 1));

            List<Vector2> cells = new List<Vector2>();

            for (int i = 0; i < cellCount; i++)
            {
                float cx = transform.position.x + NextRange(-halfWidthX, halfWidthX);
                float cz = transform.position.z + NextRange(-lengthZ * 0.5f, lengthZ * 0.5f);
                cells.Add(new Vector2(cx, cz));
            }

            Strike strike = new Strike { Cells = cells, Elapsed = 0f, Exploded = false };

            // 예고 표시: 각 셀을 붉은 점으로 (DangerGrid 원 - 반경 작게)
            if (DangerGrid.Instance != null)
            {
                Vector3 center = new Vector3(transform.position.x, 0f, transform.position.z);
                strike.DangerHandle = DangerGrid.Instance.ShowCircle(center, halfWidthX);
            }

            strikes.Add(strike);
        }

        void TickStrikes()
        {
            for (int i = strikes.Count - 1; i >= 0; i--)
            {
                Strike strike = strikes[i];
                strike.Elapsed += Time.deltaTime;

                if (!strike.Exploded && strike.Elapsed >= telegraphSeconds)
                    Explode(strike);

                if (strike.Elapsed <= telegraphSeconds + ExplosionLinger)
                    continue;

                HideStrike(strike);
                strikes.RemoveAt(i);
            }
        }

        void Explode(Strike strike)
        {
            strike.Exploded = true;

            if (CameraShake.Instance != null)
                CameraShake.Instance.AddImpulse(0.4f);

            if (player.State == PlayerState.Dead || playerHealth == null)
                return;

            Vector3 playerPosition = player.transform.position;

            foreach (Vector2 cell in strike.Cells)
            {
                float dx = Mathf.Abs(playerPosition.x - cell.x);
                float dz = Mathf.Abs(playerPosition.z - cell.y);

                if (dx > CellHitRadius || dz > CellHitRadius)
                    continue;

                playerHealth.Damage(damage, "strike");
                return;
            }
        }

        void ClearStrikes()
        {
            for (int i = strikes.Count - 1; i >= 0; i--)
                HideStrike(strikes[i]);

            strikes.Clear();
        }

        void HideStrike(Strike strike)
        {
            if (strike.DangerHandle == 0)
                return;

            if (DangerGrid.Instance != null)
                DangerGrid.Instance.Hide(strike.DangerHandle);

            strike.DangerHandle = 0;
        }

        bool TryResolvePlayer()
        {
            if (player != null)
                return true;

            player = FindFirstObjectByType<PlayerController>();

            if (player == null)
                return false;

            playerHealth = player.Health;
            return true;
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
