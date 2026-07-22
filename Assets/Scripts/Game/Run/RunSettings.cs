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

        // 시간 압박은 바닥 붕괴(CollapseFront)로 대체됨 (ADR-0006).
        // 붕괴 속도 튜닝은 SegmentSpawner 프리팹의 CollapseFront에서.

        public static RunSettings CreateDefault()
        {
            RunSettings settings = CreateInstance<RunSettings>();
            settings.name = "RunSettings (Default)";
            return settings;
        }
    }
}
