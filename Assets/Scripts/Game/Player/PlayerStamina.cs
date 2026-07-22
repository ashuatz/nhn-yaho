using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Player
{
    /// <summary>
    /// 스테미나 (사용자 지시 - HUD 표시 대상). 무게 압박과 연동:
    /// 과적 상태로 이동하는 동안 소모되고, 그 외에는 회복된다.
    /// 바닥나면 탈진 - 회복 임계까지 추가 감속 (Motor.StaminaScale).
    /// 스프린트 도입 시 소모 소스를 여기에 추가한다. 수치는 Player 프리팹 튜닝 지점.
    /// 판정 로직은 정적 순수 함수 - EditMode 테스트 가능 구조 (CarryLoad와 동일 패턴).
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    [RequireComponent(typeof(CarryLoad))]
    public sealed class PlayerStamina : MonoBehaviour
    {
        [Header("최대 스테미나")]
        public float maxStamina = 100f;

        [Header("소모 (초당) - 과적/초과적 상태로 이동 중")]
        public float drainOverloaded = 7f;
        public float drainSeverelyOverloaded = 14f;

        [Header("회복 (초당) - 소모 조건이 아닐 때")]
        public float regenPerSecond = 10f;

        [Header("탈진: 0 도달 시 감속, 회복 임계 도달까지 유지")]
        [Range(0.1f, 1f)] public float exhaustedSpeedScale = 0.55f;
        public float recoverThreshold = 30f;

        public float Current { get; private set; }
        public bool IsExhausted { get; private set; }

        public float Normalized
        {
            get { return maxStamina > 0f ? Current / maxStamina : 0f; }
        }

        PlayerMotor motor;
        CarryLoad carryLoad;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            carryLoad = GetComponent<CarryLoad>();
            Current = maxStamina;
        }

        // 튜닝 실수 가드: 회복 임계가 최대치를 넘으면 탈진에서 영원히 못 벗어난다
        void OnValidate()
        {
            maxStamina = Mathf.Max(1f, maxStamina);
            recoverThreshold = Mathf.Clamp(recoverThreshold, 0f, maxStamina);
        }

        // 비활성/파괴 경로에서 탈진 감속이 모터에 남지 않게 복구
        void OnDisable()
        {
            if (motor != null)
                motor.StaminaScale = 1f;
        }

        void Update()
        {
            RunManager run = RunManager.Instance;

            if (run == null || run.StateMachine.Current != RunState.Running)
                return;

            float drainRate = ResolveDrainRate(
                carryLoad.Stage, motor.IsMoving, drainOverloaded, drainSeverelyOverloaded);

            if (drainRate > 0f)
                Current = Mathf.Max(0f, Current - drainRate * Time.deltaTime);
            else
                Current = Mathf.Min(maxStamina, Current + regenPerSecond * Time.deltaTime);

            // 탈진 히스테리시스: 0에서 걸리고 임계 회복 전까지 풀리지 않는다
            if (!IsExhausted && Current <= 0f)
                IsExhausted = true;
            else if (IsExhausted && Current >= recoverThreshold)
                IsExhausted = false;

            motor.StaminaScale = IsExhausted ? exhaustedSpeedScale : 1f;
        }

        /// <summary>과적 상태로 이동 중일 때만 소모. 0 = 소모 없음(회복).</summary>
        public static float ResolveDrainRate(
            CarryLoadStage stage, bool isMoving, float overloadedRate, float severeRate)
        {
            if (!isMoving)
                return 0f;

            if (stage == CarryLoadStage.SeverelyOverloaded)
                return severeRate;

            if (stage == CarryLoadStage.Overloaded)
                return overloadedRate;

            return 0f;
        }

        /// <summary>런 재시작 시 GameFlow가 호출.</summary>
        public void ResetFull()
        {
            Current = maxStamina;
            IsExhausted = false;
            motor.StaminaScale = 1f;
        }
    }
}
