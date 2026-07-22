using UnityEngine;

namespace Scavenger.Player
{
    /// <summary>
    /// 하이퍼캐주얼풍 이동 연출 (ADR-0006에서 연속 이동용 워크 밥으로 개편).
    /// 이동 중: 통통 튀는 밥(짧은 홉 반복) + 공중 스트레치. 정지 순간: 착지 스쿼시.
    /// 비주얼 자식만 조작하고 로직 루트/콜라이더는 건드리지 않는다.
    /// 수치는 전부 인스펙터 튜닝 지점.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerStepAnimator : MonoBehaviour
    {
        [Header("비주얼 루트 (비우면 자식 'Visual' 자동 탐색)")]
        [SerializeField] Transform visual;

        [Header("워크 밥 (이동 중 반복 홉)")]
        public float bobHeight = 0.22f;
        public float bobFrequency = 5.5f;

        [Header("스쿼시/스트레치 (1 = 변형 없음)")]
        public float airStretch = 1.12f;
        public float landSquash = 0.85f;
        public float landRecoverSeconds = 0.12f;

        PlayerMotor motor;
        bool wasMoving;
        float bobPhase;
        float squashRemaining;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();

            if (visual == null)
                visual = transform.Find("Visual");
        }

        void LateUpdate()
        {
            if (visual == null)
                return;

            // 낙하 중에는 연출 정지 (낙사 가독성)
            bool moving = motor.IsMoving && motor.IsGrounded;

            // 정지 순간: 착지 스쿼시
            if (wasMoving && !moving)
            {
                squashRemaining = landRecoverSeconds;
                bobPhase = 0f;
            }

            wasMoving = moving;

            if (moving)
            {
                ApplyBob();
                return;
            }

            ApplyGrounded();
        }

        void ApplyBob()
        {
            bobPhase += Time.deltaTime * bobFrequency;

            // |sin|으로 짧은 홉 반복 + 정점에서 최대 스트레치
            float arc = Mathf.Abs(Mathf.Sin(bobPhase * Mathf.PI));

            float stretchY = Mathf.Lerp(1f, airStretch, arc);
            float compensateXz = 1f / Mathf.Sqrt(stretchY);

            visual.localPosition = new Vector3(0f, arc * bobHeight, 0f);
            visual.localScale = new Vector3(compensateXz, stretchY, compensateXz);
        }

        void ApplyGrounded()
        {
            if (squashRemaining <= 0f)
            {
                visual.localPosition = Vector3.zero;
                visual.localScale = Vector3.one;
                return;
            }

            squashRemaining -= Time.deltaTime;

            // 스쿼시에서 원형으로 복귀 (부피 보존 보정 포함)
            float t = Mathf.Clamp01(squashRemaining / landRecoverSeconds);
            float squashY = Mathf.Lerp(1f, landSquash, t);
            float compensateXz = 1f / Mathf.Sqrt(squashY);

            visual.localPosition = Vector3.zero;
            visual.localScale = new Vector3(compensateXz, squashY, compensateXz);
        }
    }
}
