using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Player
{
    /// <summary>
    /// 적재 단계 (M2-1). 무게 비율(합산/최대) 기준 4경계 -> 5단계 (웹 프로토타입 이식).
    /// 가벼움 &lt; 25% &lt;= 보통 &lt; 50% &lt;= 무거움 &lt; 75% &lt;= 매우무거움 &lt; 90% &lt;= 과적.
    /// </summary>
    public enum CarryLoadStage
    {
        Light,
        Normal,
        Heavy,
        VeryHeavy,
        Overloaded,
    }

    /// <summary>
    /// 무게 -> 이동속도 배율 (M2-1). 런 인벤토리의 무게 합산을 최대 적재량 대비
    /// 비율로 환산해 5단계를 판정하고 PlayerMotor.SpeedScale에 반영한다.
    /// 많이 주울수록 느려져 붕괴 전선에 쫓기는 선택 압박이 목적.
    /// 비율 구간별 페널티(웹 이식): 25%->-7% / 50%->-13% / 75%->-23% / 90%->-33%.
    /// 임계/배율은 Player 프리팹 튜닝 지점 (코드 상수 금지 원칙).
    /// 판정 로직은 정적 순수 함수 - EditMode 테스트 대상.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class CarryLoad : MonoBehaviour
    {
        [Header("최대 적재량 (kg). 무게 비율 = 합산 / 이 값 (웹 MAXW 대응)")]
        public float maxCarryWeight = 45f;

        [Header("단계별 이동속도 배율 (일반=1). 웹 이식: -7/-13/-23/-33%")]
        [Range(0.1f, 1f)] public float heavySpeedScale = 0.93f;
        [Range(0.1f, 1f)] public float veryHeavySpeedScale = 0.87f;
        [Range(0.1f, 1f)] public float severeSpeedScale = 0.77f;
        [Range(0.1f, 1f)] public float overloadedSpeedScale = 0.67f;

        public CarryLoadStage Stage { get; private set; } = CarryLoadStage.Light;
        public float TotalWeight { get; private set; }

        /// <summary>무게 비율 0..1 (합산/최대). HUD 바 표시용.</summary>
        public float LoadRatio { get; private set; }

        // 무게 비율 경계 (웹 프로토타입 고정값). 프리팹 노출 없이 코드 상수로 유지 -
        // 이 곡선 자체가 이식 대상이라 튜닝은 배율/최대량으로 한다
        const float HeavyRatio = 0.25f;
        const float VeryHeavyRatio = 0.5f;
        const float SevereRatio = 0.75f;
        const float OverloadedRatio = 0.9f;

        PlayerMotor motor;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
        }

        void Update()
        {
            RefreshNow();
        }

        // 튜닝 실수 가드: 0 나눗셈 방지 + 무거울수록 느려야 하므로 배율 단조 감소 강제
        void OnValidate()
        {
            maxCarryWeight = Mathf.Max(0.01f, maxCarryWeight);

            veryHeavySpeedScale = Mathf.Min(veryHeavySpeedScale, heavySpeedScale);
            severeSpeedScale = Mathf.Min(severeSpeedScale, veryHeavySpeedScale);
            overloadedSpeedScale = Mathf.Min(overloadedSpeedScale, severeSpeedScale);
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
            LoadRatio = Mathf.Clamp01(TotalWeight / maxCarryWeight);
            Stage = EvaluateStage(LoadRatio);

            motor.SpeedScale = ResolveSpeedScale(
                Stage, heavySpeedScale, veryHeavySpeedScale, severeSpeedScale, overloadedSpeedScale);
        }

        /// <summary>무게 비율(0..1) -> 단계. 경계 도달(이상)이면 상위 단계.</summary>
        public static CarryLoadStage EvaluateStage(float loadRatio)
        {
            if (loadRatio >= OverloadedRatio)
                return CarryLoadStage.Overloaded;

            if (loadRatio >= SevereRatio)
                return CarryLoadStage.VeryHeavy;

            if (loadRatio >= VeryHeavyRatio)
                return CarryLoadStage.Heavy;

            if (loadRatio >= HeavyRatio)
                return CarryLoadStage.Normal;

            return CarryLoadStage.Light;
        }

        public static float ResolveSpeedScale(
            CarryLoadStage stage,
            float heavyScale,
            float veryHeavyScale,
            float severeScale,
            float overloadedScale)
        {
            if (stage == CarryLoadStage.Overloaded)
                return overloadedScale;

            if (stage == CarryLoadStage.VeryHeavy)
                return severeScale;

            if (stage == CarryLoadStage.Heavy)
                return veryHeavyScale;

            if (stage == CarryLoadStage.Normal)
                return heavyScale;

            return 1f;
        }

        /// <summary>HUD 표기용 단계 이름 (웹 프로토타입 라벨).</summary>
        public static string StageLabel(CarryLoadStage stage)
        {
            if (stage == CarryLoadStage.Overloaded)
                return "과적";

            if (stage == CarryLoadStage.VeryHeavy)
                return "매우 무거움";

            if (stage == CarryLoadStage.Heavy)
                return "무거움";

            if (stage == CarryLoadStage.Normal)
                return "보통";

            return "가벼움";
        }
    }
}
