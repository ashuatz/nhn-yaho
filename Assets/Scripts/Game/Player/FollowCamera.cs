using UnityEngine;

namespace Scavenger.Player
{
    /// <summary>
    /// 쿼터뷰 추적 카메라. 회피와 주변 파밍 요소 시인성을 위한 탑다운 기울임.
    /// 캐릭터 에셋 생성 게이트(구현계획 v0.0.2 섹션 3.3)의 카메라 각도 기준값이 된다.
    /// </summary>
    public sealed class FollowCamera : MonoBehaviour
    {
        public Transform target;

        [Header("쿼터뷰 오프셋. 에셋 생성 전 동결 대상")]
        public Vector3 offset = new Vector3(0f, 10f, -7f);

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
            transform.rotation = Quaternion.LookRotation(anchor + Vector3.forward * 2f - transform.position);
        }
    }
}
