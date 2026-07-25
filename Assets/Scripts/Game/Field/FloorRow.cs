using UnityEngine;

namespace Scavenger.Field
{
    /// <summary>제거 진행 상태 (필드 규칙 문서 3.2 (3)).</summary>
    public enum FloorRowState
    {
        Normal,
        Pending,
        Falling,
    }

    /// <summary>
    /// 바닥 제거 단위 (필드 규칙 문서 3.2). 존 너비 전체를 덮는 1블록 두께의 행.
    /// 정상 -> 제거 예정(흔들림, 흔들림 시간) -> 제거(낙하)로 전이한다.
    /// 제거 예정은 마지막 경고 구간이라 아직 밟을 수 있고, 낙하 시작과 동시에
    /// 콜라이더가 꺼져 위에 있던 것은 떨어진다 (낙사).
    /// 행 위에 놓인 요소(드랍 아이템 등)는 자식으로 부착해 함께 낙하시킨다.
    /// </summary>
    public sealed class FloorRow : MonoBehaviour
    {
        public FloorRowState State { get; private set; } = FloorRowState.Normal;

        /// <summary>행 중심 z (월드). 제거 기준선 비교에 사용.</summary>
        public float CenterZ
        {
            get { return transform.position.z; }
        }

        const float FallSpeedInitial = 1.5f;
        const float FallGravity = 14f;
        const float FallTiltDegrees = 25f;
        const float DestroyDepth = -14f;
        const float ShakeAmplitude = 0.06f;
        const float ShakeFrequency = 26f;

        Vector3 restPosition;
        float pendingRemaining;
        float fallSpeed;
        Vector3 tiltAxis = Vector3.right;

        void Awake()
        {
            restPosition = transform.position;
        }

        /// <summary>
        /// 제거 예정으로 전환 (흔들림 시작). 이미 진행 중이면 무시한다.
        /// shakeSeconds가 0이면 즉시 낙하로 넘어간다.
        /// </summary>
        public void BeginPending(float shakeSeconds)
        {
            if (State != FloorRowState.Normal)
                return;

            if (shakeSeconds <= 0f)
            {
                BeginFall();
                return;
            }

            State = FloorRowState.Pending;
            pendingRemaining = shakeSeconds;
        }

        /// <summary>낙하 시작. 콜라이더를 끄고 아래로 떨어진다 (자식 포함).</summary>
        public void BeginFall()
        {
            if (State == FloorRowState.Falling)
                return;

            State = FloorRowState.Falling;

            transform.position = restPosition;
            fallSpeed = FallSpeedInitial;

            // 행마다 다른 기울기 축 - 판정과 무관한 낙하 연출용
            tiltAxis = new Vector3(1f, 0f, Mathf.Sin(restPosition.z)).normalized;

            Collider[] colliders = GetComponentsInChildren<Collider>();

            foreach (Collider rowCollider in colliders)
                rowCollider.enabled = false;
        }

        void Update()
        {
            if (State == FloorRowState.Pending)
            {
                TickPending();
                return;
            }

            if (State == FloorRowState.Falling)
                TickFalling();
        }

        void TickPending()
        {
            pendingRemaining -= Time.deltaTime;

            // 흔들림: 제자리 진동 - 곧 무너진다는 경고
            float offsetX = Mathf.Sin(Time.time * ShakeFrequency) * ShakeAmplitude;
            float offsetY = Mathf.Sin(Time.time * ShakeFrequency * 1.7f) * ShakeAmplitude * 0.5f;
            transform.position = restPosition + new Vector3(offsetX, offsetY, 0f);

            if (pendingRemaining > 0f)
                return;

            BeginFall();
        }

        void TickFalling()
        {
            float deltaTime = Time.deltaTime;

            fallSpeed += FallGravity * deltaTime;

            transform.position += Vector3.down * (fallSpeed * deltaTime);
            transform.Rotate(tiltAxis, FallTiltDegrees * deltaTime, Space.World);

            if (transform.position.y > DestroyDepth)
                return;

            Destroy(gameObject);
        }
    }
}
