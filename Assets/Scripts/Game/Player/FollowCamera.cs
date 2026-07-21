using UnityEngine;

namespace Scavenger.Player
{
    /// <summary>
    /// 대각 쿼터뷰~사이드뷰 사이의 로우앵글 추적 카메라 (ADR-0003).
    /// 근경(플레이어 주변)과 원경(전방 진행 방향)이 함께 보이도록
    /// 측면 대각 오프셋 + 전방 주시점을 사용한다.
    /// offset/lookAhead는 캐릭터 에셋 생성 게이트의 동결 대상 수치.
    /// </summary>
    public sealed class FollowCamera : MonoBehaviour
    {
        public Transform target;

        [Header("측면 대각 로우앵글. +x측 배치 = 전진이 화면 오른쪽을 향한다. 에셋 생성 전 동결 대상")]
        public Vector3 offset = new Vector3(9.5f, 6f, -7f);

        [Header("주시점: 플레이어보다 앞을 봐서 원경 확보")]
        public float lookAheadMeters = 7f;
        public float lookHeight = 1.2f;

        void LateUpdate()
        {
            if (target == null)
                return;

            // x는 복도 중앙 고정, z만 추적 - 좌우 회피 시 화면이 흔들리지 않게
            Vector3 anchor = new Vector3(0f, 0f, target.position.z);
            transform.position = anchor + offset;
        }

        public void SnapAndLook()
        {
            if (target == null)
                return;

            Vector3 anchor = new Vector3(0f, 0f, target.position.z);
            transform.position = anchor + offset;

            Vector3 lookPoint = anchor + Vector3.up * lookHeight + Vector3.forward * lookAheadMeters;
            transform.rotation = Quaternion.LookRotation(lookPoint - transform.position);
        }
    }
}
