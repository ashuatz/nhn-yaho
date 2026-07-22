using System.Collections.Generic;
using Scavenger.Loot;
using Scavenger.Player;
using Scavenger.Run;
using Scavenger.Segment;
using UnityEngine;

namespace Scavenger.Obstacle
{
    /// <summary>
    /// 낙하물 위협 (사용자 지시): 플레이어 접근 시 착탄 지점을 예고(셀 점멸)하고
    /// 돌이 떨어진다. 착탄하면 그 지점의 바닥 스트립이 깨져 가라앉고(낙사 구멍),
    /// 직격당한 플레이어는 사망. 존 하나 = 1회 발동 (배치는 SegmentSpawner).
    /// 발동 시점 산포 난수는 인스턴스 시드 - 배치 스트림(RunManager.Rng) 오염 금지.
    /// </summary>
    public sealed class RockfallZone : MonoBehaviour
    {
        enum Phase
        {
            Armed,
            Warning,
            Falling,
        }

        [Header("튜닝 (SegmentSpawner가 주입)")]
        public float triggerDistance = 7f;
        public float warnSeconds = 0.95f;
        public float impactRadius = 1.6f;

        /// <summary>착탄 시 남길 우회로 폭 (전폭 함몰 = 봉쇄 금지, 땅 꺼짐과 동일 규칙).</summary>
        public float safeLaneWidth = 2.6f;

        /// <summary>
        /// 착탄 산포 시드. 착탄점은 사망/발판 파괴를 결정하는 게임 결과이므로
        /// 스포너가 배치 시 RunManager.Rng에서 배정 (시드 재현성 - Codex 교차 검토).
        /// 0이면 인스턴스 id 폴백.
        /// </summary>
        public int scatterSeed;

        const float SpawnHeight = 13f;
        const float FallGravityScale = 2.2f;
        const float DirectHitHeightTolerance = 1.6f;
        const float ShakeMaxStrength = 0.5f;
        const float ShakeFalloffDistance = 10f;
        [Range(0f, 1f)] public float warnTremorMax = 0.3f;

        Phase phase = Phase.Armed;
        float warnElapsed;
        int dangerHandle;
        Vector3 target;
        Transform rock;
        float fallSpeed;
        Material rockMaterial;

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

                case Phase.Warning:
                    TickWarning();
                    return;

