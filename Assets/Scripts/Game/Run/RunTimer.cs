using UnityEngine;

namespace Scavenger.Run
{
    /// <summary>
    /// 런 경과 시간 기록 (스톱워치). 템포 계측/정산 로그용.
    /// 제한 시간/탈출 잠금 규칙은 바닥 붕괴로 대체되어 제거됨 (ADR-0006).
    /// </summary>
    public sealed class RunTimer : MonoBehaviour
    {
        public float Elapsed { get; private set; }
        public bool IsTicking { get; private set; }

        public void Begin()
        {
            Elapsed = 0f;
            IsTicking = true;
        }

        public void Stop()
        {
            IsTicking = false;
        }

        void Update()
        {
            if (!IsTicking)
                return;

            Elapsed += Time.deltaTime;
        }
    }
}
