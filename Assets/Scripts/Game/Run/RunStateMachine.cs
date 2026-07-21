using System;
using UnityEngine;

namespace Scavenger.Run
{
    /// <summary>
    /// 런 상태 전이만 담당한다. 타이머, 정산 등은 별도 컴포넌트 (RunController 비대화 방지).
    /// </summary>
    public sealed class RunStateMachine : MonoBehaviour
    {
        public RunState Current { get; private set; } = RunState.Ready;

        /// <summary>
        /// (이전 상태, 새 상태) 순으로 전달.
        /// </summary>
        public event Action<RunState, RunState> StateChanged;

        public bool TryTransition(RunState next)
        {
            if (!IsValidTransition(Current, next))
            {
                UnityEngine.Debug.LogWarning($"[Run] Invalid transition: {Current} -> {next}");
                return false;
            }

            RunState previous = Current;
            Current = next;

            StateChanged?.Invoke(previous, next);
            return true;
        }

        static bool IsValidTransition(RunState from, RunState to)
        {
            if (from == to)
                return false;

            // 대기 상태에서는 런 시작만 가능
            if (from == RunState.Ready)
                return to == RunState.Running;

            // 진행 중에는 탈출 또는 사망으로만 종료
            if (from == RunState.Running)
                return to == RunState.Extracted || to == RunState.Dead;

            // 종료 상태(탈출/사망)에서는 재시작만 가능
            return to == RunState.Ready;
        }
    }
}
