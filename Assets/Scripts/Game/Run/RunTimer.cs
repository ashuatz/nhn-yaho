using UnityEngine;

namespace Scavenger.Run
{
    /// <summary>
    /// 숨겨진 런 제한 시간. 실제 값은 플레이어에게 노출하지 않는다 (디버그 대시보드 전용).
    /// 정책: 루팅/선택지 중에도 진행, 일시정지 시에만 정지 (구현계획 v0.0.2 섹션 0.2).
    /// </summary>
    public sealed class RunTimer : MonoBehaviour
    {
        public float Elapsed { get; private set; }
        public float LimitSeconds { get; private set; }
        public bool IsTicking { get; private set; }

        public float Remaining
        {
            get
            {
                float remaining = LimitSeconds - Elapsed;

                if (remaining < 0f)
                    return 0f;

                return remaining;
            }
        }

        // 시간 초과 = 탈출 잠금. 사망이 아님 (ADR-0001 배경 결정).
        public bool IsExpired
        {
            get { return Elapsed >= LimitSeconds; }
        }

        public void Begin(float limitSeconds)
        {
            LimitSeconds = limitSeconds;
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
