using Scavenger.Player;
using Scavenger.Run;
using Scavenger.Segment;
using UnityEngine;

namespace Scavenger.Obstacle
{
    /// <summary>
    /// 돌진 적 (사용자 지시): 플레이어 접근 시 진행 방향 앞쪽에서 나타나
    /// 뒤쪽(-z)으로 복도를 질주한다. 경로는 레인 셀 점멸로 예고 - 옆으로 피하는 위협.
    /// 접촉 = 피해 1 + 밀려남. 존 하나 = 1회 발동 (배치는 SegmentSpawner).
    /// 체크포인트 안전지대에서는 발동하지 않는다.
    /// </summary>
    public sealed class ChargingEnemy : MonoBehaviour
    {
        enum Phase
        {
            Armed,
            Telegraph,
            Charging,
        }

        [Header("튜닝 (SegmentSpawner가 주입)")]
        public float triggerDistance = 10f;
        public float telegraphSeconds = 0.8f;
        public float chargeSpeed = 8.5f;
        public float hitRadius = 1.05f;
        public float knockbackSpeed = 6.5f;

        /// <summary>돌진 시작점 = 발동 시점 플레이어 z + 이 거리 (포그 너머).</summary>
        public float spawnAheadDistance = 16f;

        /// <summary>레인 x 산포 시드. 배치 시 RunManager.Rng에서 배정 (재현성).</summary>
        public int scatterSeed;

        const float LaneDisplayWidth = 1.5f;
        const float PassBehindDistance = 12f;
        const float HitHeightTolerance = 1.6f;
        const float RunBobHeight = 0.18f;
        const float RunBobFrequency = 9f;

        Phase phase = Phase.Armed;
        float telegraphElapsed;
        float laneX;
        float chargeStartZ;
        float chargeEndZ;
        int dangerHandle;
        Transform body;
        Material bodyMaterial;
        bool hasHit;

        PlayerController player;
        System.Random rng;

        void Update()
        {
            if (!IsRunActive())
            {
                Cancel();
                return;
            }

            switch (phase)
            {
                case Phase.Armed:
                    TickArmed();
                    return;

                case Phase.Telegraph:
                    TickTelegraph();
                    return;

                case Phase.Charging:
                    TickCharging();
                    return;
            }
        }

        void OnDisable()
        {
            HideDanger();
        }

        void OnDestroy()
        {
            if (bodyMaterial != null)
                Destroy(bodyMaterial);
        }

        void TickArmed()
        {
            if (!TryResolvePlayer())
                return;

            // 체크포인트는 안전지대 - 발동 보류 (사용자 지시)
            if (CheckpointZone.PlayerInside)
                return;

            float playerZ = player.transform.position.z;

            if (playerZ > transform.position.z)
                return;

            if (transform.position.z - playerZ > triggerDistance)
                return;

            if (rng == null)
                rng = new System.Random(scatterSeed != 0 ? scatterSeed : GetInstanceID());

            // 레인 = 발동 시점 플레이어 x 부근. 예고를 보고 옆으로 피할 시간을 준다
            float halfWidth = player.Motor.corridorHalfWidth;
            laneX = Mathf.Clamp(
                player.transform.position.x + NextRange(-0.8f, 0.8f),
                -halfWidth + 0.8f, halfWidth - 0.8f);

            chargeStartZ = playerZ + spawnAheadDistance;
            chargeEndZ = playerZ - PassBehindDistance;

            if (DangerGrid.Instance != null)
            {
                float length = chargeStartZ - chargeEndZ;
                Vector3 center = new Vector3(laneX, 0f, (chargeStartZ + chargeEndZ) * 0.5f);
                dangerHandle = DangerGrid.Instance.ShowRect(
                    center, new Vector2(LaneDisplayWidth, length));
            }

            telegraphElapsed = 0f;
            phase = Phase.Telegraph;
        }

        void TickTelegraph()
        {
            telegraphElapsed += Time.deltaTime;

            if (CameraShake.Instance != null)
                CameraShake.Instance.RequestTremor(0.12f);

            if (telegraphElapsed < telegraphSeconds)
                return;

            SpawnBody();
            phase = Phase.Charging;
        }

        void SpawnBody()
        {
            GameObject bodyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyObject.name = "ChargerBody";
            bodyObject.transform.SetParent(transform, true);
            bodyObject.transform.position = new Vector3(laneX, 0.55f, chargeStartZ);
            bodyObject.transform.localScale = new Vector3(0.7f, 1.1f, 0.9f);

            // 판정은 거리 기반 - 물리 충돌 없음 (통과하며 밀치는 위협)
            Collider bodyCollider = bodyObject.GetComponent<Collider>();

            if (bodyCollider != null)
                Destroy(bodyCollider);

            Renderer bodyRenderer = bodyObject.GetComponent<Renderer>();

            if (bodyRenderer != null)
            {
                bodyMaterial = new Material(bodyRenderer.sharedMaterial);
                bodyMaterial.color = new Color(0.75f, 0.2f, 0.15f);
                bodyRenderer.sharedMaterial = bodyMaterial;
            }

            body = bodyObject.transform;
        }

        void TickCharging()
        {
            if (body == null)
            {
                Cancel();
                return;
            }

            Vector3 position = body.position;
            position.z -= chargeSpeed * Time.deltaTime;
            position.y = 0.55f + Mathf.Abs(Mathf.Sin(Time.time * RunBobFrequency)) * RunBobHeight;
            body.position = position;

            TryHitPlayer();

            if (position.z > chargeEndZ)
                return;

            HideDanger();
            Destroy(gameObject);
        }

        void TryHitPlayer()
        {
            if (hasHit || player == null)
                return;

            Vector3 playerPosition = player.transform.position;
            Vector2 flatDelta = new Vector2(
                playerPosition.x - body.position.x, playerPosition.z - body.position.z);

            if (flatDelta.sqrMagnitude > hitRadius * hitRadius)
                return;

            if (Mathf.Abs(playerPosition.y - 0.55f) > HitHeightTolerance)
                return;

            hasHit = true;

            // 밀려남: 진행 반대(-z) + 레인 바깥쪽으로 비스듬히
            float sideSign = playerPosition.x >= body.position.x ? 1f : -1f;
            Vector3 knockback = new Vector3(sideSign * 0.6f, 0f, -1f).normalized * knockbackSpeed;
            player.Motor.AddImpulse(knockback);

            if (CameraShake.Instance != null)
                CameraShake.Instance.AddImpulse(0.35f);

            PlayerHealth health = player.GetComponent<PlayerHealth>();

            if (health != null)
                health.Damage(1, "charger");
            else
                player.Kill("charger");
        }

        void Cancel()
        {
            HideDanger();

            if (phase != Phase.Armed)
                Destroy(gameObject);
        }

        void HideDanger()
        {
            if (dangerHandle == 0)
                return;

            if (DangerGrid.Instance != null)
                DangerGrid.Instance.Hide(dangerHandle);

            dangerHandle = 0;
        }

        static bool IsRunActive()
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return false;

            return run.StateMachine.Current == RunState.Running;
        }

        bool TryResolvePlayer()
        {
            if (player != null)
                return true;

            player = FindFirstObjectByType<PlayerController>();
            return player != null;
        }

        float NextRange(float min, float max)
        {
            return Mathf.Lerp(min, max, (float)rng.NextDouble());
        }
    }
}
