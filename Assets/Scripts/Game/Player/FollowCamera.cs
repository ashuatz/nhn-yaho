using UnityEngine;

namespace Scavenger.Player
{
    /// <summary>
    /// 대각 쿼터뷰~사이드뷰 사이의 로우앵글 추적 카메라 (ADR-0003).
    /// +x측 배치 = 전진이 화면 오른쪽을 향한다.
    /// 포즈(위치+회전)를 매 프레임 재계산하므로 인스펙터에서 offset/주시점을
    /// 플레이 중에 조작해도 즉시 반영된다 (개발자 튜닝 반응성).
    /// offset/주시점은 캐릭터 에셋 생성 게이트의 동결 대상 수치.
    /// </summary>
    [ExecuteAlways]
    public sealed class FollowCamera : MonoBehaviour
    {
        public Transform target;

        [Header("측면 대각 로우앵글. 에셋 생성 전 동결 대상")]
        public Vector3 offset = new Vector3(9.5f, 6f, -7f);

        [Header("주시점: 플레이어보다 앞을 봐서 원경 확보")]
        public float lookAheadMeters = 7f;
        public float lookHeight = 1.2f;

        void LateUpdate()
        {
            UpdatePose();
        }

        public void SnapAndLook()
        {
            UpdatePose();
        }

        void UpdatePose()
        {
            if (target == null)
                return;

            // x는 복도 중앙 고정, z만 추적 - 좌우 회피 시 화면이 흔들리지 않게
            Vector3 anchor = new Vector3(0f, 0f, target.position.z);
            transform.position = anchor + offset;

            Vector3 lookPoint = anchor + Vector3.up * lookHeight + Vector3.forward * lookAheadMeters;
            Vector3 lookDirection = lookPoint - transform.position;

            if (lookDirection.sqrMagnitude < 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }
}
