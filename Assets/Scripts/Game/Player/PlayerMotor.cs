using UnityEngine;

namespace Scavenger.Player
{
    /// <summary>
    /// CharacterController 기반 이동만 담당. 상태 판정은 PlayerController가 소유.
    /// 클릭 스텝 전진 (ADR-0002): 기본 정지, RequestStep 1회 = 고정 거리 전진 트윈.
    /// 스텝 중 요청 1회는 버퍼링되어 연타가 끊기지 않는다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [Header("스텝 전진 (이동느낌 튜닝 지점)")]
        public float stepDistance = 2.4f;
        public float stepDuration = 0.22f;

        public float lateralSpeed = 5f;

        /// <summary>이동 가능한 복도 반폭. 구간 진입 시 SegmentSpawner가 갱신.</summary>
        public float corridorHalfWidth = 3.5f;

        public bool IsStepping
        {
            get { return stepRemaining > 0f; }
        }

        /// <summary>현재 스텝의 진행도 0..1. 스텝 애니메이터가 읽는다.</summary>
        public float StepProgress01
        {
            get
            {
                if (!IsStepping || stepDistance <= 0f)
                    return 0f;

                return 1f - stepRemaining / stepDistance;
            }
        }

        CharacterController controller;
        float stepRemaining;
        bool stepBuffered;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        /// <summary>클릭 1회 = 스텝 1회. 스텝 중이면 1회까지 버퍼.</summary>
        public void RequestStep()
        {
            if (IsStepping)
            {
                stepBuffered = true;
                return;
            }

            stepRemaining = stepDistance;
        }

        /// <summary>진행 중인 스텝과 버퍼를 모두 버린다 (사망, 선택지 진입 등).</summary>
        public void CancelSteps()
        {
            stepRemaining = 0f;
            stepBuffered = false;
        }

        /// <summary>
        /// 한 프레임 이동. lateral은 -1..1 입력. 스텝 진행은 내부 상태로 처리.
        /// </summary>
        public void Tick(float lateral)
        {
            float deltaTime = Time.deltaTime;

            if (deltaTime <= 0f)
                return;

            Vector3 velocity = Vector3.zero;

            velocity.z = ConsumeStepSpeed(deltaTime);
            velocity.x = ComputeLateralSpeed(lateral, deltaTime);

            // 접지 유지용 간단 중력 (경사/단차 없음 전제의 그레이박스)
            velocity.y = -9.81f;

            controller.Move(velocity * deltaTime);
        }

        float ConsumeStepSpeed(float deltaTime)
        {
            if (!IsStepping)
                return 0f;

            float stepSpeed = stepDistance / Mathf.Max(stepDuration, 0.01f);
            float advance = Mathf.Min(stepSpeed * deltaTime, stepRemaining);

            stepRemaining -= advance;

            // 스텝 종료 시 버퍼 소비 - 연타 유지
            if (!IsStepping && stepBuffered)
            {
                stepBuffered = false;
                stepRemaining = stepDistance;
            }

            return advance / deltaTime;
        }

        float ComputeLateralSpeed(float lateral, float deltaTime)
        {
            // CharacterController는 transform.position 직접 설정을 무시할 수 있으므로
            // 이동 전에 복도 경계를 넘지 않도록 좌우 속도를 미리 깎는다
            float currentX = transform.position.x;
            float targetX = currentX + lateral * lateralSpeed * deltaTime;
            targetX = Mathf.Clamp(targetX, -corridorHalfWidth, corridorHalfWidth);

            return (targetX - currentX) / deltaTime;
        }
    }
}
