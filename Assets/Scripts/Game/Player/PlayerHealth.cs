using System;
using UnityEngine;

namespace Scavenger.Player
{
    /// <summary>
    /// 플레이어 체력 (HP). 지속/순간 피해를 누적하고 0 이하가 되면
    /// PlayerController.Kill로 사망 경로를 단일화한다 (즉사 요인은 여전히 Kill 직접 호출).
    /// 회복 수단 없음 - HP는 순수 소모 자원 (A안): 피해를 얼마나 덜 맞느냐가 실력 축.
    /// 판정 로직(ApplyDamage)은 정적 순수 함수 - EditMode 테스트 대상 (CarryLoad 관행).
    /// 수치는 Player 프리팹 튜닝 지점 (코드 상수 금지 원칙).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [Header("체력 (Player 프리팹 튜닝 지점)")]
        public float maxHealth = 100f;

        /// <summary>현재 체력. 0 이하 = 사망.</summary>
        public float Current { get; private set; }

        /// <summary>0..1 정규화 체력. HUD 바 표시용.</summary>
        public float Normalized
        {
            get
            {
                if (maxHealth <= 0f)
                    return 0f;

                return Mathf.Clamp01(Current / maxHealth);
            }
        }

        /// <summary>체력 변동 알림 (current, max). HUD가 구독해 즉시 갱신.</summary>
        public event Action<float, float> HealthChanged;

        /// <summary>피해를 받은 프레임 알림 (누적 피해량). 화면 붉은 플래시 등 피드백용.</summary>
        public event Action<float> Damaged;

        PlayerController controller;

        void Awake()
        {
            controller = GetComponent<PlayerController>();

            Current = maxHealth;
        }

        void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
        }

        /// <summary>
        /// 피해 적용 (지속 피해는 amount = dps * deltaTime으로 매 프레임 호출).
        /// 이미 사망했거나 피해가 0 이하면 무시. 0 이하로 떨어지면 Kill로 위임.
        /// </summary>
        public void Damage(float amount, string cause)
        {
            if (controller.State == PlayerState.Dead)
                return;

            if (amount <= 0f)
                return;

            Current = ApplyDamage(Current, amount);

            Damaged?.Invoke(amount);
            HealthChanged?.Invoke(Current, maxHealth);

            if (Current > 0f)
                return;

            // 사망 경로 단일화 - 상태 전이/런 종료/쉐이크는 Kill이 소유
            controller.Kill(cause);
        }

        /// <summary>런 재시작 시 GameFlow가 호출. 체력을 만땅으로 복구.</summary>
        public void ResetForNewRun()
        {
            Current = maxHealth;

            HealthChanged?.Invoke(Current, maxHealth);
        }

        /// <summary>체력 감산 (0 하한). 순수 함수 - EditMode 테스트 대상.</summary>
        public static float ApplyDamage(float current, float amount)
        {
            float next = current - amount;

            if (next < 0f)
                return 0f;

            return next;
        }
    }
}
