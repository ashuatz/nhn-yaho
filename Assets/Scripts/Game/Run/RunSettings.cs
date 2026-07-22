using UnityEngine;

namespace Scavenger.Run
{
    /// <summary>
    /// 런 규칙 데이터. 에셋이 없으면 GameBootstrap이 기본값으로 생성한다 (그레이박스 편의).
    /// </summary>
    [CreateAssetMenu(menuName = "Scavenger/Run Settings", fileName = "RunSettings")]
    public sealed class RunSettings : ScriptableObject
    {
        [Header("시드. 0이면 매 런 랜덤, 그 외에는 고정 재현용")]
        public int seedOverride = 0;

        [Header("제한 시간(초). 웹 이식(ADR-0008): 이 안에 탈출 지점 도달 못하면 실패.")]
        [Header("붕괴 전선과 병행하는 이중 압박 (ADR-0006 타이머 폐기 일부 복원)")]
        public float timeLimitSeconds = 150f;

        public static RunSettings CreateDefault()
        {
            RunSettings settings = CreateInstance<RunSettings>();
            settings.name = "RunSettings (Default)";
            return settings;
        }
    }
}
