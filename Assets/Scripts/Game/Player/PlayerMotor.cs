using UnityEngine;

namespace Scavenger.Player
{
    /// <summary>
    /// CharacterController 기반 이동만 담당. 상태 판정은 PlayerController가 소유.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        public float forwardSpeed = 4f;
        public float lateralSpeed = 5f;

        /// <summary>이동 가능한 복도 반폭. 구간 진입 시 SegmentSpawner가 갱신.</summary>
        public float corridorHalfWidth = 3.5f;

        CharacterController controller;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        /// <summary>
        /// 한 프레임 이동. lateral은 -1..1 입력, advance가 false면 전진 없이 좌우만.
        /// </summary>
        public void Step(float lateral, bool advance)
        {
            float deltaTime = Time.deltaTime;

            if (deltaTime <= 0f)
                return;

            Vector3 velocity = Vector3.zero;

            if (advance)
                velocity.z = forwardSpeed;

            // CharacterController는 transform.position 직접 설정을 무시할 수 있으므로
            // 이동 전에 복도 경계를 넘지 않도록 좌우 속도를 미리 깎는다
            float currentX = transform.position.x;
            float targetX = currentX + lateral * lateralSpeed * deltaTime;
            targetX = Mathf.Clamp(targetX, -corridorHalfWidth, corridorHalfWidth);
            velocity.x = (targetX - currentX) / deltaTime;

            // 접지 유지용 간단 중력 (경사/단차 없음 전제의 그레이박스)
            velocity.y = -9.81f;

            controller.Move(velocity * deltaTime);
        }
    }
}
