using System.Collections.Generic;
using Scavenger.Loot;
using Scavenger.Player;
using Scavenger.Run;
using Scavenger.Segment;
using UnityEngine;

namespace Scavenger.Obstacle
{
    /// <summary>
    /// 기둥 붕괴 (사용자 지시: 기둥에 의해 무너지는 바닥).
    /// 복도 가장자리에 서 있는 기둥이 플레이어 접근 시 예고(발자국 셀 점멸 + 흔들림)
    /// 후 복도를 가로질러 쓰러진다. 깔리면 피해 2, 착지 발자국의 바닥은 분할 침몰
    /// (FloorBreaker - 반대쪽 안전 레인 보존). 존 하나 = 1회 발동.
    /// 체크포인트 안전지대에서는 발동하지 않는다.
    /// </summary>
    public sealed class ToppleColumn : MonoBehaviour
    {
        enum Phase
        {
            Armed,
            Warning,
            Toppling,
        }

        [Header("튜닝 (SegmentSpawner가 주입)")]
        public float triggerDistance = 8f;
        public float warnSeconds = 1.1f;
        public float safeLaneWidth = 2.6f;

        /// <summary>쓰러지는 길이 (복도 폭 - 안전 레인). 스포너가 계산해 주입.</summary>
        public float pillarLength = 7f;

        /// <summary>기둥이 서 있는 쪽 (+1/-1). 반대쪽으로 쓰러진다.</summary>
        public float side = 1f;

        const float PillarThickness = 1.1f;
        const float ToppleAccelDegrees = 260f;
        const float ImpactHalfWidthZ = 0.8f;
        const float WarnWobbleDegrees = 1.6f;

        Phase phase = Phase.Armed;
        float warnElapsed;
        float toppleAngle;
        float toppleVelocity;
        int dangerHandle;
        Transform pillar;
        Material pillarMaterial;
        Vector3 pivotPoint;

        PlayerController player;

        void Start()
        {
            BuildPillar();
        }

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

                case Phase.Warning:
                    TickWarning();
                    return;

