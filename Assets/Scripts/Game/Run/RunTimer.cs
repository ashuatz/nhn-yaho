using System;
using UnityEngine;

namespace Scavenger.Run
{
    /// <summary>
    /// 런 시간 관리. 경과 스톱워치(Elapsed) + 제한 시간 카운트다운(Remaining).
    /// 웹 이식(ADR-0008): 제한 시간 안에 탈출 지점에 도달하지 못하면 실패.
    /// 붕괴 전선(ADR-0006)과 병행하는 이중 압박. 시간 초과 시 TimeExpired 발화.
    /// </summary>
    public sealed class RunTimer : MonoBehaviour
    {
        public float Elapsed { get; private set; }
        public bool IsTicking { get; private set; }

        /// <summary>제한 시간(초). RunManager가 RunSettings에서 주입.</summary>
        public float LimitSeconds { get; private set; }

        /// <summary>남은 시간(초). 0이면 시간 초과. HUD 카운트다운 표시용.</summary>
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

        /// <summary>제한 시간 초과 알림 (1회). RunManager가 구독해 실패 처리.</summary>
        public event Action TimeExpired;

        bool expiredFired;

        public void Begin(float limitSeconds)
        {
            Elapsed = 0f;
            LimitSeconds = limitSeconds;
            IsTicking = true;
            expiredFired = false;
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

            if (expiredFired)
                return;

            if (LimitSeconds <= 0f)
                return;

            if (Elapsed < LimitSeconds)
                return;

            // 제한 시간 도달 - 1회만 알린다
            expiredFired = true;
            TimeExpired?.Invoke();
        }
    }
}
