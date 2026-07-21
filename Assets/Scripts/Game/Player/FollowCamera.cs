using UnityEngine;

namespace Scavenger.Player
{
    /// <summary>
    /// 대각 쿼터뷰~사이드뷰 사이의 로우앵글 추적 카메라 (ADR-0003).
    /// +x측 배치 = 전진이 화면 오른쪽을 향한다.
    /// 위치는 SmoothDamp로 약간 늦게 따라간다 (스텝 이동의 타격감 보조).
    /// 포즈를 매 프레임 재계산하므로 플레이 중 인스펙터 튜닝이 즉시 반영된다.
    /// offset/lookOffset은 캐릭터 에셋 생성 게이트의 동결 대상 수치.
    /// </summary>
    [ExecuteAlways]
    public sealed class FollowCamera : MonoBehaviour
    {
        public Transform target;

        [Header("카메라 위치 오프셋 (측면 대각 로우앵글)")]
        public Vector3 offset = new Vector3(9.5f, 6f, -7f);

        [Header("룩앳 오프셋 (앵커 기준). z = 전방 주시, y = 시선 높이")]
        public Vector3 lookOffset = new Vector3(0f, 1.2f, 7f);

        [Header("따라가기 지연 (초). 0 = 즉시 추적")]
        public float followSmoothTime = 0.15f;

        float smoothedZ;
        float zVelocity;

        void LateUpdate()
        {
            if (target == null)
                return;

            UpdateSmoothedZ();
            ApplyPose();
        }

        /// <summary>런 시작/월드 재생성 시 지연 없이 즉시 위치 맞춤.</summary>
        public void SnapAndLook()
        {
            if (target == null)
                return;

            smoothedZ = target.position.z;
            zVelocity = 0f;

            ApplyPose();
        }

        void UpdateSmoothedZ()
        {
            float targetZ = target.position.z;

            // 에디트 모드나 지연 0에서는 즉시 추적
            if (!Application.isPlaying || followSmoothTime <= 0f)
            {
                smoothedZ = targetZ;
                zVelocity = 0f;
                return;
            }

            smoothedZ = Mathf.SmoothDamp(smoothedZ, targetZ, ref zVelocity, followSmoothTime);
        }

        void ApplyPose()
        {
            // x는 복도 중앙 고정, z만 추적 - 좌우 회피 시 화면이 흔들리지 않게
            Vector3 anchor = new Vector3(0f, 0f, smoothedZ);
            transform.position = anchor + offset;

            Vector3 lookPoint = anchor + lookOffset;
            Vector3 lookDirection = lookPoint - transform.position;

            if (lookDirection.sqrMagnitude < 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }
}