                case Phase.Toppling:
                    TickToppling();
                    return;
            }
        }

        void OnDisable()
        {
            HideDanger();
        }

        void OnDestroy()
        {
            if (pillarMaterial != null)
                Destroy(pillarMaterial);
        }

        void BuildPillar()
        {
            GameObject pillarObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillarObject.name = "Pillar";
            pillarObject.transform.SetParent(transform, false);
            pillarObject.transform.localScale = new Vector3(
                PillarThickness, pillarLength, PillarThickness);
            pillarObject.transform.localPosition = new Vector3(0f, pillarLength * 0.5f, 0f);

            Collider pillarCollider = pillarObject.GetComponent<Collider>();

            if (pillarCollider != null)
                Destroy(pillarCollider);

            Renderer pillarRenderer = pillarObject.GetComponent<Renderer>();

            if (pillarRenderer != null)
            {
                pillarMaterial = new Material(pillarRenderer.sharedMaterial);
                pillarMaterial.color = new Color(0.34f, 0.3f, 0.28f);
                pillarRenderer.sharedMaterial = pillarMaterial;
            }

            pillar = pillarObject.transform;

            // 회전 피벗 = 복도 쪽 밑동 모서리
            pivotPoint = transform.position + new Vector3(-side * PillarThickness * 0.5f, 0f, 0f);
        }

        void TickArmed()
        {
            if (!TryResolvePlayer())
                return;

            // 체크포인트는 안전지대 - 발동 보류 (사용자 지시)
            if (CheckpointZone.PlayerInside)
                return;

            float playerZ = player.transform.position.z;

            if (playerZ > transform.position.z + 2f)
                return;

            if (transform.position.z - playerZ > triggerDistance)
                return;

            if (DangerGrid.Instance != null)
            {
                Vector3 footprintCenter = FootprintCenter();
                dangerHandle = DangerGrid.Instance.ShowRect(
                    footprintCenter, new Vector2(pillarLength, ImpactHalfWidthZ * 2f));
            }

            warnElapsed = 0f;
            phase = Phase.Warning;
        }

        void TickWarning()
        {
            warnElapsed += Time.deltaTime;

            // 흔들림 = 곧 쓰러진다는 신호
            if (pillar != null)
            {
                float wobble = Mathf.Sin(warnElapsed * 22f) * WarnWobbleDegrees
                    * Mathf.Clamp01(warnElapsed / warnSeconds);
                pillar.rotation = Quaternion.Euler(0f, 0f, wobble);
            }

            if (CameraShake.Instance != null && player != null)
            {
                float distance = Mathf.Abs(player.transform.position.z - transform.position.z);
                float proximity = Mathf.Clamp01(1f - distance / triggerDistance);
                CameraShake.Instance.RequestTremor(0.25f * proximity);
            }

            if (warnElapsed < warnSeconds)
                return;

            toppleAngle = 0f;
            toppleVelocity = 0f;
            phase = Phase.Toppling;
        }

        void TickToppling()
        {
            if (pillar == null)
            {
                Cancel();
                return;
            }

            float deltaTime = Time.deltaTime;

            toppleVelocity += ToppleAccelDegrees * deltaTime;
            float deltaAngle = Mathf.Min(toppleVelocity * deltaTime, 90f - toppleAngle);
            toppleAngle += deltaAngle;

            // side 쪽에서 반대쪽으로: +x측 기둥은 +z축 기준 양의 회전이 복도를 향한다
            pillar.RotateAround(pivotPoint, Vector3.forward, side * deltaAngle);

            if (toppleAngle < 90f)
                return;

            Impact();
        }

        void Impact()
        {
            HideDanger();

            Vector3 footprintCenter = FootprintCenter();

            // 파편 + 쉐이크 = 착지 무게감
            LootBurst.Spawn(footprintCenter + Vector3.up * 0.3f, 12, new Color(0.4f, 0.36f, 0.33f));

            if (CameraShake.Instance != null && player != null)
            {
                float distance = Vector3.Distance(player.transform.position, footprintCenter);
                float strength = 0.55f * Mathf.Clamp01(1f - distance / 12f);
                CameraShake.Instance.AddImpulse(strength);
            }

            DamagePlayerIfCrushed();
            BreakFootprintFloor();

            Destroy(gameObject);
        }

        void DamagePlayerIfCrushed()
        {
            if (player == null)
                return;

            Vector3 playerPosition = player.transform.position;

            if (Mathf.Abs(playerPosition.z - transform.position.z) > ImpactHalfWidthZ + 0.4f)
                return;

            // 발자국 x 범위: 복도 쪽 밑동에서 반대쪽으로 pillarLength
            float nearX = transform.position.x - side * PillarThickness * 0.5f;
            float farX = nearX - side * pillarLength;
            float minX = Mathf.Min(nearX, farX);
            float maxX = Mathf.Max(nearX, farX);

            if (playerPosition.x < minX || playerPosition.x > maxX)
                return;

            if (playerPosition.y > 1.6f)
                return;

            PlayerHealth health = player.GetComponent<PlayerHealth>();

            if (health != null)
                health.Damage(2, "column");
            else
                player.Kill("column");
        }

        // 발자국을 따라 표본을 떠서 걸친 스트립 전부 분할 침몰 (안전 레인은 반대쪽)
        void BreakFootprintFloor()
        {
            HashSet<FloorStrip> strips = new HashSet<FloorStrip>();
            float[] zOffsets = { -ImpactHalfWidthZ * 0.7f, 0f, ImpactHalfWidthZ * 0.7f };

            foreach (float zOffset in zOffsets)
            {
                Vector3 probe = new Vector3(
                    transform.position.x - side * (PillarThickness * 0.5f + pillarLength * 0.5f),
                    0f,
                    transform.position.z + zOffset);

                FloorStrip strip = FloorBreaker.FindStripBelow(probe);

                if (strip != null)
                    strips.Add(strip);
            }

            foreach (FloorStrip strip in strips)
                FloorBreaker.SinkWithSafeLane(strip, transform.position.x, safeLaneWidth);
        }

        Vector3 FootprintCenter()
        {
            return new Vector3(
                transform.position.x - side * (PillarThickness * 0.5f + pillarLength * 0.5f),
                0f,
                transform.position.z);
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
    }
}
