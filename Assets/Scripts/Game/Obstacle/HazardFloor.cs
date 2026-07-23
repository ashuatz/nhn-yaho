using Scavenger.Player;
using Scavenger.Run;
using Scavenger.Segment;
using UnityEngine;

namespace Scavenger.Obstacle
{
    /// <summary>
    /// 바닥 장판 (웹 프로토타입 이식). 사각 영역 위에 서 있는 동안 지속 피해(HP).
    /// 즉사가 아니라 HP 소모형 - 잠깐 스치는 것과 오래 머무는 것의 차이가 핵심.
    /// 영역은 DangerGrid로 상시 표시 (예고가 아니라 지속 위험이라 늘 보인다).
    /// 발판 위(공중)나 사망 상태면 면제. 수치는 SegmentSpawner가 주입.
    /// </summary>
    public sealed class HazardFloor : MonoBehaviour
    {
        [Header("영역 반크기 (로컬 x/z). 실제 폭 = 2배")]
        public float halfWidthX = 1.5f;
        public float halfWidthZ = 1f;

        [Header("지속 피해 (초당 HP)")]
        public float damagePerSecond = 26f;

        // 발판/단차 위에 올라선 상태면 장판 면제 - 지면 근처일 때만 밟은 것으로 본다
        const float GroundHeightTolerance = 0.6f;

        // 접촉 판정 여유 (셀 경계에서 애매하게 빠지는 것 방지)
        const float ContactMargin = 0.4f;

        int dangerHandle;
        PlayerController player;
        PlayerHealth playerHealth;

        void Start()
        {
            ShowArea();
        }

        void OnDestroy()
        {
            HideArea();
        }

        void Update()
        {
            if (!IsRunActive())
                return;

            if (!TryResolvePlayer())
                return;

            if (player.State == PlayerState.Dead)
                return;

            if (playerHealth == null)
                return;

            if (!IsPlayerOnHazard())
                return;

            // 지속 피해 - dps * dt로 매 프레임 누적 (HP 0 이하 시 Health가 Kill 위임)
            playerHealth.Damage(damagePerSecond * Time.deltaTime, "hazard");

            // 가벼운 피격 트레머 - 밟고 있는 동안 위험을 몸으로 알린다
            if (CameraShake.Instance != null)
                CameraShake.Instance.RequestTremor(0.12f);
        }

        bool IsPlayerOnHazard()
        {
            Vector3 playerPosition = player.transform.position;

            // 발판/단차 위(공중)면 면제 - 지면 근처에 있을 때만 밟은 것으로 판정
            if (Mathf.Abs(playerPosition.y - transform.position.y) > GroundHeightTolerance)
                return false;

            float dx = Mathf.Abs(playerPosition.x - transform.position.x);
            float dz = Mathf.Abs(playerPosition.z - transform.position.z);

            if (dx > halfWidthX + ContactMargin)
                return false;

            if (dz > halfWidthZ + ContactMargin)
                return false;

            return true;
        }

        void ShowArea()
        {
            if (DangerGrid.Instance == null)
                return;

            Vector2 size = new Vector2(halfWidthX * 2f, halfWidthZ * 2f);
            dangerHandle = DangerGrid.Instance.ShowRect(transform.position, size);
        }

        void HideArea()
        {
            if (dangerHandle == 0)
                return;

            if (DangerGrid.Instance != null)
                DangerGrid.Instance.Hide(dangerHandle);

            dangerHandle = 0;
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
    }
}
