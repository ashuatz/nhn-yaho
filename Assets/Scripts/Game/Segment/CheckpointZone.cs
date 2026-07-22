using System.Collections.Generic;
using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 체크포인트 안전지대 (사용자 지시). 구간 끝 선택지 주변의 넓은 휴식 공간:
    /// - 머무는 동안 공격 발동 없음 (위협 존들이 PlayerInside를 확인)
    /// - 붕괴 전선 정지 (CollapseFront.RequestHold)
    /// - 복도보다 넓다 - 머무는 동안 이동 클램프를 체크포인트 폭으로 확장
    /// - 앞으로 벗어나면 플랫폼 통째로 분해되어 낙하하고 붕괴가 재개된다
    /// 생성/배선은 SegmentSpawner.BuildCheckpoint.
    /// </summary>
    public sealed class CheckpointZone : MonoBehaviour
    {
        static CheckpointZone activeZone;

        /// <summary>플레이어가 체크포인트 안에 있는가. 위협 존들의 발동 보류 신호.</summary>
        public static bool PlayerInside
        {
            get { return activeZone != null; }
        }

        /// <summary>월드 z 범위. 스포너가 주입.</summary>
        public float StartZ { get; private set; }
        public float EndZ { get; private set; }

        float checkpointHalfWidth;
        float corridorHalfWidth;

        readonly List<FloorStrip> platformStrips = new List<FloorStrip>();

        bool insideLastFrame;
        bool crumbled;
        PlayerController player;

        public void Initialize(
            List<FloorStrip> strips, float startZ, float endZ,
            float checkpointHalfWidth, float corridorHalfWidth)
        {
            platformStrips.AddRange(strips);
            StartZ = startZ;
            EndZ = endZ;
            this.checkpointHalfWidth = checkpointHalfWidth;
            this.corridorHalfWidth = corridorHalfWidth;
        }

        void Update()
        {
            RunManager run = RunManager.Instance;

            if (run == null || run.StateMachine.Current != RunState.Running)
            {
                LeaveWithoutCrumble();
                return;
            }

            if (!TryResolvePlayer())
                return;

            if (player.State == PlayerState.Dead)
            {
                LeaveWithoutCrumble();
                return;
            }

            // z만으로 판정하면 옆으로 떨어진/밀린 플레이어도 안전지대로 잡힌다
            // (교차 검토) - x 범위와 대략적 접지 높이를 함께 확인
            Vector3 playerPosition = player.transform.position;

            bool inside = !crumbled
                && playerPosition.z >= StartZ && playerPosition.z <= EndZ
                && Mathf.Abs(playerPosition.x) <= checkpointHalfWidth + 0.5f
                && playerPosition.y > -1.5f;

            if (inside)
            {
                TickInside();
                return;
            }

            if (!insideLastFrame)
                return;

            // 이탈: 앞으로 나가면 플랫폼 분해 (사용자 지시). 뒤로는 그냥 해제
            RestorePlayerBounds();
            insideLastFrame = false;

            if (activeZone == this)
                activeZone = null;

            if (playerPosition.z > EndZ)
                Crumble();
        }

        // Destroy 지연 중 잔존 방지 - 비활성화 즉시 전역 신호/클램프 정리
        void OnDisable()
        {
            LeaveWithoutCrumble();
        }

        // 붕괴 전선 정지는 CollapseFront가 PlayerInside를 직접 조회한다
        void TickInside()
        {
            if (insideLastFrame)
                return;

            insideLastFrame = true;
            activeZone = this;

            // 체크포인트는 복도보다 넓다 - 이동 클램프 확장
            player.Motor.corridorHalfWidth = checkpointHalfWidth;
        }

        void LeaveWithoutCrumble()
        {
            if (!insideLastFrame)
                return;

            RestorePlayerBounds();
            insideLastFrame = false;

            if (activeZone == this)
                activeZone = null;
        }

        void RestorePlayerBounds()
        {
            if (player != null)
                player.Motor.corridorHalfWidth = corridorHalfWidth;
        }

        // 플랫폼 통째로 분해 낙하 (사용자 지시). 코리도 스트립은 표면이
        // 그리드 셀 조각으로 쪼개져 떨어진다 (FloorStrip.Sink -> SinkDebris)
        void Crumble()
        {
            if (crumbled)
                return;

            crumbled = true;

            foreach (FloorStrip strip in platformStrips)
            {
                if (strip != null && !strip.IsSinking)
                    strip.Sink();
            }

            if (CameraShake.Instance != null)
                CameraShake.Instance.AddImpulse(0.25f);

            if (EnvironmentRenderer.Active != null)
            {
                EnvironmentRenderer.Active.AddImpulse(
                    new Vector3(0f, 0f, (StartZ + EndZ) * 0.5f), radius: 12f, strength: 1.8f);
            }

            // 존 오브젝트는 남겨도 무해하지만 즉시 정리 (스트립은 각자 침몰)
            Destroy(gameObject);
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
