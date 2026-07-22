using System;
using UnityEngine;

namespace Scavenger.Player
{
    /// <summary>
    /// 플레이어 체력 (사용자 지시 - HUD 표시 대상).
    /// 피해 소스: 폭탄/낙하물 직격 = 2, 돌진 적 = 1. 낙사는 여전히 즉사 (Kill 직행).
    /// 피격 직후 짧은 무적 - 같은 위협의 다중 프레임 중복 피해 방지.
    /// 수치는 Player 프리팹 튜닝 지점.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [Header("최대 체력 (그레이박스: 히트 수)")]
        public int maxHealth = 3;

        [Header("피격 후 무적 시간 (초)")]
        public float invulnerableSeconds = 0.8f;

        public int Current { get; private set; }

        /// <summary>체력 변화 알림 (HUD 참조는 폴링이라 필수 아님 - 확장 지점).</summary>
        public event Action<int, int> Changed;

        float invulnerableUntil;
        PlayerController controller;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            Current = maxHealth;
        }

        /// <summary>
        /// 피해 적용. 0 이하가 되면 사망 처리 (PlayerController.Kill).
        /// 무적 시간 중에는 무시된다. 낙사 등 즉사는 Kill을 직접 호출할 것.
        /// </summary>
        public void Damage(int amount, string cause)
        {
            if (amount <= 0 || controller.State == PlayerState.Dead)
                return;

            if (Time.time < invulnerableUntil)
                return;

            invulnerableUntil = Time.time + invulnerableSeconds;

            int previous = Current;
            Current = Mathf.Max(0, Current - amount);
            Changed?.Invoke(previous, Current);

            UnityEngine.Debug.Log($"[Health] -{amount} ({cause}) -> {Current}/{maxHealth}");

            if (Current > 0)
                return;

            controller.Kill(cause);
        }

        /// <summary>런 재시작 시 GameFlow가 호출.</summary>
        public void ResetFull()
        {
            int previous = Current;
            Current = maxHealth;
            invulnerableUntil = 0f;

            if (previous != Current)
                Changed?.Invoke(previous, Current);
        }
    }
}
