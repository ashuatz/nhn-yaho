using UnityEngine;

namespace Scavenger.Player
{
    /// <summary>
    /// 하이퍼캐주얼풍 스텝 연출: 스텝(가상 그리드 1칸) 동안 홉(포물선) + 공중 스트레치,
    /// 착지 시 스쿼시. 비주얼 자식만 조작하고 로직 루트/콜라이더는 건드리지 않는다.
    /// 수치는 전부 인스펙터 튜닝 지점 (이동느낌 우선 검증).
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerStepAnimator : MonoBehaviour
    {
        [Header("비주얼 루트 (비우면 자식 'Visual' 자동 탐색)")]
        [SerializeField] Transform visual;

        [Header("홉 (스텝당 점프)")]
        public float hopHeight = 0.35f;

        [Header("스쿼시/스트레치 (1 = 변형 없음)")]
        public float airStretch = 1.15f;
        public float landSquash = 0.82f;
        public float landRecoverSeconds = 0.12f;

        PlayerMotor motor;
        bool wasStepping;
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

            bool stepping = motor.IsStepping;

            // 착지 순간: 스쿼시 시작
            if (wasStepping && !stepping)
                squashRemaining = landRecoverSeconds;

            wasStepping = stepping;

            if (stepping)
            {
                ApplyAir(motor.StepProgress01);
                return;
            }

            ApplyGrounded();
        }

        void ApplyAir(float progress01)
        {
            // 포물선 홉 + 정점에서 최대 스트레치
            float arc = Mathf.Sin(progress01 * Mathf.PI);

            float stretchY = Mathf.Lerp(1f, airStretch, arc);
            float compensateXz = 1f / Mathf.Sqrt(stretchY);

            visual.localPosition = new Vector3(0f, arc * hopHeight, 0f);
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
