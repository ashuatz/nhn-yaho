using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Player
{
    /// <summary>적재 단계 (M2-1). 무게 임계 2개로 3단계 판정.</summary>
    public enum CarryLoadStage
    {
        Normal,
        Overloaded,
        SeverelyOverloaded,
    }

    /// <summary>
    /// 무게 -> 이동속도 배율 (M2-1). 런 인벤토리의 무게 합산을 읽어
    /// 3단계(일반/과적/초과적)를 판정하고 PlayerMotor.SpeedScale에 반영한다.
    /// 많이 주울수록 느려져 붕괴 전선에 쫓기는 선택 압박이 목적.
    /// 임계/배율은 Player 프리팹 튜닝 지점 (코드 상수 금지 원칙).
    /// 판정 로직은 정적 순수 함수 - EditMode 테스트 대상.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class CarryLoad : MonoBehaviour
    {
        [Header("과적 임계 (무게 합산, 도달 시 해당 단계)")]
        public float overloadedAt = 8f;
        public float severelyOverloadedAt = 16f;

        [Header("단계별 이동속도 배율 (일반 = 1)")]
        [Range(0.1f, 1f)] public float overloadedSpeedScale = 0.7f;
        [Range(0.1f, 1f)] public float severelySpeedScale = 0.45f;

        public CarryLoadStage Stage { get; private set; } = CarryLoadStage.Normal;
        public float TotalWeight { get; private set; }

        PlayerMotor motor;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
        }

        void Update()
        {
            RefreshNow();
        }

        // 튜닝 실수 가드: 임계 역전(과적 단계 소실)과 초과적이 과적보다 빠른 배율 방지
        void OnValidate()
        {
            overloadedAt = Mathf.Max(0f, overloadedAt);
            severelyOverloadedAt = Mathf.Max(overloadedAt, severelyOverloadedAt);
            severelySpeedScale = Mathf.Min(severelySpeedScale, overloadedSpeedScale);
        }

        /// <summary>
        /// 즉시 재판정. 런 재시작 등 인벤토리가 방금 바뀐 시점에 호출해
        /// 다음 Update까지 이전 런의 배율이 남는 프레임을 없앤다 (Codex 검토 반영).
        /// </summary>
        public void RefreshNow()
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return;

            TotalWeight = run.Inventory.TotalWeight;
            Stage = EvaluateStage(TotalWeight, overloadedAt, severelyOverloadedAt);

            motor.SpeedScale = ResolveSpeedScale(Stage, overloadedSpeedScale, severelySpeedScale);
        }

        /// <summary>임계 도달(이상)이면 해당 단계. 상위 단계 우선.</summary>
        public static CarryLoadStage EvaluateStage(float weight, float overloadedAt, float severelyOverloadedAt)
        {
            if (weight >= severelyOverloadedAt)
                return CarryLoadStage.SeverelyOverloaded;

            if (weight >= overloadedAt)
                return CarryLoadStage.Overloaded;

            return CarryLoadStage.Normal;
        }

        public static float ResolveSpeedScale(CarryLoadStage stage, float overloadedScale, float severelyScale)
        {
            if (stage == CarryLoadStage.SeverelyOverloaded)
                return severelyScale;

            if (stage == CarryLoadStage.Overloaded)
                return overloadedScale;

            return 1f;
        }

        /// <summary>HUD 표기용 단계 이름.</summary>
        public static string StageLabel(CarryLoadStage stage)
        {
            if (stage == CarryLoadStage.SeverelyOverloaded)
                return "초과적";

            if (stage == CarryLoadStage.Overloaded)
                return "과적";

            return "일반";
        }
    }
}
