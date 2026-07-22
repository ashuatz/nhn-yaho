using System.Collections.Generic;
using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 시간 압박의 단일 소스 (ADR-0006): 붕괴 전선이 플레이어 뒤에서 전진하며
    /// 지나간 바닥 스트립을 가라앉힌다. 밟을 곳이 사라지므로 정지 플레이 = 낙사.
    /// 후퇴 불가도 이 장치가 겸한다 (전선 앞으로 플레이어 z 클램프).
    /// 속도는 깊이에 따라 증가. 수치는 프리팹에서 튜닝.
    /// </summary>
    public sealed class CollapseFront : MonoBehaviour
    {
        public static CollapseFront Instance { get; private set; }

        [Header("붕괴 진행 (프리팹 튜닝 지점)")]
        public float startDelaySeconds = 6f;
        public float baseSpeed = 1.1f;
        public float speedPerDepth = 0.15f;
        public float maxSpeed = 3.2f;

        [Header("플레이어 후퇴 클램프 여유")]
        public float playerClampMargin = 1.5f;

        public float FrontZ { get; private set; }

        public float SafeMinZ
        {
            get { return FrontZ + playerClampMargin; }
        }

        /// <summary>플레이어와 전선의 거리. HUD 경고 판정용.</summary>
        public float DistanceToPlayer
        {
            get
            {
                if (trackedPlayer == null)
                    return float.PositiveInfinity;

                return trackedPlayer.transform.position.z - FrontZ;
            }
        }

        readonly List<FloorStrip> strips = new List<FloorStrip>();
        PlayerController trackedPlayer;
        float delayRemaining;

        void OnEnable()
        {
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Track(PlayerController player)
        {
            trackedPlayer = player;
        }

        /// <summary>런 시작 시 GameFlow가 호출. 전선을 플레이어 뒤로 리셋.</summary>
        public void ResetFront(float startZ)
        {
            FrontZ = startZ;
            delayRemaining = startDelaySeconds;

            if (trackedPlayer != null)
                trackedPlayer.Motor.MinZ = SafeMinZ;
        }

        public void RegisterStrips(List<FloorStrip> newStrips)
        {
            foreach (FloorStrip strip in newStrips)
            {
                if (strip == null || strips.Contains(strip))
                    continue;

                strips.Add(strip);
            }
        }

        void Update()
        {
            RunManager run = RunManager.Instance;

            if (run == null || run.StateMachine.Current != RunState.Running)
                return;

            if (delayRemaining > 0f)
            {
                delayRemaining -= Time.deltaTime;
                return;
            }

            float speed = Mathf.Min(maxSpeed, baseSpeed + speedPerDepth * (run.Depth - 1));
            FrontZ += speed * Time.deltaTime;

            SinkPassedStrips();

            // 후퇴 불가: 전선 앞으로 z 클램프 (구조적 전달, ADR-0006)
            if (trackedPlayer != null)
                trackedPlayer.Motor.MinZ = SafeMinZ;
        }

        void SinkPassedStrips()
        {
            for (int i = strips.Count - 1; i >= 0; i--)
            {
                FloorStrip strip = strips[i];

                // 구간 제거와 함께 파괴된 스트립 정리
                if (strip == null)
                {
                    strips.RemoveAt(i);
                    continue;
                }

                // 긴 요소(단차)는 전선이 2m 파고들면 무너진다 - 면역 구간 방지 (검증 반영)
                if (strip.SinkThresholdZ >= FrontZ)
                    continue;

                float sinkZ = strip.EndZ;
                strip.Sink();
                strips.RemoveAt(i);

                // 배경이 붕괴에 반응 (brg-shooter 바운스 이식)
                if (EnvironmentRenderer.Active != null)
                {
                    EnvironmentRenderer.Active.AddImpulse(
                        new Vector3(0f, 0f, sinkZ), radius: 10f, strength: 1.6f);
                }
            }
        }
    }
}
