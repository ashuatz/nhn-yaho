using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 땅 꺼짐 트랩 (M3-2). FloorStrip과 같은 오브젝트에 부착된다.
    /// 플레이어가 접근하면 예고(위험 셀 점멸 + 근접 비례 카메라 트레머) 후
    /// 스트립을 함몰시킨다 (FloorStrip.Sink 재활용 -> 낙사 연계).
    /// 수치는 SegmentSpawner 프리팹 필드에서 주입.
    /// </summary>
    public sealed class SinkTrap : MonoBehaviour
    {
        [Header("발동 (스포너가 주입)")]
        public float triggerDistance = 5.5f;
        public float warnSeconds = 1.1f;
        [Range(0f, 1f)] public float warnTremorMax = 0.4f;

        FloorStrip strip;
        PlayerController player;
        int dangerHandle;
        float warnRemaining;
        bool warning;
        bool done;

        void Awake()
        {
            strip = GetComponent<FloorStrip>();
        }

        void Update()
        {
            if (done)
                return;

            // 붕괴 전선이 먼저 침몰시켰으면 트랩 무효
            if (strip == null || strip.IsSinking)
            {
                Finish();
                return;
            }

            RunManager run = RunManager.Instance;

            if (run == null || run.StateMachine.Current != RunState.Running)
                return;

            if (!TryResolvePlayer())
                return;

            // 스트립은 복도 전폭이므로 z 거리로 판정
            float distance = Mathf.Abs(player.transform.position.z - transform.position.z);

            if (!warning)
            {
                if (distance > triggerDistance)
                    return;

                BeginWarning();
            }

            TickWarning(distance);
        }

        void OnDestroy()
        {
            HideDanger();
        }

        void BeginWarning()
        {
            warning = true;
            warnRemaining = warnSeconds;

            if (DangerGrid.Instance != null)
            {
                Vector2 footprint = new Vector2(transform.localScale.x, strip.depthMeters);
                dangerHandle = DangerGrid.Instance.ShowRect(transform.position, footprint);
            }
        }

        void TickWarning(float distance)
        {
            // 가까울수록 강한 트레머 (사용자 지시: 땅 꺼짐 근접 피드백)
            if (CameraShake.Instance != null)
            {
                float proximity01 = 1f - Mathf.Clamp01(distance / triggerDistance);
                CameraShake.Instance.RequestTremor(Mathf.Lerp(0.1f, warnTremorMax, proximity01));
            }

            warnRemaining -= Time.deltaTime;

            if (warnRemaining > 0f)
                return;

            strip.Sink();
            Finish();
        }

        bool TryResolvePlayer()
        {
            if (player != null)
                return true;

            player = FindFirstObjectByType<PlayerController>();
            return player != null;
        }

        void Finish()
        {
            done = true;
            HideDanger();
            enabled = false;
        }

        void HideDanger()
        {
            if (dangerHandle == 0)
                return;

            if (DangerGrid.Instance != null)
                DangerGrid.Instance.Hide(dangerHandle);

            dangerHandle = 0;
        }
    }
}
