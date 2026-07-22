using UnityEngine;

namespace Scavenger.Player
{
    /// <summary>
    /// 대각 쿼터뷰~사이드뷰 사이의 로우앵글 추적 카메라 (ADR-0003).
    /// 리그 구성: 룩앳 지점을 먼저 정하고, 카메라 포지션은 룩앳 기준 오프셋으로 정의한다.
    ///   1. lookAtOffset      - 앵커(복도 중앙, 플레이어 z) 기준 월드축. 바라보는 지점
    ///   2. positionOffsetWorld - 룩앳 지점 기준 월드축. 카메라 위치
    ///   3. positionOffsetLocal - 시선 로컬축. 회전 확정 후 평행이동 (화면 구도 시프트,
    ///      시선 방향은 바뀌지 않음 - 플레이어를 화면 비중심에 두는 용도)
    /// 위치는 SmoothDamp로 약간 늦게 따라간다. 포즈는 매 프레임 재계산 - 튜닝 즉시 반영.
    /// 수치는 캐릭터 에셋 생성 게이트의 동결 대상.
    /// </summary>
    [ExecuteAlways]
    public sealed class FollowCamera : MonoBehaviour
    {
        public Transform target;

        [Header("룩앳 지점: 앵커 기준 월드축")]
        public Vector3 lookAtOffset = new Vector3(0f, 1.2f, 7f);

        [Header("카메라 포지션: 룩앳 지점 기준 월드축")]
        public Vector3 positionOffsetWorld = new Vector3(9.5f, 4.8f, -14f);

        [Header("카메라 포지션 보정: 시선 로컬축 (구도 시프트, 시선 방향 불변)")]
        public Vector3 positionOffsetLocal = Vector3.zero;

        [Header("따라가기 지연 (초). 0 = 즉시 추적")]
        public float followSmoothTime = 0.15f;

        [Header("벨트스크롤: 카메라 후방 한계 여유 (앵커 기준 뒤 허용 거리)")]
        public float backLimitMargin = 4f;

        /// <summary>플레이어 후퇴 한계 z (벨트스크롤 - 카메라 영역 밖 이탈 금지).</summary>
        public float BackLimitZ
        {
            get { return smoothedZ - backLimitMargin; }
        }

        float smoothedZ;
        float zVelocity;

        // 벨트스크롤 (사용자 지시): 카메라 앵커는 최대 도달 z만 따른다 - 후퇴 없음
        float ratchetZ;

        PlayerMotor targetMotor;

        void LateUpdate()
        {
            if (target == null)
                return;

            UpdateSmoothedZ();
            ApplyPose();
            PushBackLimit();
        }

        /// <summary>런 시작/월드 재생성 시 지연 없이 즉시 위치 맞춤.</summary>
        public void SnapAndLook()
        {
            if (target == null)
                return;

            smoothedZ = target.position.z;
            ratchetZ = smoothedZ;
            zVelocity = 0f;

            ApplyPose();
            PushBackLimit();
        }

        void UpdateSmoothedZ()
        {
            float targetZ = target.position.z;

            // 에디트 모드나 지연 0에서는 즉시 추적 (에디트 모드는 래칫도 무시)
            if (!Application.isPlaying || followSmoothTime <= 0f)
            {
                if (Application.isPlaying)
                    targetZ = ratchetZ = Mathf.Max(ratchetZ, targetZ);

                smoothedZ = targetZ;
                zVelocity = 0f;
                return;
            }

            // 전진 전용 래칫: 플레이어가 후퇴해도 카메라는 물러나지 않는다
            ratchetZ = Mathf.Max(ratchetZ, targetZ);

            smoothedZ = Mathf.SmoothDamp(smoothedZ, ratchetZ, ref zVelocity, followSmoothTime);
        }

        // 후퇴 한계를 모터에 공급 - 카메라 가시 영역 밖 이탈 금지 (벨트스크롤)
        void PushBackLimit()
        {
            if (!Application.isPlaying)
                return;

            if (targetMotor == null)
            {
                targetMotor = target.GetComponent<PlayerMotor>();

                if (targetMotor == null)
                    return;
            }

            targetMotor.CameraMinZ = BackLimitZ;
        }

        void ApplyPose()
        {
            // x는 복도 중앙 고정, z만 추적 - 좌우 회피 시 화면이 흔들리지 않게
            Vector3 anchor = new Vector3(0f, 0f, smoothedZ);
            Vector3 lookAt = anchor + lookAtOffset;

            Vector3 basePosition = lookAt + positionOffsetWorld;
            Vector3 lookDirection = lookAt - basePosition;

            if (lookDirection.sqrMagnitude < 0.0001f)
                return;

            Quaternion rotation = Quaternion.LookRotation(lookDirection);

            transform.rotation = rotation;
            transform.position = basePosition + rotation * positionOffsetLocal;
        }
    }
}