                case Phase.Falling:
                    TickFalling();
                    return;
            }
        }

        void OnDisable()
        {
            HideDanger();
        }

        void OnDestroy()
        {
            // 돌 인스턴스 머티리얼 해제 (누수 방지 - Codex 교차 검토)
            if (rockMaterial != null)
                Destroy(rockMaterial);
        }

        void TickArmed()
        {
            if (!TryResolvePlayer())
                return;

            float playerZ = player.transform.position.z;

            // 접근 중에만 발동 - 지나간 뒤 등 뒤로 떨어지는 억울함 방지
            if (playerZ > transform.position.z)
                return;

            if (transform.position.z - playerZ > triggerDistance)
                return;

            // 착탄점 = 플레이어 진행 방향 앞쪽 + 좌우 산포. 정확히 머리 위는 피한다 -
            // 예고를 보고 피할 시간을 주는 위협 (즉사 저격 금지)
            Vector3 playerPosition = player.transform.position;

            target = new Vector3(
                playerPosition.x + NextRange(-1f, 1f),
                0f,
                playerPosition.z + NextRange(0.9f, 2.4f));

            if (DangerGrid.Instance != null)
                dangerHandle = DangerGrid.Instance.ShowCircle(target, impactRadius);

            warnElapsed = 0f;
            phase = Phase.Warning;
        }

        void TickWarning()
        {
            warnElapsed += Time.deltaTime;

            // 근접 비례 트레머 - 떨어지기 직전 긴장감
            if (CameraShake.Instance != null && player != null)
            {
                float distance = Vector3.Distance(player.transform.position, target);
                float proximity = Mathf.Clamp01(1f - distance / triggerDistance);
                CameraShake.Instance.RequestTremor(warnTremorMax * proximity);
            }

            if (warnElapsed < warnSeconds)
                return;

            SpawnRock();
            phase = Phase.Falling;
        }

        void SpawnRock()
        {
            GameObject rockObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rockObject.name = "FallingRock";
            rockObject.transform.SetParent(transform, true);
            rockObject.transform.position = target + Vector3.up * SpawnHeight;
            rockObject.transform.rotation = Quaternion.Euler(
                NextRange(0f, 360f), NextRange(0f, 360f), NextRange(0f, 360f));

            float size = NextRange(0.9f, 1.25f);
            rockObject.transform.localScale = new Vector3(size, size, size);

            // 판정은 착탄 시 거리 기반 - 낙하 중 충돌 없음
            Collider rockCollider = rockObject.GetComponent<Collider>();

            if (rockCollider != null)
                Destroy(rockCollider);

            Renderer rockRenderer = rockObject.GetComponent<Renderer>();

            if (rockRenderer != null)
            {
                rockMaterial = new Material(rockRenderer.sharedMaterial);
                rockMaterial.color = new Color(0.27f, 0.25f, 0.27f);
                rockRenderer.sharedMaterial = rockMaterial;
            }

            rock = rockObject.transform;
            fallSpeed = 0f;
        }

        void TickFalling()
        {
            if (rock == null)
            {
                Cancel();
                return;
            }

            float deltaTime = Time.deltaTime;

            fallSpeed += 9.81f * FallGravityScale * deltaTime;
            rock.position += Vector3.down * (fallSpeed * deltaTime);
            rock.Rotate(Vector3.right, 90f * deltaTime, Space.World);

            if (rock.position.y > target.y)
                return;

            Impact();
        }

        void Impact()
        {
            HideDanger();

            // 파편 + 근접 비례 쉐이크 = 착탄 무게감
            LootBurst.Spawn(target + Vector3.up * 0.3f, 10, new Color(0.4f, 0.38f, 0.4f));

            if (CameraShake.Instance != null && player != null)
            {
                float distance = Vector3.Distance(player.transform.position, target);
                float strength = ShakeMaxStrength * Mathf.Clamp01(1f - distance / ShakeFalloffDistance);
                CameraShake.Instance.AddImpulse(strength);
            }

            KillPlayerIfDirectHit();
            BreakFloorAtImpact();

            Destroy(gameObject);
        }

        void KillPlayerIfDirectHit()
        {
            if (player == null)
                return;

            Vector3 playerPosition = player.transform.position;
            Vector2 flatDelta = new Vector2(playerPosition.x - target.x, playerPosition.z - target.z);

            if (flatDelta.sqrMagnitude > impactRadius * impactRadius)
                return;

            if (Mathf.Abs(playerPosition.y - target.y) > DirectHitHeightTolerance)
                return;

            player.Kill("rockfall");
        }

        // 착탄 지점의 바닥 스트립을 깨뜨린다 (사용자 지시: 떨어지면 발판이 깨진다).
        // RaycastAll: 착탄점 위에 플레이어/잡동사니 콜라이더가 겹쳐 있어도 바닥을 찾는다
        void BreakFloorAtImpact()
        {
            Ray probe = new Ray(target + Vector3.up * 1f, Vector3.down);
            RaycastHit[] hits = Physics.RaycastAll(probe, 4f);

            foreach (RaycastHit hit in hits)
            {
                FloorStrip strip = hit.collider.GetComponentInParent<FloorStrip>();

                if (strip == null || strip.IsSinking)
                    continue;

                SinkWithSafeLane(strip);
                return;
            }
        }

        // 전폭 함몰 = 점프 없는 플레이어에게 우회 불가 봉쇄가 된다 (Codex 교차 검토).
        // 땅 꺼짐 트랩과 동일 규칙: 스트립을 분할해 착탄 쪽만 침몰시키고
        // 반대쪽에 안전 레인을 남긴다. 좁은 조각/단차 루트는 통째로 침몰
        void SinkWithSafeLane(FloorStrip strip)
        {
            Transform stripTransform = strip.transform;
            float fullWidth = stripTransform.localScale.x;

            if (fullWidth < safeLaneWidth * 2f)
            {
                strip.Sink();
                return;
            }

            float laneWidth = Mathf.Clamp(safeLaneWidth, 1f, fullWidth - 1f);
            float sinkWidth = fullWidth - laneWidth;
            float sinkSide = target.x >= stripTransform.position.x ? 1f : -1f;

            Vector3 scale = stripTransform.localScale;
            Vector3 localPosition = stripTransform.localPosition;

            // 부착물(루트/폭탄 등)은 부모 리스케일 왜곡을 피해 잠시 떼어둔다
            List<Transform> attachments = new List<Transform>();

            for (int i = stripTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = stripTransform.GetChild(i);
                child.SetParent(stripTransform.parent, true);
                attachments.Add(child);
            }

            // 침몰 조각 (착탄 쪽 가장자리, 신규)
            float sinkCenterX = sinkSide * (fullWidth * 0.5f - sinkWidth * 0.5f);

            GameObject sinkObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sinkObject.name = "FloorStrip (Rockfall)";
            sinkObject.transform.SetParent(stripTransform.parent, false);
            sinkObject.transform.localScale = new Vector3(sinkWidth, scale.y, scale.z);
            sinkObject.transform.localPosition = new Vector3(
                localPosition.x + sinkCenterX, localPosition.y, localPosition.z);

            FloorStrip sinkStrip = sinkObject.AddComponent<FloorStrip>();
            sinkStrip.depthMeters = strip.depthMeters;
            SegmentEnvironment.TintGameObject(sinkObject, new Color(0.3f, 0.31f, 0.33f));

            // 원본 = 안전 레인 (반대쪽 가장자리로 축소)
            float laneCenterX = -sinkSide * (fullWidth * 0.5f - laneWidth * 0.5f);
            stripTransform.localScale = new Vector3(laneWidth, scale.y, scale.z);
            stripTransform.localPosition = new Vector3(
                localPosition.x + laneCenterX, localPosition.y, localPosition.z);

            // 부착물 재부착: 착탄 조각 위 = 함께 침몰, 안전 레인 위 = 유지
            float splitX = stripTransform.parent != null
                ? stripTransform.parent.TransformPoint(new Vector3(
                    localPosition.x + sinkCenterX - sinkSide * sinkWidth * 0.5f, 0f, 0f)).x
                : localPosition.x + sinkCenterX - sinkSide * sinkWidth * 0.5f;

            foreach (Transform attachment in attachments)
            {
                bool onSinkSide = sinkSide > 0f
                    ? attachment.position.x >= splitX
                    : attachment.position.x <= splitX;

                attachment.SetParent(onSinkSide ? sinkStrip.transform : stripTransform, true);
            }

            // 붕괴 전선이 나중에 지나갈 때 함께 처리되도록 등록
            if (CollapseFront.Instance != null)
                CollapseFront.Instance.RegisterStrips(new List<FloorStrip> { sinkStrip });

            sinkStrip.Sink();
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

        // 지연 초기화: AddComponent 직후 Awake에서 만들면 스포너의 시드 주입보다
        // 먼저 실행된다 - 첫 사용 시점에 생성해야 배정된 시드가 반영된다
        float NextRange(float min, float max)
        {
            if (rng == null)
                rng = new System.Random(scatterSeed != 0 ? scatterSeed : GetInstanceID());

            return Mathf.Lerp(min, max, (float)rng.NextDouble());
        }
    }
}
