using UnityEngine;

namespace Scavenger.Player
{
    /// <summary>
    /// CharacterController 기반 이동만 담당. 상태 판정은 PlayerController가 소유.
    /// 4방향 토글 이동 (ADR-0006): 방향을 지정하면 그 방향으로 연속 이동한다.
    /// 낙사 도입: 바닥이 없으면 중력으로 떨어진다 (사망 판정은 PlayerController).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [Header("이동 (이동느낌 튜닝 지점)")]
        public float moveSpeed = 4.2f;

        /// <summary>이동속도 배율. 과적(CarryLoad) 등 외부 시스템이 설정. 1 = 정상.</summary>
        public float SpeedScale { get; set; } = 1f;

        /// <summary>이동 가능한 복도 반폭. GameFlow가 구간 정의로 갱신.</summary>
        public float corridorHalfWidth = 3.5f;

        /// <summary>후퇴 한계 z (붕괴 전선 앞). CollapseFront가 매 프레임 갱신.</summary>
        public float MinZ { get; set; } = float.NegativeInfinity;

        /// <summary>현재 토글된 이동 방향 (카디널 단위 벡터 또는 zero).</summary>
        public Vector2 Direction { get; private set; }

        public bool IsMoving
        {
            get { return Direction != Vector2.zero; }
        }

        /// <summary>접지 여부. 낙하 중 워크 밥을 끄는 데 사용.</summary>
        public bool IsGrounded
        {
            get { return controller != null && controller.isGrounded; }
        }

        CharacterController controller;
        float fallVelocity;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        /// <summary>방향 토글. 같은 방향이면 정지, 다른 방향이면 전환.</summary>
        public void ToggleDirection(Vector2 cardinal)
        {
            if (Direction == cardinal)
            {
                Direction = Vector2.zero;
                return;
            }

            Direction = cardinal;
        }

        public void ClearDirection()
        {
            Direction = Vector2.zero;
        }

        /// <summary>한 프레임 이동. 상태가 이동을 허용할 때만 호출된다.</summary>
        public void Tick()
        {
            float deltaTime = Time.deltaTime;

            if (deltaTime <= 0f)
                return;

            Vector3 velocity = Vector3.zero;

            // z 하한은 현재 위치보다 앞으로 당기지 않는다 - 붕괴 전선이 정지한
            // 플레이어를 밀어주는 컨베이어가 되면 안 된다 (검증 반영, ADR-0006:
            // 정지 = 발밑 붕괴 = 낙사가 압박의 본질)
            float backwardLimit = Mathf.Min(MinZ, transform.position.z);

            velocity.x = ComputeAxisSpeed(
                transform.position.x, Direction.x, -corridorHalfWidth, corridorHalfWidth, deltaTime);
            velocity.z = ComputeAxisSpeed(
                transform.position.z, Direction.y, backwardLimit, float.PositiveInfinity, deltaTime);

            // 낙사용 누적 중력 (접지 시 소폭 유지로 접지 판정 안정화)
            if (controller.isGrounded)
                fallVelocity = -2f;
            else
                fallVelocity -= 9.81f * deltaTime;

            velocity.y = fallVelocity;

            controller.Move(velocity * deltaTime);
        }

        /// <summary>런 재시작 등에서 낙하 속도 초기화.</summary>
        public void ResetVertical()
        {
            fallVelocity = 0f;
        }

        // CharacterController는 transform 직접 설정을 무시할 수 있으므로
        // 경계를 넘지 않도록 이동 전에 축 속도를 미리 깎는다
        float ComputeAxisSpeed(float current, float axisInput, float min, float max, float deltaTime)
        {
            float target = current + axisInput * moveSpeed * SpeedScale * deltaTime;
            target = Mathf.Clamp(target, min, max);

            return (target - current) / deltaTime;
        }
    }
}
