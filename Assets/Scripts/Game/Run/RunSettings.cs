using UnityEngine;

namespace Scavenger.Run
{
    /// <summary>
    /// 런 규칙 데이터. 에셋이 없으면 GameBootstrap이 기본값으로 생성한다 (그레이박스 편의).
    /// </summary>
    [CreateAssetMenu(menuName = "Scavenger/Run Settings", fileName = "RunSettings")]
    public sealed class RunSettings : ScriptableObject
    {
        [Header("숨김 타이머 (초). 매 런 범위 내 랜덤 - 고정값은 암기됨")]
        public float timerMinSeconds = 150f;
        public float timerMaxSeconds = 210f;

        [Header("시드. 0이면 매 런 랜덤, 그 외에는 고정 재현용")]
        public int seedOverride = 0;

        public static RunSettings CreateDefault()
        {
            RunSettings settings = CreateInstance<RunSettings>();
            settings.name = "RunSettings (Default)";
            return settings;
        }
    }
}
