using UnityEngine;

namespace Scavenger.Player
{
    /// <summary>
    /// CharacterController 기반 이동만 담당. 상태 판정은 PlayerController가 소유.
    /// 2D 벡터 홀드 이동 (ADR-0007): 입력 벡터가 있는 동안 그 방향으로 이동한다.
    /// 토글 방식(ADR-0006)은 폐기. 아날로그 크기(조이스틱)는 속도에 비례 반영.
    /// 낙사: 바닥이 없으면 중력으로 떨어진다 (사망 판정은 PlayerController).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [Header("이동 (이동느낌 튜닝 지점)")]
        public float moveSpeed = 5f;

        [Header("가속 반응 (지수 보간 계수. 클수록 빠릿, 웹 프로토타입 기본 10)")]
        public float acceleration = 10f;

        [Header("무게가 실릴수록 가속 반응 저하 (SpeedScale 비례 최소 반응 비율)")]
        [Range(0f, 1f)] public float loadedAccelFloor = 0.55f;

        /// <summary>이동속도 배율. 과적(CarryLoad) 등 외부 시스템이 설정. 1 = 정상.</summary>
        public float SpeedScale { get; set; } = 1f;

        /// <summary>이동 가능한 복도 반폭. GameFlow가 구간 정의로 갱신.</summary>
        public float corridorHalfWidth = 3.5f;

        /// <summary>후퇴 한계 z (붕괴 전선 앞). CollapseFront가 매 프레임 갱신.</summary>
        public float MinZ { get; set; } = float.NegativeInfinity;

        /// <summary>
        /// 카메라 가시 영역 후방 한계 (벨트스크롤 규칙 - 사용자 지시).
        /// FollowCamera가 매 프레임 갱신. 붕괴 한계와 max로 합성된다.
        /// </summary>
        public float CameraMinZ { get; set; } = float.NegativeInfinity;

        /// <summary>현재 이동 입력 벡터 (x = 좌우, y = 전후). 크기 0..1.</summary>
        public Vector2 MoveInput { get; private set; }

        public bool IsMoving
        {
            get { return MoveInput.sqrMagnitude > 0.0001f; }
        }

        /// <summary>접지 여부. 낙하 중 워크 밥을 끄는 데 사용.</summary>
        public bool IsGrounded
        {
            get { return controller != null && controller.isGrounded; }
        }

        [Header("외부 임펄스 감쇠 (m/s^2) - 밀기 트랩 등")]
        public float impulseDamping = 10f;

        CharacterController controller;
        float fallVelocity;
        Vector3 externalVelocity;

        // 지수 보간되는 수평 이동 속도 (관성). 입력이 사라져도 감쇠하며 멈춘다 -
        // 임펄스(externalVelocity)는 별도 관리라 여기 포함하지 않는다 (타격감 유지)
        Vector2 smoothedMove;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        /// <summary>
        /// 외부 충격 속도 (밀기 트랩 등, M3-3). 감쇠하며 사라진다.
        /// 이동 입력과 합산 후 복도/붕괴 클램프를 그대로 받는다.
        /// </summary>
        public void AddImpulse(Vector3 velocity)
        {
            externalVelocity += velocity;
        }

        /// <summary>이동 입력 설정. 크기 1 초과는 클램프 (대각 가속 방지).</summary>
        public void SetMoveInput(Vector2 input)
        {
            MoveInput = Vector2.ClampMagnitude(input, 1f);
        }

        public void ClearMoveInput()
        {
            MoveInput = Vector2.zero;
        }

        /// <summary>한 프레임 이동. 상태가 이동을 허용할 때만 호출된다.</summary>
        public void Tick()
        {
            float deltaTime = Time.deltaTime;

            if (deltaTime <= 0f)
                return;

            Vector3 velocity = Vector3.zero;

            // z 하한 = 붕괴 전선과 카메라 후방 한계 중 앞선 것. 단, 현재 위치보다
            // 앞으로 당기지 않는다 - 정지한 플레이어를 밀어주는 컨베이어 금지
            // (검증 반영, ADR-0006: 정지 = 발밑 붕괴 = 낙사가 압박의 본질)
            float backwardLimit = Mathf.Min(Mathf.Max(MinZ, CameraMinZ), transform.position.z);

            // 입력이 만드는 목표 이동 속도 (관성 보간의 목표점)
            Vector2 targetMove = new Vector2(
                MoveInput.x * moveSpeed * SpeedScale,
                MoveInput.y * moveSpeed * SpeedScale);

            // 지수 보간으로 목표 속도를 따라간다 (관성). 무게가 실릴수록(SpeedScale 낮을수록)
            // 반응 계수를 loadedAccelFloor까지 낮춰 무거운 몸을 끌고 가는 감각을 만든다.
            // 웹 프로토타입 이식: k = 1 - exp(-accel*dt*(floor + (1-floor)*load))
            float loadFactor = Mathf.Lerp(loadedAccelFloor, 1f, Mathf.Clamp01(SpeedScale));
            float k = 1f - Mathf.Exp(-acceleration * deltaTime * loadFactor);

            smoothedMove += (targetMove - smoothedMove) * k;

            // 보간된 이동 속도 + 외부 임펄스 합산 후 축별 경계 클램프
            float desiredX = smoothedMove.x + externalVelocity.x;
            float desiredZ = smoothedMove.y + externalVelocity.z;

            velocity.x = ComputeAxisSpeed(
                transform.position.x, desiredX, -corridorHalfWidth, corridorHalfWidth, deltaTime);
            velocity.z = ComputeAxisSpeed(
                transform.position.z, desiredZ, backwardLimit, float.PositiveInfinity, deltaTime);

            externalVelocity = Vector3.MoveTowards(
                externalVelocity, Vector3.zero, impulseDamping * deltaTime);

            // 낙사용 누적 중력 (접지 시 소폭 유지로 접지 판정 안정화)
            if (controller.isGrounded)
                fallVelocity = -2f;
            else
                fallVelocity -= 9.81f * deltaTime;

            velocity.y = fallVelocity;

            controller.Move(velocity * deltaTime);
        }

        /// <summary>런 재시작 등에서 낙하 속도/잔존 임펄스/관성 속도 초기화.</summary>
        public void ResetVertical()
        {
            fallVelocity = 0f;
            externalVelocity = Vector3.zero;

            // 관성 잔재 제거 - 재시작 첫 프레임에 이전 런의 속도가 남지 않게
            smoothedMove = Vector2.zero;
        }

        // CharacterController는 transform 직접 설정을 무시할 수 있으므로
        // 경계를 넘지 않도록 이동 전에 축 속도를 미리 깎는다
        float ComputeAxisSpeed(float current, float desiredSpeed, float min, float max, float deltaTime)
        {
            float target = current + desiredSpeed * deltaTime;
            target = Mathf.Clamp(target, min, max);

            return (target - current) / deltaTime;
        }
    }
}
